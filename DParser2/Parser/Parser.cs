using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using System;

namespace D_Parser.Parser
{
    /// <summary>
    /// Parser for D Code
    /// </summary>
    public partial class DParser:DTokens, IDisposable
	{
		#region Properties
		/// <summary>
		/// Holds document structure
		/// </summary>
		DModule doc;

		public DModule Document
		{
			get { return doc; }
		}

		/// <summary>
		/// Modifiers for entire block
		/// </summary>
		Stack<DAttribute> BlockAttributes = new Stack<DAttribute>();
		/// <summary>
		/// Modifiers for current expression only
		/// </summary>
		Stack<DAttribute> DeclarationAttributes = new Stack<DAttribute>();

		bool ParseStructureOnly = false;
		public Lexer Lexer;

		/// <summary>
		/// Used to track the expression/declaration/statement/whatever which is handled currently.
		/// Required for code completion.
		/// </summary>
		public object LastParsedObject
		{ 
			get { return TrackerVariables.LastParsedObject; } 
			set { TrackerVariables.LastParsedObject = value; }
		}

		/// <summary>
		/// Required for code completion.
		/// True if a type/variable/method/etc. identifier is expected.
		/// </summary>
		public bool ExpectingIdentifier { set { TrackerVariables.ExpectingIdentifier = value; } }

		public ParserTrackerVariables TrackerVariables = new ParserTrackerVariables();

		DToken t
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				return (DToken)Lexer.CurrentToken;
			}
		}

		/// <summary>
		/// lookAhead token
		/// </summary>
		DToken la
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				return Lexer.LookAhead;
			}

			set
			{
				Lexer.LookAhead = value;
				laKind = value.Kind;
			}
		}
		int laKind = 0;

		bool IsEOF
		{
			get { return Lexer.IsEOF; }
		}

		public IList<ParserError> ParseErrors = new List<ParserError>();
		public const int MaxParseErrorsBeforeFailure = 100;

		#endregion

		public void Dispose()
		{
			doc = null;
			BlockAttributes.Clear();
			BlockAttributes = null;
			DeclarationAttributes.Clear();
			DeclarationAttributes = null;
			Lexer.Dispose();
			Lexer = null;
			TrackerVariables = null;
			ParseErrors = null;
		}

		public DParser(Lexer lexer)
		{
			this.Lexer = lexer;
			Lexer.LexerErrors = ParseErrors;
		}

		#region External interface
		/// <summary>
		/// Finds the last import statement and returns its end location (the position after the semicolon).
		/// If no import but module statement was found, the end location of this module statement will be returned.
		/// </summary>
		public static CodeLocation FindLastImportStatementEndLocation(DModule m, string moduleCode = null)
		{
			IStatement lastStmt = null;

			foreach (var s in m.StaticStatements)
				if (s is ImportStatement)
					lastStmt = s;
				else if (lastStmt != null)
					break;

			if (lastStmt != null)
				return lastStmt.EndLocation;

			if (m.OptionalModuleStatement != null)
				return lastStmt.EndLocation;

			if (moduleCode != null)
				using(var sr = new StreamReader(moduleCode))
				using (var lx = new Lexer(sr) { OnlyEnlistDDocComments = false })
				{
					lx.NextToken();

					if (lx.Comments.Count != 0)
						return lx.Comments[lx.Comments.Count - 1].EndPosition;
				}

			return new CodeLocation(1, 1);
		}

		public static BlockStatement ParseBlockStatement(string Code, INode ParentNode = null)
		{
			return ParseBlockStatement(Code, CodeLocation.Empty, ParentNode);
		}

		public static BlockStatement ParseBlockStatement(string Code, CodeLocation initialLocation, INode ParentNode = null)
		{
			var p = Create(new StringReader(Code));
			p.Lexer.SetInitialLocation(initialLocation);
			p.Step();

			return p.BlockStatement(ParentNode);
		}

        public static IExpression ParseExpression(string Code)
        {
            var p = Create(new StringReader(Code));
            p.Step();
            return p.Expression();
        }

		public static IExpression ParseAssignExpression(string Code)
		{
			var p = Create(new StringReader(Code));
			p.Step();
			return p.AssignExpression();
		}

        public static ITypeDeclaration ParseBasicType(string Code,out DToken OptionalToken)
        {
            OptionalToken = null;

            var p = Create(new StringReader(Code));
            p.Step();
            // Exception: If we haven't got any basic types as our first token, return this token via OptionalToken
            if (!p.IsBasicType() || p.laKind == __LINE__ || p.laKind == __FILE__)
            {
                p.Step();
                p.Peek(1);
                OptionalToken = p.t;

                // Only if a dot follows a 'this' or 'super' token we go on parsing; Return otherwise
                if (!((p.t.Kind == This || p.t.Kind == Super) && p.laKind == Dot))
                    return null;
            }
            
            var bt= p.BasicType();
            while (p.IsBasicType2())
            {
                var bt2 = p.BasicType2();
                bt2.InnerMost = bt;
                bt = bt2;
            }
            return bt;
        }

        public static IAbstractSyntaxTree ParseString(string ModuleCode,bool SkipFunctionBodies=false)
        {
            var p = Create(new StringReader(ModuleCode));
            return p.Parse(SkipFunctionBodies);
        }

        public static IAbstractSyntaxTree ParseFile(string File, bool SkipFunctionBodies=false)
        {
			var s = new StreamReader(File);
            var p=Create(s);
            var m = p.Parse(SkipFunctionBodies);
            m.FileName = File;
			m.ModuleName = Path.GetFileNameWithoutExtension(File);
			s.Close();
			return m;
        }

        /// <summary>
        /// Parses the module again
        /// </summary>
        /// <param name="Module"></param>
        public static void UpdateModule(IAbstractSyntaxTree Module)
        {
            var m = DParser.ParseFile(Module.FileName);
			Module.ParseErrors = m.ParseErrors;
            Module.AssignFrom(m);
        }

        public static void UpdateModuleFromText(IAbstractSyntaxTree Module, string Code)
        {
            var m = DParser.ParseString(Code);
			Module.ParseErrors = m.ParseErrors;
            Module.AssignFrom(m);
        }

        public static DParser Create(TextReader tr)
        {
			return new DParser(new Lexer(tr));
        }
		#endregion

		void PushAttribute(DAttribute attr, bool BlockAttributes)
		{
			var stk=BlockAttributes?this.BlockAttributes:this.DeclarationAttributes;

			// If attr would change the accessability of an item, remove all previously found (so the most near attribute that's next to the item is significant)
			if (DTokens.VisModifiers[attr.Token])
				DAttribute.CleanupAccessorAttributes(stk, attr.Token);
			else
				DAttribute.RemoveFromStack(stk, attr.Token);

			LastParsedObject = attr;

			stk.Push(attr);
		}

        void ApplyAttributes(DNode n)
        {
            foreach (var attr in BlockAttributes.ToArray())
                n.Attributes.Add(attr);

            while (DeclarationAttributes.Count > 0)
            {
                var attr = DeclarationAttributes.Pop();

				// If accessor already in attribute array, remove it
				if (DTokens.VisModifiers[attr.Token])
					DAttribute.CleanupAccessorAttributes(n.Attributes);

                if (attr.IsProperty || !DAttribute.ContainsAttribute(n.Attributes.ToArray(),attr.Token))
                    n.Attributes.Add(attr);
            }
        }

		void ApplyAttributes(IStatement n)
		{
			var attributes = new List<DAttribute>();

			foreach (var attr in BlockAttributes.ToArray())
				attributes.Add(attr);

			while (DeclarationAttributes.Count > 0)
			{
				var attr = DeclarationAttributes.Pop();

				// If accessor already in attribute array, remove it
				if (DTokens.VisModifiers[attr.Token])
					DAttribute.CleanupAccessorAttributes(attributes);

				if (attr.IsProperty || !DAttribute.ContainsAttribute(attributes, attr.Token))
					attributes.Add(attr);
			}

			n.Attributes = attributes.Count == 0 ? null : attributes.ToArray();
		}

        void OverPeekBrackets(int OpenBracketKind,bool LAIsOpenBracket = false)
        {
            int CloseBracket = CloseParenthesis;

            if (OpenBracketKind == OpenSquareBracket) 
				CloseBracket = CloseSquareBracket;
            else if (OpenBracketKind == OpenCurlyBrace) 
				CloseBracket = CloseCurlyBrace;

			var pk = Lexer.CurrentPeekToken;
            int i = LAIsOpenBracket?1:0;
            while (pk.Kind != EOF)
            {
                if (pk.Kind== OpenBracketKind)
                    i++;
                else if (pk.Kind== CloseBracket)
                {
                    i--;
                    if (i <= 0) 
					{ 
						Peek(); 
						break; 
					}
                }
                pk = Peek();
            }
        }

        private bool Expect(int n)
        {
			if(n == Identifier)
				ExpectingIdentifier = true;
			if (laKind == n)
			{
				Step();
				if (n == Identifier)
					ExpectingIdentifier = false;
				return true; 
			}
			else
			{
				SynErr(n, DTokens.GetTokenString(n) + " expected, "+DTokens.GetTokenString(laKind)+" found!");
			}
            return false;
        }

        /// <summary>
        /// Retrieve string value of current token
        /// </summary>
        protected string strVal
        {
            get
            {
                if (t.Kind == DTokens.Identifier || t.Kind == DTokens.Literal)
                    return t.Value;
                return DTokens.GetTokenString(t.Kind);
            }
        }

        DToken Peek()
        {
            return Lexer.Peek();
        }

        DToken Peek(int n)
        {
            Lexer.StartPeek();
            DToken x = la;
            while (n > 0)
            {
                x = Lexer.Peek();
                n--;
            }
            return x;
        }

		public void Step()
		{ 
			Lexer.NextToken();

			Lexer.StartPeek();
			Lexer.Peek();
 
			laKind = la.Kind;
		}

        [DebuggerStepThrough()]
        public DModule Parse()
        {
            return Parse(false);
        }

        /// <summary>
        /// Initializes and proceed parse procedure
        /// </summary>
        /// <param name="imports">List of imports in the module</param>
        /// <param name="ParseStructureOnly">If true, all statements and non-declarations are ignored - useful for analysing libraries</param>
        /// <returns>Completely parsed module structure</returns>
        public DModule Parse(bool ParseStructureOnly)
        {
            this.ParseStructureOnly = ParseStructureOnly;
            doc=Root();
			doc.ParseErrors = new System.Collections.ObjectModel.ReadOnlyCollection<ParserError>(ParseErrors);
            return doc;
        }
        
        #region Error handlers
        void SynErr(int n, string msg)
        {
			if (ParseErrors.Count > MaxParseErrorsBeforeFailure)
			{
				Lexer.StopLexing();
				return;
			}
			else if (ParseErrors.Count == MaxParseErrorsBeforeFailure)
				msg = "Too many errors - stop parsing";

			ParseErrors.Add(new ParserError(false,msg,n,t==null?la.Location:t.EndLocation));
        }
        void SynErr(int n)
		{
			SynErr(n, DTokens.GetTokenString(n) + " expected" + (t!=null?(", "+DTokens.GetTokenString(t.Kind)+" found"):""));
        }

        void SemErr(int n, string msg)
        {
			ParseErrors.Add(new ParserError(true, msg, n, t == null ? la.Location : t.EndLocation));
        }
        /*void SemErr(int n)
        {
			ParseErrors.Add(new ParserError(true, DTokens.GetTokenString(n) + " expected" + (t != null ? (", " + DTokens.GetTokenString(t.Kind) + " found") : ""), n, t == null ? la.Location : t.EndLocation));
        }*/
        #endregion
	}

	public class ParserTrackerVariables
	{
		public object PreviousParsedObject { get; protected set; }
		/// <summary>
		/// Used to track the expression/declaration/statement/whatever which is handled currently.
		/// Required for code completion.
		/// </summary>
		public object LastParsedObject { get { return lastParsedObj; } set { PreviousParsedObject = lastParsedObj; lastParsedObj = value; } }
		object lastParsedObj = null;

		public readonly List<Comment> Comments = new List<Comment>();

		/// <summary>
		/// Required for code completion.
		/// True if a type/variable/method/etc. identifier is expected.
		/// </summary>
		public bool ExpectingIdentifier = false;

		public INode InitializedNode=null;
		public bool IsParsingInitializer=false;
	}
}
