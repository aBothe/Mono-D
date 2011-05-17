using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Dom.Parser;
using System.IO;
using MonoDevelop.Projects.Dom;
using MonoDevelop.D.Parser.Lexer;

/*
 * D Parser "Back-End" - Contains the internal parsing procedures
 */

namespace MonoDevelop.D.Parser
{
	public partial class DParser
	{
		DLexer lexer;

#region Lexer helper methods

		void Step()
		{
			lexer.NextToken();
		}

		DToken la{get{return lexer.LookAhead;}}
		DToken t{get{return lexer.CurrentToken;}}

#endregion

		/// <summary>
		/// Modifiers for entire block
		/// </summary>
		Stack<DAttribute> BlockAttributes = new Stack<DAttribute>();
		/// <summary>
		/// Modifiers for current expression only
		/// </summary>
		Stack<DAttribute> DeclarationAttributes = new Stack<DAttribute>();


		#region Modules
        // http://www.digitalmars.com/d/2.0/module.html

        /// <summary>
        /// Module entry point
        /// </summary>
        void Root(ICompilationUnit module)
        {
            Step();

            doc = module;
            // Only one module declaration possible possible!
            if (la.Kind == (Module))
            {
                module.Description = GetComments();
                module.ModuleName = ModuleDeclaration().ToString();
                module.Description += CheckForPostSemicolonComment();
            }
            var _block = module as IBlockNode;
            // Now only declarations or other statements are allowed!
            while (!IsEOF)
            {
                DeclDef(_block);
            }
            module.EndLocation = la.Location;
            return module;
        }

        #region Comments
        string PreviousComment = "";

        string GetComments()
        {
            string ret = "";

            while (lexer.Comments.Count > 0)
            {
                var c = lexer.Comments.Pop();

                foreach (var line in c.CommentText.Split('\n'))
                    ret += line.Trim().TrimStart('*') + "\r\n";
                ret += "\r\n";
            }

            ret = ret.Trim().Trim('*', '+');

            if (String.IsNullOrEmpty(ret)) return "";

            // Overwrite only if comment is not 'ditto'
            if (ret.ToLower() != "ditto")
                PreviousComment=ret;

            return PreviousComment;
        }

        /// <summary>
        /// Returns the pre- and post-declaration comment
        /// </summary>
        /// <returns></returns>
        string CheckForPostSemicolonComment()
        {
            int ExpectedLine = t.line;

            string ret = "";

            while (lexer.Comments.Count > 0 && lexer.Comments.Peek().StartPosition.Line == ExpectedLine)
            {
                var c = lexer.Comments.Pop();

                foreach (var line in c.CommentText.Split('\n'))
                    ret += line.Trim().TrimStart('*') + "\n";
                ret += "\n";
            }

            ret = ret.Trim().Trim('*', '+');

            // Add post-declaration string only if comment is not 'ditto'
            if (ret.ToLower() != "ditto")
            {
                if (!String.IsNullOrEmpty(ret))
                {
                    PreviousComment += "\n" + ret;
                    PreviousComment = PreviousComment.Trim();
                    return ret;
                }
            }
            else 
                return PreviousComment;

            return "";
        }

        void ClearCommentCache()
        {
            lexer.Comments.Clear();
        }
        #endregion

        void DeclDef(IBlockNode module)
        {
            //AttributeSpecifier
			if (IsAttributeSpecifier())
				AttributeSpecifier();

			//ImportDeclaration
			else if (la.Kind == (Import))
				ImportDeclaration();

			//Constructor
			else if (la.Kind == (This))
				module.Add(Constructor(module is DClassLike ? (module as DClassLike).ClassType == DTokens.Struct : false));

			//Destructor
			else if (la.Kind == (Tilde) && lexer.CurrentPeekToken.Kind == (This))
				module.Add(Destructor());

			//Invariant
			else if (la.Kind == (Invariant))
				module.Add(_Invariant());

			//UnitTest
			else if (la.Kind == (Unittest))
			{
				Step();
				var dbs = new DMethod(DMethod.MethodType.Unittest);
				dbs.StartLocation = t.Location;
				FunctionBody(dbs);
				dbs.EndLocation = t.EndLocation;
				module.Add(dbs);
			}

			//ConditionalDeclaration
			else if (la.Kind == (Version) || la.Kind == (Debug) || la.Kind == (If))
			{
				Step();
				var n = t.ToString();

				if (t.Kind == (If))
				{
					Expect(OpenParenthesis);
					AssignExpression();
					Expect(CloseParenthesis);
				}
				else if (la.Kind == (Assign))
				{
					Step();
					Step();
					Expect(Semicolon);
				}
				else if (t.Kind == (Version))
				{
					Expect(OpenParenthesis);
					n += "(";
					Step();
					n += t.ToString();
					Expect(CloseParenthesis);
					n += ")";
				}
				else if (t.Kind == (Debug) && la.Kind == (OpenParenthesis))
				{
					Expect(OpenParenthesis);
					n += "(";
					Step();
					n += t.ToString();
					Expect(CloseParenthesis);
					n += ")";
				}

				if (la.Kind == (Colon))
					Step();
			}

			//TODO
			else if (la.Kind == (Else))
			{
				Step();
			}

			//StaticAssert
			else if (la.Kind == (Assert))
			{
				Step();
				Expect(OpenParenthesis);
				AssignExpression();
				if (la.Kind == (Comma))
				{
					Step();
					AssignExpression();
				}
				Expect(CloseParenthesis);
				Expect(Semicolon);
			}
			//TemplateMixin

			//MixinDeclaration
			else if (la.Kind == (Mixin))
				MixinDeclaration();

			//;
			else if (la.Kind == (Semicolon))
				Step();

			// {
			else if (la.Kind == (OpenCurlyBrace))
			{
				// Due to having a new attribute scope, we'll have use a new attribute stack here
				var AttrBackup = BlockAttributes;
				BlockAttributes = new Stack<DAttribute>();

				while (DeclarationAttributes.Count > 0)
					BlockAttributes.Push(DeclarationAttributes.Pop());

				ClassBody(module);

				// After the block ended, restore the previous block attributes
				BlockAttributes = AttrBackup;
			}

			// Class Allocators
			// Note: Although occuring in global scope, parse it anyway but declare it as semantic nonsense;)
			else if (la.Kind == (New))
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Allocator);
				dm.Name = "new";
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				module.Add(dm);
			}

			// Class Deallocators
			else if (la.Kind == Delete)
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Deallocator);
				dm.Name = "delete";
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				module.Add(dm);
			}

			// else:
			else Declaration(module);
        }

        ITypeDeclaration ModuleDeclaration()
        {
            Expect(Module);
            var ret = ModuleFullyQualifiedName();
            Expect(Semicolon);
            return ret;
        }

        ITypeDeclaration ModuleFullyQualifiedName()
        {
            Expect(Identifier);

			if (la.Kind != Dot)
				return new NormalDeclaration(t.Value);

			var il = new D_Parser.IdentifierList();
			il.Add(t.Value);

            while (la.Kind == Dot)
            {
                Step();
                Expect(Identifier);

                il.Add(t.Value);
            }
            return il;
        }

        void ImportDeclaration()
        {
            bool IsPublic = DAttribute.ContainsAttribute(BlockAttributes,Public) || DAttribute.ContainsAttribute(DeclarationAttributes,Public);
            DeclarationAttributes.Clear();
            CheckForDocComments();
            Expect(Import);

            var imp = _Import();
            if (!doc.ContainsImport(imp)) // Check if import is already done
                doc.Imports.Add(imp, IsPublic);

            // ImportBindings
            if (la.Kind==(Colon))
            {
                Step();
                ImportBind();
                while (la.Kind==(Comma))
                {
                    Step();
                    ImportBind();
                }
            }
            else
                while (la.Kind==(Comma))
                {
                    Step();
                    imp = _Import();
                    if(!doc.ContainsImport(imp)) // Check if import is already done
                        doc.Imports.Add(imp,IsPublic);

                    if (la.Kind == (Colon))
                    {
                        Step();
                        ImportBind();
                        while (la.Kind == (Comma))
                        {
                            Step();
                            ImportBind();
                        }
                    }
                }

            Expect(Semicolon);
        }

        ITypeDeclaration _Import()
        {
            // ModuleAliasIdentifier
            if (lexer.CurrentPeekToken.Kind==(Assign))
            {
                Expect(Identifier);
                string ModuleAliasIdentifier = t.Value;
                Step();
            }

            return ModuleFullyQualifiedName();
        }

        void ImportBind()
        {
            Expect(Identifier);
            string imbBind = t.Value;
            string imbBindDef = null;

            if (la.Kind==(Assign))
            {
                Step();
                Expect(Identifier);
                imbBindDef = t.Value;
            }
        }


        INode MixinDeclaration()
        {
            Expect(Mixin);

            if (LA(OpenParenthesis))
            {
                Step();
                AssignExpression();
                Expect(CloseParenthesis);
            }
            else
            {
                // TemplateMixinDeclaration
                if (LA(Template))
                    return TemplateDeclaration();

                // TemplateMixin
                else if (LA(Identifier))
                {
                    if (PK(Not))
                        TemplateInstance();
                    else
                        Expect(Identifier);

                    // MixinIdentifier
                    if (LA(Identifier))
                        Step();
                }
            }
            Expect(Semicolon);
            return null;
        }
        #endregion

        #region Declarations
        // http://www.digitalmars.com/d/2.0/declaration.html

        bool IsDeclaration()
        {
            return la.Kind==(Alias) || IsStorageClass || IsBasicType();
        }

        bool CheckForStorageClasses()
        {
            bool ret = false;
            while (IsStorageClass || Attributes[la.Kind])
            {
                if (IsAttributeSpecifier()) // extern, align
                    AttributeSpecifier();
                else
                {
                    Step();
                    if (!DAttribute.ContainsAttribute(DeclarationAttributes.ToArray(), t.Kind))
						PushAttribute(new DAttribute(t.Kind),false);
                }
                ret = true;
            }
            return ret;
        }

        bool CheckForModifiers()
        {
            bool ret = false;
            while (Modifiers[la.Kind] || Attributes[la.Kind])
            {
                if (IsAttributeSpecifier()) // extern, align
                    AttributeSpecifier();
                else
                {
                    Step();
                    if (!DAttribute.ContainsAttribute(DeclarationAttributes.ToArray(), t.Kind))
                        PushAttribute(new DAttribute(t.Kind),false);
                }
                ret = true;
            }
            return ret;
        }

        void Declaration(IBlockNode par)
        {
            // Skip ref token
            if (la.Kind == (Ref))
            {
				PushAttribute(new DAttribute(Ref),false);
                Step();
            }
            
            // Enum possible storage class attributes
            bool HasStorageClassModifiers = CheckForStorageClasses();            

            if (la.Kind==(Alias) || la.Kind==Typedef)
            {
                Step();
                // _t is just a synthetic node
                var _t = new DStatementBlock() as IBlockNode;
                ApplyAttributes(_t as DNode);

                // AliasThis
                if (la.Kind == Identifier && PK(This))
                {
                    Step();
                    var dv = new DVariable();
                    dv.Description = GetComments();
                    dv.StartLocation = lexer.LastToken.Location;
                    dv.IsAlias=true;
                    dv.Name = "this";
                    dv.Type = new NormalDeclaration(t.Value);
                    dv.EndLocation = t.EndLocation;
                    par.Add(dv);
                    Step();
                    Expect(Semicolon);
                    dv.Description += CheckForPostSemicolonComment();
                    return;
                }

                Decl(ref _t,HasStorageClassModifiers);
                foreach (var n in _t)
                {
                    if (n is DVariable)
                        (n as DVariable).IsAlias = true;
                }

                par.AddRange(_t);
            }
            else if (la.Kind==(Struct) || la.Kind==(Union))
                par.Add(AggregateDeclaration());
            else if (la.Kind==(Enum))
                EnumDeclaration(ref par);
            else if (la.Kind==(Class))
                par.Add(ClassDeclaration());
            else if (la.Kind==(Template))
                par.Add(TemplateDeclaration());
            else if (la.Kind==(Interface))
                par.Add(InterfaceDeclaration());
            else
                Decl(ref par,HasStorageClassModifiers);
        }

        void Decl(ref IBlockNode par, bool HasStorageClassModifiers)
        {
            var startLocation = la.Location;
            ITypeDeclaration ttd =null;

            CheckForStorageClasses();
            // Skip ref token
            if (la.Kind==(Ref))
            {
                if (!DAttribute.ContainsAttribute(DeclarationAttributes, Ref))
					PushAttribute(new DAttribute(Ref),false);
                Step();
            }

            // Autodeclaration
            var StorageClass = DTokens.ContainsStorageClass(DeclarationAttributes.ToArray());
            
            // If there's no explicit type declaration, leave our node's type empty!
            if ((StorageClass.Token!=DAttribute.Empty.Token && la.Kind==(Identifier) && DeclarationAttributes.Count > 0 &&
                (PK(Assign) || PK(OpenParenthesis)))) // public auto var=0; // const foo(...) {} 
            {
            }
            else 
                ttd= BasicType();

            // Declarators
            var firstNode = Declarator(false);
            firstNode.Description = GetComments();
            firstNode.StartLocation = startLocation;

            if (firstNode.Type == null)
                firstNode.Type = ttd;
            else
                firstNode.Type.MostBasic = ttd;

            ApplyAttributes(firstNode as DNode);

            // Check for declaration constraints
            if (la.Kind == (If))
                Constraint();

            // BasicType Declarators ;
            bool ExpectFunctionBody = !(la.Kind==(Assign) || la.Kind==(Comma) || la.Kind==(Semicolon));
            if (!ExpectFunctionBody)
            {
                // DeclaratorInitializer
                if (la.Kind==(Assign))
                    (firstNode as DVariable).Initializer = Initializer();
                firstNode.EndLocation = t.EndLocation;
                par.Add(firstNode);

                // DeclaratorIdentifierList
                while (la.Kind==(Comma))
                {
                    Step();
                    Expect(Identifier);

                    var otherNode = new DVariable();
                    otherNode.Assign(firstNode);
                    otherNode.StartLocation = t.Location;
                    otherNode.Name = t.Value;

                    if (la.Kind==(Assign))
                        otherNode.Initializer = Initializer();
                    otherNode.EndLocation = t.EndLocation;
                    par.Add(otherNode);
                }

                Expect(Semicolon);
				var pb=(par as IBlockNode);
				if(pb.Count>0)
                pb[pb.Count - 1].Description += CheckForPostSemicolonComment();
            }

            // BasicType Declarator FunctionBody
            else if (firstNode is IBlockNode)
            {
                FunctionBody(firstNode as IBlockNode);
                
                par.Add(firstNode);
            }
            else
            {
                SynErr(OpenCurlyBrace, "Function declaration expected in front of block statement");
            }
        }

        bool IsBasicType()
        {
            return BasicTypes[la.Kind] || la.Kind==(Typeof) || MemberFunctionAttribute[la.Kind] || (la.Kind==(Dot) && lexer.CurrentPeekToken.Kind==(Identifier)) || la.Kind==(Identifier);
        }

        ITypeDeclaration BasicType()
        {
            ITypeDeclaration td = null;
            if (BasicTypes[la.Kind])
            {
                Step();
                return new DTokenDeclaration(t.Kind);
            }

            if (MemberFunctionAttribute[la.Kind])
            {
                Step();
                var md = new MemberFunctionAttributeDecl(t.Kind);
                bool p = false;
                
                if (la.Kind == OpenParenthesis)
                {
                    Step();
                    p = true;
                }

                // e.g. cast(const)
                if(la.Kind!=CloseParenthesis)
                    md.InnerType = Type();

                if (p)
                    Expect(CloseParenthesis);
                return md;
            }

            //TODO
            if (la.Kind == Ref)
                Step();
            
            if (la.Kind==(Typeof))
            {
                td = TypeOf();
                if (la.Kind!=(Dot)) return td;
            }

            if (la.Kind==(Dot))
                Step();

            if (td == null)
                td = IdentifierList() as ITypeDeclaration;
            else
                td.MostBasic = IdentifierList() as ITypeDeclaration;

            return td;
        }

        bool IsBasicType2()
        {
            return la.Kind==(Times) || la.Kind==(OpenSquareBracket) || la.Kind==(Delegate) || la.Kind==(Function);
        }

        ITypeDeclaration BasicType2()
        {
            // *
            if (la.Kind==(Times))
            {
                Step();
                return new PointerDecl();
            }

            // [ ... ]
            else if (la.Kind==(OpenSquareBracket))
            {
                Step();
                // [ ]
                if (la.Kind==(CloseSquareBracket)) { Step(); return new ClampDecl(); }

                var cd = new ClampDecl();

                // [ Type ]
                if (!IsAssignExpression())
                    cd.KeyType = Type();
                else
                {
                    var fromExpression=AssignExpression();
                    
                    // [ AssignExpression .. AssignExpression ]
                    if (la.Kind==(DoubleDot))
                    {
                        Step();
                        var from_to_Expression = new AssignTokenExpression(DoubleDot);
                        from_to_Expression.PrevExpression = fromExpression;

                        from_to_Expression.FollowingExpression =AssignExpression();
                        cd.KeyType = new DExpressionDecl(from_to_Expression);
                    }
                    else
                        cd.KeyType = new DExpressionDecl(fromExpression);
                }

                Expect(CloseSquareBracket);
                return cd;
            }

            // delegate | function
            else if (la.Kind==(Delegate) || la.Kind==(Function))
            {
                Step();
                ITypeDeclaration td = null;
                var dd = new DelegateDeclaration();
                dd.IsFunction = t.Kind == Function;

                dd.Parameters = Parameters(null);
                td = dd;
                //TODO: add attributes to declaration
                while (FunctionAttribute[la.Kind])
                {
                    Step();
                    td = new DTokenDeclaration(t.Kind, td);
                }
                return td;
            }
            else
                SynErr(Identifier);
            return null;
        }

        /// <summary>
        /// Parses a type declarator
        /// </summary>
        /// <returns>A dummy node that contains the return type, the variable name and possible parameters of a function declaration</returns>
        INode Declarator(bool IsParam)
        {
            INode ret = new DVariable();
            ITypeDeclaration ttd = null;

            while (IsBasicType2())
            {
                if (ret.Type == null) ret.Type = BasicType2();
                else { ttd = BasicType2(); ttd.Base = ret.Type; ret.Type = ttd; }
            }
            /*
             * Add some syntax possibilities here
             * like
             * int (x);
             * int(*foo);
             */
            if (la.Kind==(OpenParenthesis))
            {
                Step();
                var cd = new ClampDecl(ret.Type, ClampDecl.ClampType.Round);
                ret.Type = cd;

                /* 
                 * Parse all basictype2's that are following the initial '('
                 */
                while (IsBasicType2())
                {
                    ttd = BasicType2();

                    if (cd.KeyType == null) cd.KeyType = ttd;
                    else
                    {
                        ttd.Base = cd.KeyType;
                        cd.KeyType = ttd;
                    }
                }

                /*
                 * Here can be an identifier with some optional DeclaratorSuffixes
                 */
                if (la.Kind!=(CloseParenthesis))
                {
                    if (IsParam && la.Kind!=(Identifier))
                    {
                        /* If this Declarator is a parameter of a function, don't expect anything here
                         * exept a '*' that means that here's an anonymous function pointer
                         */
                        if (t.Kind!=(Times))
                            SynErr(Times);
                    }
                    else
                    {
                        Expect(Identifier);
                        ret.Name = t.Value;

                        /*
                         * Just here suffixes can follow!
                         */
                        if (la.Kind!=(CloseParenthesis))
                        {
							INode[] _unused2 = null;
                            List<INode> _unused = null;
                            ttd = DeclaratorSuffixes(out _unused2, out _unused);

                            if (cd.KeyType == null) cd.KeyType = ttd;
                            else
                            {
                                ttd.Base = cd.KeyType;
                                cd.KeyType = ttd;
                            }
                        }
                    }
                }
                ret.Type = cd;
                Expect(CloseParenthesis);
            }
            else
            {
                if (IsParam && la.Kind!=(Identifier))
                    return ret;

                Expect(Identifier);
                ret.Name = t.Value;
            }

            if (IsDeclaratorSuffix)
            {
                // DeclaratorSuffixes
                List<INode> _Parameters;
                ttd = DeclaratorSuffixes(out (ret as DNode).TemplateParameters, out _Parameters);
                if (ttd != null)
                {
                    ttd.Base = ret.Type;
                    ret.Type = ttd;
                }

                if (_Parameters != null)
                {
                    var dm = new DMethod();
                    dm.Assign(ret);
                    dm.Parameters = _Parameters;
					foreach (var pp in dm.Parameters)
						pp.Parent = dm;
                    ret = dm;
                }
            }

            return ret;
        }

        bool IsDeclaratorSuffix
        {
            get { return la.Kind==(OpenSquareBracket) || la.Kind==(OpenParenthesis); }
        }

        /// <summary>
        /// Note:
        /// http://www.digitalmars.com/d/2.0/declaration.html#DeclaratorSuffix
        /// The definition of a sequence of declarator suffixes is buggy here! Theoretically template parameters can be declared without a surrounding ( and )!
        /// Also, more than one parameter sequences are possible!
        /// 
        /// TemplateParameterList[opt] Parameters MemberFunctionAttributes[opt]
        /// </summary>
        ITypeDeclaration DeclaratorSuffixes(out INode[] TemplateParameters, out List<INode> _Parameters)
        {
            ITypeDeclaration td = null;
            TemplateParameters = null;
            _Parameters = null;

            while (la.Kind==(OpenSquareBracket))
            {
                Step();
                var ad = new ClampDecl(td);
                if (la.Kind!=(CloseSquareBracket))
                {
                    if (IsAssignExpression())
                        ad.KeyType = new DExpressionDecl(AssignExpression());
                    else
                        ad.KeyType = Type();
                }
                Expect(CloseSquareBracket);
                ad.ValueType = td;
                td = ad;
            }

            if (la.Kind==(OpenParenthesis))
            {
                if (IsTemplateParameterList())
                {
                    TemplateParameters = TemplateParameterList();
                }
                _Parameters = Parameters(null);

                //TODO: MemberFunctionAttributes -- add them to the declaration
                while (StorageClass[la.Kind] || Attributes[la.Kind])
                {
                    Step();
                }
            }
            return td;
        }

        ITypeDeclaration IdentifierList()
        {
            ITypeDeclaration td = null;

            if (la.Kind!=(Identifier))
                SynErr(Identifier);

            // Template instancing
            if (lexer.CurrentPeekToken.Kind==(Not))
                td=TemplateInstance();

            // Identifier
            else
            {
                Step();
                td = new NormalDeclaration(t.Value);
            }

            // If we have only one identifier, return immediately
            if (la.Kind != Dot)
                return td;

            // Otherwise go on with parsing an identifierlist declaration
            var ret = new IdentifierList();

            // Important: Add the already parsed identifier/template decl
            ret.Add(td);

            while (la.Kind==(Dot))
            {
                Step();
                // Template instancing
                if (lexer.CurrentPeekToken.Kind==(Not))
                    ret.Add(TemplateInstance());
                // Identifier
                else
                {
                    Expect(Identifier);
                    ret.Add(t.Value);
                }
            }
            return ret;
        }

        bool IsStorageClass
        {
            get
            {
                return la.Kind==(Abstract) ||
            la.Kind==(Auto) ||
            ((MemberFunctionAttribute[la.Kind]) && lexer.CurrentPeekToken.Kind!=(OpenParenthesis)) ||
            la.Kind==(Deprecated) ||
            la.Kind==(Extern) ||
            la.Kind==(Final) ||
            la.Kind==(Override) ||
            la.Kind==(Scope) ||
            la.Kind==(Static) ||
            la.Kind==(Synchronized) ||
            la.Kind==__gshared||
            la.Kind==__thread;
            }
        }

        ITypeDeclaration Type()
        {
            var td = BasicType();

            if (IsDeclarator2())
            {
                var ttd = Declarator2();
                if (ttd != null)
                {
                    ttd.Base = td;
                    td = ttd;
                }
            }

            return td;
        }

        bool IsDeclarator2()
        {
            return IsBasicType2() || la.Kind==(OpenParenthesis);
        }

        /// <summary>
        /// http://www.digitalmars.com/d/2.0/declaration.html#Declarator2
        /// The next bug: Following the definition strictly, this function would end up in an endless loop of requesting another Declarator2
        /// 
        /// So here I think that a Declarator2 only consists of a couple of BasicType2's and some DeclaratorSuffixes
        /// </summary>
        /// <returns></returns>
        ITypeDeclaration Declarator2()
        {
            ITypeDeclaration td = null;
            if (la.Kind==(OpenParenthesis))
            {
                Step();

                td = Declarator2();
                Expect(CloseParenthesis);

                // DeclaratorSuffixes
                if (la.Kind==(OpenSquareBracket))
                {
					List<INode> _unused = null;
					INode[] _unused2 = null;
                    DeclaratorSuffixes(out _unused2, out _unused);
                }
                return td;
            }

            while (IsBasicType2())
            {
                var ttd = BasicType2();
                ttd.Base = td;
                td = ttd;
            }

            return td;
        }

        /// <summary>
        /// Parse parameters
        /// </summary>
        List<INode> Parameters(IBlockNode Parent)
        {
            var ret = new List<INode>();
            Expect(OpenParenthesis);

            // Empty parameter list
            if (la.Kind==(CloseParenthesis))
            {
                Step();
                return ret;
            }

            if (la.Kind!=TripleDot)
                ret.Add(Parameter());

            while (la.Kind==(Comma))
            {
                Step();
                if (la.Kind == TripleDot)
                    break;
				var p = Parameter();
				p.Parent = p;
                ret.Add(p);
            }

            /*
             * There can be only one '...' in every parameter list
             */
            if (la.Kind == TripleDot)
            {
                // If it had not a comma, add a VarArgDecl to the last parameter
                bool HadComma = t.Kind==(Comma);

                Step();

                if (!HadComma && ret.Count > 0 && ret is IBlockNode)
                {
					((ret as IBlockNode)[(ret as IBlockNode).Count - 1] as IBlockNode).Type = new VarArgDecl((ret as IBlockNode)[(ret as IBlockNode).Count - 1].Type);
                }
                else
                {
                    var dv = new DVariable();
					dv.Parent = Parent;
                    dv.Type = new VarArgDecl();
                    ret.Add(dv);
                }
            }

            Expect(CloseParenthesis);
            return ret;
        }

        private INode Parameter()
        {
            var attr = new List<DAttribute>();
            var startLocation = la.Location;

            while (ParamModifiers[la.Kind] ||( MemberFunctionAttribute[la.Kind] && !PK(OpenParenthesis)))
            {
                Step();
                attr.Add(new DAttribute(t.Kind));
            }

            if (la.Kind == Auto && lexer.CurrentPeekToken.Kind == Ref) // functional.d:595 // auto ref F fp
            {
                Step();
                Step();
                attr.Add(new DAttribute( Auto));
                attr.Add(new DAttribute( Ref));
            }

            var td = BasicType();

            var ret = Declarator(true);
            ret.StartLocation = startLocation;
            if (attr.Count > 0) (ret as DNode).Attributes.AddRange(attr);
            if (ret.Type == null)
                ret.Type = td;
            else
                ret.Type.Base = td;

            // DefaultInitializerExpression
            if (la.Kind==(Assign))
            {
                Step();
                DExpression defInit = null;
                if (la.Kind==(Identifier) && (la.Value == "__FILE__" || la.Value == "__LINE__"))
                    defInit = new IdentExpression(la.Value);
                else
                    defInit = AssignExpression();

                if (ret is DVariable)
                    (ret as DVariable).Initializer = defInit;
            }
            ret.EndLocation = t.EndLocation;

            return ret;
        }

        private DExpression Initializer()
        {
            Expect(Assign);

            // VoidInitializer
            if (la.Kind==(Void))
            {
                Step();
                return new TokenExpression(Void);
            }

            return NonVoidInitializer();
        }

        DExpression NonVoidInitializer()
        {
            // ArrayInitializer | StructInitializer
            if (la.Kind==(OpenSquareBracket) || la.Kind==(OpenCurlyBrace))
            {
                Step();
                bool IsStructInit = t.Kind==(OpenCurlyBrace);
                if (IsStructInit ? la.Kind==(CloseCurlyBrace) : la.Kind==(CloseSquareBracket))
                {
                    Step();
                    return new ClampExpression(IsStructInit ? ClampExpression.ClampType.Curly : ClampExpression.ClampType.Square);
                }

                // ArrayMemberInitializations
                var ae = new ArrayExpression(IsStructInit ? ClampExpression.ClampType.Curly : ClampExpression.ClampType.Square);
                DExpression element = null;

                bool IsInit = true;
                while (IsInit || la.Kind==(Comma))
                {
                    if (!IsInit) Step();
                    IsInit = false;

                    // Allow empty post-comma expression IF the following token finishes the initializer expression
                    // int[] a=[1,2,3,4,];
                    if (la.Kind == (IsStructInit ? CloseCurlyBrace : CloseSquareBracket))
                        break;

                    if (IsStructInit)
                    {
                        // Identifier : NonVoidInitializer
                        if (la.Kind==(Identifier) && lexer.CurrentPeekToken.Kind==(Colon))
                        {
                            Step();
                            var inh = new AssignTokenExpression(Colon);
                            inh.PrevExpression = new IdentExpression(t.Value);
                            Step();
                            inh.FollowingExpression = NonVoidInitializer();
                            element = inh;
                        }
                        else
                            element = NonVoidInitializer();
                    }
                    else
                    {
                        // ArrayMemberInitialization
                        element = NonVoidInitializer();
                        bool HasBeenAssExpr = !(t.Kind==(CloseSquareBracket) || t.Kind==(CloseCurlyBrace));

                        // AssignExpression : NonVoidInitializer
                        if (HasBeenAssExpr && la.Kind==(Colon))
                        {
                            Step();
                            var inhExpr = new AssignTokenExpression(Colon);
                            inhExpr.PrevExpression = element;
                            inhExpr.FollowingExpression = NonVoidInitializer();
                            element = inhExpr;
                        }
                    }

                    ae.Expressions.Add(element);
                }

                Expect(IsStructInit? CloseCurlyBrace:CloseSquareBracket);

                // auto i=[1,2,3].idup;
                if (la.Kind == Dot)
                {
                    Step();
                    var ae2 = new AssignTokenExpression(t.Kind);
                    ae2.PrevExpression = ae;
                    ae2.FollowingExpression = AssignExpression();
                    return ae2;
                }

                return ae;
            }
            else
                return AssignExpression();
        }

        ITypeDeclaration TypeOf()
        {
            Expect(Typeof);
            Expect(OpenParenthesis);
            var md = new MemberFunctionAttributeDecl(Typeof);
            if (la.Kind == (Return))
            {
                Step();
                md.InnerType = new DTokenDeclaration(Return);
            }
            else
                md.InnerType = new DExpressionDecl(Expression());
            Expect(CloseParenthesis);
            return md;
        }

        #endregion

        #region Attributes

        IBlockNode _Invariant()
        {
            IBlockNode inv = new DMethod();
            inv.Name = "invariant";

            Expect(Invariant);
            Expect(OpenParenthesis);
            Expect(CloseParenthesis);
            BlockStatement(ref inv);
            return inv;
        }

        void _Pragma()
        {
            Expect(Pragma);
            Expect(OpenParenthesis);
            Expect(Identifier);

            if (la.Kind==(Comma))
            {
                Step();
                ArgumentList();
            }
            Expect(CloseParenthesis);
        }

        bool IsAttributeSpecifier()
        {
            return (la.Kind==(Extern) || la.Kind==(Export) || la.Kind==(Align) || la.Kind==Pragma || la.Kind==(Deprecated) || IsProtectionAttribute()
                || la.Kind==(Static) || la.Kind==(Final) || la.Kind==(Override) || la.Kind==(Abstract) || la.Kind==(Scope) || la.Kind==(__gshared)
                || ((la.Kind==(Auto) || MemberFunctionAttribute[la.Kind]) && (lexer.CurrentPeekToken.Kind!=(OpenParenthesis) && lexer.CurrentPeekToken.Kind!=(Identifier)))
                || Attributes[la.Kind]);
        }

        bool IsProtectionAttribute()
        {
            return la.Kind==(Public) || la.Kind==(Private) || la.Kind==(Protected) || la.Kind==(Extern) || la.Kind==(Package);
        }

        private void AttributeSpecifier()
        {
            var attr = new DAttribute(la.Kind);
            if (la.Kind==(Extern) && lexer.CurrentPeekToken.Kind==(OpenParenthesis))
            {
                Step(); // Skip extern
                Step(); // Skip (
                while (!IsEOF && la.Kind!=(CloseParenthesis))
                    Step();
                Expect(CloseParenthesis);
            }
            else if (la.Kind==(Align) && lexer.CurrentPeekToken.Kind==(OpenParenthesis))
            {
                Step();
                Step();
                Expect(Literal);
                Expect(CloseParenthesis);
            }
            else if (la.Kind==(Pragma))
                _Pragma();
            else
                Step();

			if (la.Kind == (Colon))
			{
				PushAttribute(attr, true);
				Step();
			}

			else if(la.Kind!=Semicolon)
				PushAttribute(attr, false);
        }
        #endregion

        #region Expressions
        DExpression Expression()
        {
            // AssignExpression
            DExpression ass = AssignExpression();
            if (la.Kind!=(Comma))
                return ass;

            /*
             * The following is a leftover of C syntax and proably cause some errors when parsing arguments etc.
             */
            // AssignExpression , Expression
            ArrayExpression ae = new ArrayExpression(ClampExpression.ClampType.Round);
            ae.Expressions.Add(ass);
            while (la.Kind==(Comma))
            {
                Step();
                ae.Expressions.Add(AssignExpression());
            }
            return ae;
        }

        /// <summary>
        /// This function has a very high importance because here we decide whether it's a declaration or assignExpression!
        /// </summary>
        bool IsAssignExpression()
        {
            if (IsBasicType())
            {
                bool HadPointerDeclaration = false;

                // uint[]** MyArray;
                if (!BasicTypes[la.Kind])
                {
                    // Skip initial dot
                    if (la.Kind == Dot)
                        Step();

                    if (lexer.CurrentPeekToken.Kind != Identifier)
                    {
                        if (la.Kind == Identifier)
                        {
                            // Skip initial identifier list
                            bool init = true;
                            bool HadTemplateInst = false;
                            while (init || lexer.CurrentPeekToken.Kind == (Dot))
                            {
                                HadTemplateInst = false;
                                if (lexer.CurrentPeekToken.Kind ==Dot) Peek();
                                init = false;

                                if (lexer.CurrentPeekToken.Kind == Identifier)
                                    Peek();

                                if (lexer.CurrentPeekToken.Kind == (Not))
                                {
                                    HadTemplateInst = true;
                                    Peek();
                                    if (lexer.CurrentPeekToken.Kind != (Is) && lexer.CurrentPeekToken.Kind != (In))
                                    {
                                        if (lexer.CurrentPeekToken.Kind == (OpenParenthesis))
                                            OverPeekBrackets(OpenParenthesis);
                                        else Peek();
                                    }
                                }
                            }
                            //if (!init && !HadTemplateInst) Peek();
                        }
                        else if (la.Kind == (Typeof) || MemberFunctionAttribute[la.Kind])
                        {
                            if (lexer.CurrentPeekToken.Kind == (OpenParenthesis))
                                OverPeekBrackets(OpenParenthesis);
                        }
                    }
                }

                // Skip basictype2's
                while (lexer.CurrentPeekToken.Kind==(Times) || lexer.CurrentPeekToken.Kind==(OpenSquareBracket))
                {
                    if (PK(Times))
                        HadPointerDeclaration = true;

                    if (lexer.CurrentPeekToken.Kind==(OpenSquareBracket))
                        OverPeekBrackets(OpenSquareBracket);
                    else Peek();

                    if (HadPointerDeclaration && PK(Literal)) // char[a.member*8] abc; // conv.d:3278
                    {
                        Peek(1);
                        return true;
                    }
                }

                // And now, after having skipped the basictype and possible trailing basictype2's,
                // we check for an identifier or delegate declaration to ensure that there's a declaration and not an expression
                // Addition: If a times token ('*') follows an identifier list, we can assume that we have a declaration and NOT an expression!
                // Example: *a=b is an expression; a*=b is not possible - instead something like A* a should be taken...
                if (HadPointerDeclaration || lexer.CurrentPeekToken.Kind==(Identifier) || lexer.CurrentPeekToken.Kind==(Delegate) || lexer.CurrentPeekToken.Kind==(Function))
                {
                    Peek(1);
                    return false;
                }
            }
            else if (IsStorageClass)
                return false;

            Peek(1);
            return true;
        }

        DExpression AssignExpression()
        {
            var left = ConditionalExpression();
            if (!AssignOps[la.Kind])
                return left;

            Step();
            var ate = new AssignTokenExpression(t.Kind);
            ate.PrevExpression = left;
            ate.FollowingExpression = AssignExpression();
            return ate;
        }

        DExpression ConditionalExpression()
        {
            var trigger = OrOrExpression();
            if (la.Kind!=(Question))
                return trigger;

            Expect(Question);
            var se = new SwitchExpression(trigger);
            se.TrueCase = AssignExpression();
            Expect(Colon);
            se.FalseCase = ConditionalExpression();
            return se;
        }

        DExpression OrOrExpression()
        {
            var left = CmpExpression();
            if (!(la.Kind==(LogicalOr) || la.Kind==(LogicalAnd) || la.Kind==(BitwiseOr) || la.Kind==(BitwiseAnd) || la.Kind==(Xor)))
                return left;

            Step();
            var ae = new AssignTokenExpression(t.Kind);
            ae.PrevExpression = left;
            ae.FollowingExpression = OrOrExpression();
            return ae;
        }

        bool IsCmpExression
        {
            get
            {
                return 
                    // RelExpression
                RelationalOperators[la.Kind] ||
                    // EqualExpression
                la.Kind == (Equal) || la.Kind == (NotEqual) ||
                    // IdentityExpression | InExpression
                la.Kind == (Is) || la.Kind == (In) || (la.Kind == (Not) && (lexer.CurrentPeekToken.Kind == (Is) || lexer.CurrentPeekToken.Kind == In)) ||
                    // ShiftExpression
                la.Kind == (ShiftLeft) || la.Kind == (ShiftRight) || la.Kind == (ShiftRightUnsigned);
            }
        }

        DExpression CmpExpression()
        {
            var left = AddExpression();

            bool IsShift=la.Kind==(ShiftLeft) || la.Kind==(ShiftRight) || la.Kind==(ShiftRightUnsigned);
            bool CanProceed =
                // RelExpression
                RelationalOperators[la.Kind] ||
                // EqualExpression
                la.Kind==(Equal) || la.Kind==(NotEqual) ||
                // IdentityExpression | InExpression
                la.Kind==(Is) || la.Kind==(In) || (la.Kind==(Not) && (lexer.CurrentPeekToken.Kind==(Is) || lexer.CurrentPeekToken.Kind == In)) ||
                // ShiftExpression
                IsShift;

            if (!CanProceed)
                return left;

            // If we have a !in or !is
            if (la.Kind==(Not)) Step();
            Step();
            var ae = new AssignTokenExpression(t.Kind);
            ae.PrevExpression = left;
            // When a shift expression occurs, an AddExpression is required to follow
            if (IsShift)
            {
                ae.FollowingExpression = AddExpression();
                // A Shift expression can be followed by 1) (Not)Equal expr or 2) Relational expr or 3) is/!is or 4) in/!in
                if (la.Kind == Equal || la.Kind == NotEqual || 
                    RelationalOperators[la.Kind] ||
                    (la.Kind == Not && lexer.CurrentPeekToken.Kind == In) || la.Kind == In ||
                    (la.Kind == Not && lexer.CurrentPeekToken.Kind == Is) || la.Kind == Is)
                {
                    Step();
                    if (t.Kind == Not)
                        Step();
                    var ae2 = new AssignTokenExpression(t.Kind);
                    ae2.PrevExpression = ae;
                    ae2.FollowingExpression = CmpExpression();
                    return ae2;
                }
            }
            else
                ae.FollowingExpression = OrOrExpression();
            return ae;
        }

        private DExpression AddExpression()
        {
            var left = MulExpression();

            if (!(la.Kind==(Plus) || la.Kind==(Minus) || la.Kind==(Tilde)))
                return left;

            Step();
            var ae = new AssignTokenExpression(t.Kind);
            ae.PrevExpression = left;
            ae.FollowingExpression = AddExpression();
            return ae;
        }

        DExpression MulExpression()
        {
            var left = PowExpression();

            if (!(la.Kind==(Times) || la.Kind==(Div) || la.Kind==(Mod)))
                return left;

            Step();
            var ae = new AssignTokenExpression(t.Kind);
            ae.PrevExpression = left;
            if (la.Kind != CloseParenthesis) // file.d:222 // (SECURITY_ATTRIBUTES*).init // Skip the multiplication expression if there's a trailing ')' after the *
                ae.FollowingExpression = MulExpression();
            return ae;
        }

        DExpression PowExpression()
        {
            var left = UnaryExpression();

            if (!(la.Kind==(Pow)))
                return left;

            Step();
            var ae = new AssignTokenExpression(t.Kind);
            ae.PrevExpression = left;
            ae.FollowingExpression = PowExpression();
            return ae;
        }

        DExpression UnaryExpression()
        {
            // CastExpression
            if (la.Kind==(Cast))
            {
                Step();
                Expect(OpenParenthesis);
                ITypeDeclaration castType=null;
                if(la.Kind!=CloseParenthesis) // Yes, it is possible that a cast() can contain an empty type!
                    castType = Type();
                Expect(CloseParenthesis);

                var ex = UnaryExpression();
                var ce = new ClampExpression(new TokenExpression(Cast), ClampExpression.ClampType.Round);
                if(castType!=null) 
                    ce.InnerExpression = new TypeDeclarationExpression(castType);
                ex.Base = ce;
                return ex;
            }

            if (la.Kind==(BitwiseAnd) || la.Kind==(Increment) || la.Kind==(Decrement) || la.Kind==(Times) || la.Kind==(Minus) || la.Kind==(Plus) ||
                la.Kind==(Not) || la.Kind==(Tilde))
            {
                Step();
                var ae = new AssignTokenExpression(t.Kind);
                ae.FollowingExpression = UnaryExpression();
                return ae;
            }

            // NewExpression
            if (la.Kind==(New))
                return NewExpression();

            // DeleteExpression
            if (la.Kind==(Delete))
            {
                Step();
                DExpression ex = UnaryExpression();
                ex.Base = new TokenExpression(Delete);
                return ex;
            }

            return PostfixExpression();
        }

        DExpression NewExpression()
        {
            Expect(New);
            var ex = new InitializerExpression(null);

            // NewArguments
            if (la.Kind==(OpenParenthesis))
            {
                Step();
                if (la.Kind!=(CloseParenthesis))
                    ex.NewArguments = ArgumentList().ToArray();
                Expect(CloseParenthesis);
            }

            /*
             * If there occurs a class keyword here, interpretate it as an anonymous class definition
             * http://digitalmars.com/d/2.0/expression.html#NewExpression
             * 
             * NewArguments ClassArguments BaseClasslist_opt { DeclDefs } 
             * 
             * http://digitalmars.com/d/2.0/class.html#anonymous
             * 
                NewAnonClassExpression:
                    new PerenArgumentListopt class PerenArgumentList_opt SuperClass_opt InterfaceClasses_opt ClassBody

                PerenArgumentList:
                    (ArgumentList)
             * 
             */
            if (la.Kind==(Class))
            {
                Step();
                DExpression ex2 = new TokenExpression(Class);
                ex2.Base = ex;
                ex.Initializer = ex2;

                // ClassArguments
                if (la.Kind==(OpenParenthesis))
                {
                    if (la.Kind==(CloseParenthesis))
                        Step();
                    else
                    {
                        var ae = new ArrayExpression(ClampExpression.ClampType.Round);
                        ae.Base = ex2;
                        ae.Expressions = ArgumentList();
                        ex.Initializer = ae;
                    }
                }

                // BaseClasslist_opt
                if (la.Kind==(Colon))
                    //TODO : Add base classes to expression
                    BaseClassList();
                // SuperClass_opt InterfaceClasses_opt
                else if (la.Kind != OpenCurlyBrace)
                    BaseClassList(false);

                //TODO: Add the parsed results to node tree somehow
                var _block = new DClassLike() as IBlockNode;
                ClassBody(_block);

                return ex;
            }

            // NewArguments Type
            else
            {
                DExpression InitExpr = new TypeDeclarationExpression(BasicType());

                while (la.Kind==(OpenSquareBracket))
                {
                    Step();
                    var ce = new ClampExpression();
                    ce.Base = InitExpr;
                    if(la.Kind!=CloseSquareBracket)
                        ce.InnerExpression = AssignExpression();
                    InitExpr = ce;
                    Expect(CloseSquareBracket);
                }
                
                if (la.Kind==(OpenParenthesis))
                {
                    Step();
                    if (la.Kind != CloseParenthesis)
                    {
                        var ae = new ArrayExpression(ClampExpression.ClampType.Round);
                        ae.Base = InitExpr;
                        if (la.Kind != (CloseParenthesis))
                            ae.Expressions = ArgumentList();
                        InitExpr = ae;
                    }
                    Expect(CloseParenthesis);
                }

                ex.Initializer = InitExpr;
            }
            return ex;
        }

        List<DExpression> ArgumentList()
        {
            var ret = new List<DExpression>();

            ret.Add(AssignExpression());

            while (la.Kind==(Comma))
            {
                Step();
                ret.Add(AssignExpression());
            }

            return ret;
        }

        DExpression PostfixExpression()
        {
            // PostfixExpression
            var retEx = PrimaryExpression();

            /*
             * A postfixexpression must start with a primaryexpression and can 
             * consist of more than one additional epxression --
             * things like foo()[1] become possible then
             */
            while (la.Kind == (Dot) || la.Kind == (Increment) || la.Kind == (Decrement) || la.Kind == (OpenParenthesis) || la.Kind == (OpenSquareBracket) ||
                    la.Kind == Function || la.Kind == Delegate)
            {
                // Function call
                if (la.Kind==(OpenParenthesis))
                {
                    Step();
                    var ae = new ArrayExpression(ClampExpression.ClampType.Round);
                    ae.Base = retEx;
                    if (la.Kind!=(CloseParenthesis))
                        ae.Expressions = ArgumentList();
                    Step();

                    retEx = ae;
                }

                // int function()
                else if (la.Kind==Function || la.Kind==Delegate)
                {
                    Step();
                    var de = new FunctionLiteral(t.Kind);
                    de.Base = retEx;
                    de.AnonymousMethod.Parameters = Parameters(de.AnonymousMethod);
                    retEx = de;
                }

                // IndexExpression | SliceExpression
                else if (la.Kind == (OpenSquareBracket))
                {
                    Step();

                    if (la.Kind != (CloseSquareBracket))
                    {
                        var firstEx = AssignExpression();
                        // [ AssignExpression .. AssignExpression ]
                        if (la.Kind == DoubleDot)
                        {
                            Step();
                            var from_to_expr = new AssignTokenExpression(DoubleDot);
                            from_to_expr.PrevExpression = firstEx;
                            from_to_expr.FollowingExpression = AssignExpression();
                            retEx = from_to_expr;
                        }
                        // [ ArgumentList ]
                        else if (la.Kind == (Comma))
                        {
                            var ae = new ArrayExpression();
                            ae.Expressions.Add(firstEx);
                            while (la.Kind == (Comma))
                            {
                                Step();
                                ae.Expressions.Add(AssignExpression());
                            }
                        }
                        else if (la.Kind != (CloseSquareBracket))
                            SynErr(CloseSquareBracket);
                    }

                    Expect(CloseSquareBracket);
                }

                else if (la.Kind == (Dot))
                {
                    Step();
                    AssignTokenExpression ae = new AssignTokenExpression(Dot);
                    ae.PrevExpression = retEx;
                    if (la.Kind == (New))
                        ae.FollowingExpression = NewExpression();
                    else if (la.Kind == (Identifier))
                        ae.FollowingExpression = PrimaryExpression();
                    else
                        SynErr(Identifier, "Identifier or new expected");

                    retEx = ae;
                }
                else if (la.Kind == (Increment) || la.Kind == (Decrement))
                {
                    Step();
                    DExpression ex2 = new TokenExpression(t.Kind);
                    ex2.Base = retEx;
                    retEx = ex2;
                }
            }

            return retEx;
        }

        DExpression PrimaryExpression()
        {
            if (la.Kind == Dot)
                Step();

            if (la.Kind == __FILE__ || la.Kind == __LINE__)
            {
                Step();
                return new IdentExpression(t.Kind==__FILE__? doc.FileName:(object)t.line);
            }

            // Dollar (== Array length expression)
            if (la.Kind == Dollar)
            {
                Step();
                return new TokenExpression(la.Kind);
            }

            // TemplateInstance
            if (la.Kind==(Identifier) && lexer.CurrentPeekToken.Kind==(Not) && (Peek().Kind!=Is && lexer.CurrentPeekToken.Kind!=In) /* Very important: The 'template' could be a '!is' expression - With two tokens! */)
                return new TypeDeclarationExpression(TemplateInstance());

            // Identifier
            if (la.Kind==(Identifier))
            {
                Step();
                return new IdentExpression(t.Value);
            }

            // SpecialTokens (this,super,null,true,false,$)
            if (la.Kind==(This) || la.Kind==(Super) || la.Kind==(Null) || la.Kind==(True) || la.Kind==(False) || la.Kind==(Dollar))
            {
                Step();
                return new TokenExpression(t.Kind);
            }

            #region Literal
            if ((la.Kind==Minus && lexer.CurrentPeekToken.Kind==Literal) || la.Kind==(Literal))
            {
                bool IsMinus = false;
                if (la.Kind == Minus)
                {
                    IsMinus = true;
                    Step();
                }

                Step();
                              

                // Concatenate multiple string literals here
                if (t.LiteralFormat == LiteralFormat.StringLiteral || t.LiteralFormat==LiteralFormat.VerbatimStringLiteral)
                {
                    string a = t.Value;
                    while (la.LiteralFormat == LiteralFormat.StringLiteral || la.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
                    {
                        Step();
                        a += la.Value;
                    }
                    return new IdentExpression(a);
                }
                if (t.LiteralFormat == LiteralFormat.CharLiteral)
                    return new IdentExpression(t.Value);
                return new IdentExpression(Convert.ToDouble( t.literalValue)*(IsMinus?-1:1));
            }
            #endregion

            #region ArrayLiteral | AssocArrayLiteral
            if (la.Kind==(OpenSquareBracket))
            {
                Step();
                var arre = new ArrayExpression();

                if (LA(CloseSquareBracket)) { Step(); return arre; }

                DExpression firstCondExpr = ConditionalExpression();
                // Can be an associative array only
                if (la.Kind==(Colon))
                {
                    Step();
                    AssignTokenExpression ae = new AssignTokenExpression(Colon);
                    ae.PrevExpression = firstCondExpr;
                    ae.FollowingExpression = ConditionalExpression();
                    arre.Expressions.Add(ae);

                    while (la.Kind==(Comma))
                    {
                        Step();
                        ae = new AssignTokenExpression(Colon);
                        ae.PrevExpression = ConditionalExpression();
                        Expect(Colon);
                        ae.FollowingExpression = ConditionalExpression();
                        arre.Expressions.Add(ae);
                    }
                }
                else
                {
                    if (AssignOps[la.Kind])
                    {
                        Step();
                        AssignTokenExpression ae = new AssignTokenExpression(t.Kind);
                        ae.PrevExpression = firstCondExpr;
                        ae.FollowingExpression = AssignExpression();
                        arre.Expressions.Add(ae);
                    }

                    while (la.Kind==(Comma))
                    {
                        Step();
                        arre.Expressions.Add(AssignExpression());
                    }
                }

                Expect(CloseSquareBracket);
                return arre;
            }
            #endregion

            #region FunctionLiteral
            if (la.Kind==Delegate || la.Kind==Function|| la.Kind == OpenCurlyBrace || (la.Kind==OpenParenthesis && IsFunctionLiteral()))
            {
                var fl = new FunctionLiteral();

                if (la.Kind == Delegate || la.Kind == Function)
                {
                    Step();
                    fl.LiteralToken = t.Kind;
                }

                // file.d:1248
                /*
                    listdir (".", delegate bool (DirEntry * de)
                    {
                        auto s = std.string.format("%s : c %s, w %s, a %s", de.name,
                                toUTCString (de.creationTime),
                                toUTCString (de.lastWriteTime),
                                toUTCString (de.lastAccessTime));
                        return true;
                    }
                    );
                */
                if (la.Kind != OpenCurlyBrace) // foo( 1, {bar();} ); -> is a legal delegate
                {
                    if (!MemberFunctionAttribute[la.Kind] && lexer.CurrentPeekToken.Kind == OpenParenthesis)
                        fl.AnonymousMethod.Type = BasicType();
                    else if (la.Kind != OpenParenthesis && la.Kind != OpenCurlyBrace)
                        fl.AnonymousMethod.Type = Type();

                    if (la.Kind == OpenParenthesis)
                        fl.AnonymousMethod.Parameters = Parameters(fl.AnonymousMethod);
                }
                FunctionBody(fl.AnonymousMethod);
                return fl;
            }
            #endregion

            #region AssertExpression
            if (la.Kind==(Assert))
            {
                Step();
                Expect(OpenParenthesis);
                var ce = new ClampExpression(ClampExpression.ClampType.Round);
                ce.FrontExpression = new TokenExpression(Assert);
                ce.InnerExpression = AssignExpression();

                if (la.Kind==(Comma))
                {
                    Step();
                    var ate = new AssignTokenExpression(Comma);
                    ate.PrevExpression = ce.InnerExpression;
                    ate.FollowingExpression = AssignExpression();
                    ce.InnerExpression = ate;
                }
                Expect(CloseParenthesis);
                return ce;
            }
            #endregion

            #region MixinExpression | ImportExpression
            if (la.Kind==(Mixin) || la.Kind==(Import))
            {
                Step();
                int tk = t.Kind;
                Expect(OpenParenthesis);
                ClampExpression ce = new ClampExpression(ClampExpression.ClampType.Round);
                ce.FrontExpression = new TokenExpression(tk);
                ce.InnerExpression = AssignExpression();
                Expect(CloseParenthesis);
                return ce;
            }
            #endregion

            #region Typeof
            if (la.Kind==(Typeof))
                return new TypeDeclarationExpression(TypeOf());
            #endregion

            // TypeidExpression
            if (la.Kind==(Typeid))
            {
                Step();
                Expect(OpenParenthesis);
                var ce = new ClampExpression(ClampExpression.ClampType.Round);
                ce.FrontExpression = new TokenExpression(Typeid);
                ce.InnerExpression = IsAssignExpression()? AssignExpression(): new TypeDeclarationExpression(Type());
                Expect(CloseParenthesis);
                return ce;
            }

            #region IsExpression
            if (la.Kind==(Is))
            {
                Step();
                Expect(OpenParenthesis);
                var ce = new ClampExpression(ClampExpression.ClampType.Round);
                ce.FrontExpression = new TokenExpression(Is);

                var ate = new AssignTokenExpression();
                ce.InnerExpression=ate;
                ITypeDeclaration Type_opt = null;

                // Originally, a Type is required!
                if (IsAssignExpression()) // Just allow function calls - but even doing this is still a mess :-D
                    ate.PrevExpression = PostfixExpression();
                else
                    ate.PrevExpression = new TypeDeclarationExpression( Type_opt=Type() );

                if(la.Kind==CloseParenthesis)
                {
                    Step();
                    return ce;
                }

                // Require a == or : as operator!
                if (!(la.Kind == Colon || la.Kind == Equal || la.Kind==CloseParenthesis))
                {
                    // When there's no == or : following the type, we expect a complete declaration here!
                    var n = Declarator(false);
                    n.Type = Type_opt;

                    // What's now missing is to add our node to the parent block

                    /*
                     * alias int A;
                     * static if(is(A myInt == int))
                     * {
                     *      myInt i=0; // allowed here!
                     * }
                     */
                }

                if (la.Kind == Colon || la.Kind == Equal)
                {
                    Step();
                    ate.Token = t.Kind;
                }
                else if (la.Kind == CloseParenthesis)
                {
                    Expect(CloseParenthesis);
                    return ce;
                }

                /*
                TypeSpecialization:
	                Type
	                    struct
	                    union
	                    class
	                    interface
	                    enum
	                    function
	                    delegate
	                    super
	                const
	                immutable
	                inout
	                shared
	                    return
                */

                if (ClassLike[la.Kind] || LA(Typedef) || LA(Enum) || LA(Delegate) || LA(Function) || LA(Super)  || LA(Return))
                {
                    Step();
                    ate.FollowingExpression = new TokenExpression(t.Kind);
                }
                else
                    ate.FollowingExpression = new TypeDeclarationExpression(Type());

                if (la.Kind == Comma)
                {
                    Step();
                    TemplateParameterList(false);
                }

                Expect(CloseParenthesis);
                return ce;
            }
            #endregion

            // ( Expression )
            if (la.Kind==(OpenParenthesis))
            {
                Step();
                var ret = Expression();
                Expect(CloseParenthesis);
                return ret;
            }

            // TraitsExpression
            if (la.Kind==(__traits))
                return TraitsExpression();

            #region BasicType . Identifier
            if (la.Kind==(Const) || la.Kind==(Immutable) || la.Kind==(Shared) || la.Kind==(InOut) || BasicTypes[la.Kind])
            {
                Step();
                DExpression before = null;
                if (!BasicTypes[t.Kind])
                {
                    int tk = t.Kind;
                    if (la.Kind != OpenParenthesis)
                    {
                        var ce = new ClampExpression(ClampExpression.ClampType.Round);
                        ce.FrontExpression = new TokenExpression(tk);
                        ce.InnerExpression = new TypeDeclarationExpression(Type());
                        before = ce;
                    }
                    else
                    {
                        Expect(OpenParenthesis);
                        var ce = new ClampExpression(ClampExpression.ClampType.Round);
                        ce.FrontExpression = new TokenExpression(tk);
                        ce.InnerExpression = new TypeDeclarationExpression(Type());
                        Expect(CloseParenthesis);
                        before = ce;
                    }
                }
                else
                    before = new TokenExpression(t.Kind);

                if (la.Kind==(Dot))
                {
                    Step();
                    Expect(Identifier);
                    AssignTokenExpression ate = new AssignTokenExpression(Dot);
                    ate.PrevExpression = before;
                    ate.FollowingExpression = new IdentExpression(t.Value);
                    return ate;
                }
                return before;
            }
            #endregion

            SynErr(Identifier);
            Step();
            return new TokenExpression(t.Kind);
        }

        bool IsFunctionLiteral()
        {
            if (la.Kind != OpenParenthesis)
                return false;

            OverPeekBrackets(OpenParenthesis,true);

            return lexer.CurrentPeekToken.Kind == OpenCurlyBrace;
        }
        #endregion

        #region Statements
        void IfCondition(ref IBlockNode par)
        {
            IfCondition(ref par, false);
        }
        void IfCondition(ref IBlockNode par,bool IsFor)
        {
            var stmtBlock = par as DStatementBlock;

            if ((!IsFor && lexer.CurrentPeekToken.Kind == Times) || IsAssignExpression())
            {
                if (stmtBlock != null)
                    stmtBlock.Expression = Expression();
                else
                    Expression();
            }
            else
            {
                var sl = la.Location;

                ITypeDeclaration tp = null;
                if (la.Kind == Auto)
                {
                    tp = new DTokenDeclaration(la.Kind);
                    Step();
                }
                else
                    tp = BasicType();

                INode n = null;
            repeated_decl:
                n = Declarator(false);

                n.StartLocation = sl;
                if (n.Type == null)
                    n.Type = tp;
                else
                    n.Type.MostBasic = tp;

                // Initializer is optional
                if (la.Kind == Assign)
                {
                    Expect(Assign);
                    (n as DVariable).Initializer = Expression();
                }
                n.EndLocation = t.EndLocation;
                par.Add(n);
                if (la.Kind == Comma)
                {
                    Step();
                    goto repeated_decl;
                }
            }
        }

        void Statement(IBlockNode par, bool CanBeEmpty, bool BlocksAllowed)
        {
            if (CanBeEmpty && la.Kind == (Semicolon))
            {
                Step();
                return;
            }

            else if (BlocksAllowed && la.Kind == (OpenCurlyBrace))
            {
                BlockStatement(ref par);
                return;
            }

            #region LabeledStatement (loc:... goto loc;)
            else if (la.Kind == (Identifier) && lexer.CurrentPeekToken.Kind == (Colon))
            {
                Step();
                Step();
                return;
            }
            #endregion

            #region IfStatement
            else if (la.Kind == (If) || (la.Kind == Static && lexer.CurrentPeekToken.Kind == If))
            {
                if (la.Kind == Static)
                    Step();
                Step();
                var dbs = new DStatementBlock(If);
                var bs = dbs as IBlockNode;
                dbs.StartLocation = t.Location;
                Expect(OpenParenthesis);
                
                // IfCondition
                IfCondition(ref bs);
                
                Expect(CloseParenthesis);
                // ThenStatement

                Statement(bs, false, true);
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);

                // ElseStatement
                if (la.Kind == (Else))
                {
                    Step();
                    dbs = new DStatementBlock(Else);
                    dbs.StartLocation = t.Location;
                    bs = dbs as IBlockNode;
                    Statement(bs, false, true);
                    dbs.EndLocation = t.EndLocation;
					if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
                }
            }
            #endregion

            #region WhileStatement
            else if (la.Kind == (While))
            {
                Step();

                var dbs = new DStatementBlock(While);
                var bs = dbs as IBlockNode;
                dbs.StartLocation = t.Location;

                Expect(OpenParenthesis);
                IfCondition(ref bs);
                Expect(CloseParenthesis);

                Statement(bs, false, true);
                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region DoStatement
            else if (la.Kind == (Do))
            {
                Step();

                var dbs = new DStatementBlock(Do) as IBlockNode;
                dbs.StartLocation = t.Location;
                Statement(dbs, false, true);

                Expect(While);
                Expect(OpenParenthesis);
                IfCondition(ref dbs);
                Expect(CloseParenthesis);

                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region ForStatement
            else if (la.Kind == (For))
            {
                Step();

                var dbs = new DStatementBlock(For) as IBlockNode;
                dbs.StartLocation = t.Location;

                Expect(OpenParenthesis);

                // Initialize
                if (la.Kind != Semicolon)
                    IfCondition(ref dbs,true);
                Expect(Semicolon);

                // Test
                if (la.Kind != (Semicolon))
                    (dbs as DStatementBlock).Expression=Expression();

                Expect(Semicolon);

                // Increment
                if (la.Kind != (CloseParenthesis))
                    Expression();

                Expect(CloseParenthesis);

                Statement(dbs, false, true);
                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region ForeachStatement
            else if (la.Kind == (Foreach) || la.Kind == (Foreach_Reverse))
            {
                Step();

                var dbs = new DStatementBlock(t.Kind) as IBlockNode;
                dbs.StartLocation = t.Location;

                Expect(OpenParenthesis);

                bool init = true;
                while (init || la.Kind == (Comma))
                {
                    if (!init) Step();
                    init = false;

                    var forEachVar = new DVariable();
                    forEachVar.StartLocation = la.Location;

                    if (la.Kind == (Ref))
                    {
                        Step();
                        forEachVar.Attributes.Add(new DAttribute( Ref));
                    }
                    if (la.Kind == (Identifier) && (lexer.CurrentPeekToken.Kind == (Semicolon) || lexer.CurrentPeekToken.Kind == Comma))
                    {
                        Step();
                        forEachVar.Name = t.Value;
                    }
                    else
                    {
                        forEachVar.Type = Type();
                        if(la.Kind==Identifier)
                        {
                            Expect(Identifier);
                            forEachVar.Name = t.Value;
                        }
                    }
                    forEachVar.EndLocation = t.EndLocation;
                    if(!String.IsNullOrEmpty( forEachVar.Name))dbs.Add(forEachVar);
                }

                Expect(Semicolon);
                (dbs as DStatementBlock).Expression=Expression();

                // ForeachRangeStatement
                if (la.Kind == DoubleDot)
                {
                    Step();
                    //TODO: Put this in the expression variable
                    Expression();
                }

                Expect(CloseParenthesis);

                Statement(dbs, false, true);

                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region [Final] SwitchStatement
            else if ((la.Kind == (Final) && lexer.CurrentPeekToken.Kind == (Switch)) || la.Kind == (Switch))
            {
                var dbs = new DStatementBlock(Switch) as IBlockNode;
                dbs.StartLocation = la.Location;

                if (la.Kind == (Final))
                {
                    (dbs as DNode).Attributes.Add(new DAttribute( Final));
                    Step();
                }
                Step();
                Expect(OpenParenthesis);
                (dbs as DStatementBlock).Expression=Expression();
                Expect(CloseParenthesis);
                Statement(dbs, false, true);
                dbs.EndLocation = t.EndLocation;

                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region CaseStatement
            else if (la.Kind == (Case))
            {
                Step();

                var dbs = new DStatementBlock(Case) as IBlockNode;
                dbs.StartLocation = la.Location;

                (dbs as DStatementBlock).Expression=AssignExpression();

                if (!(la.Kind == (Colon) && lexer.CurrentPeekToken.Kind == (Dot) && Peek().Kind == Dot))
                    while (la.Kind == (Comma))
                    {
                        Step();
                        AssignExpression();
                    }
                Expect(Colon);

                // CaseRangeStatement
                if (la.Kind == DoubleDot)
                {
                    Step();
                    Expect(Case);
                    AssignExpression();
                    Expect(Colon);
                }

                if(la.Kind!=CloseCurlyBrace) // {case 1:} is allowed
                    Statement(dbs, true, true);
                dbs.EndLocation = t.EndLocation;

                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region Default
            else if (la.Kind == (Default))
            {
                Step();

                IBlockNode dbs = new DStatementBlock(Default);
                dbs.StartLocation = la.Location;

                Expect(Colon);
                if(la.Kind!=CloseCurlyBrace) // switch(...) { default: }  is allowed!
                    Statement(dbs, true, true);
                dbs.EndLocation = t.EndLocation;

                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region Continue | Break
            else if (la.Kind == (Continue) || la.Kind == (Break))
            {
                Step();
                if (la.Kind == (Identifier))
                    Step();
                Expect(Semicolon);
            }
            #endregion

            #region Return
            else if (la.Kind == (Return))
            {
                Step();
                if (la.Kind != (Semicolon))
                    Expression();
                Expect(Semicolon);
            }
            #endregion

            #region Goto
            else if (la.Kind == (Goto))
            {
                Step();
                if (la.Kind == (Identifier) || la.Kind == (Default))
                {
                    Step();
                }
                else if (la.Kind == (Case))
                {
                    Step();
                    if (la.Kind != (Semicolon))
                        Expression();
                }

                Expect(Semicolon);
            }
            #endregion

            #region WithStatement
            else if (la.Kind == (With))
            {
                Step();

                IBlockNode dbs = new DStatementBlock(With);
                dbs.StartLocation = t.Location;

                Expect(OpenParenthesis);

                // Symbol
                (dbs as DStatementBlock).Expression=Expression();

                Expect(CloseParenthesis);
                Statement(dbs, false, true);
                dbs.EndLocation = t.EndLocation;

                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region SynchronizedStatement
            else if (la.Kind == (Synchronized))
            {
                Step();
                IBlockNode dbs = new DStatementBlock(Synchronized);
                dbs.StartLocation = t.Location;

                if (la.Kind == (OpenParenthesis))
                {
                    Step();
                    (dbs as DStatementBlock).Expression=Expression();
                    Expect(CloseParenthesis);
                }
                Statement(dbs, false, true);

                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
            }
            #endregion

            #region TryStatement
            else if (la.Kind == (Try))
            {
                Step();

                IBlockNode dbs = new DStatementBlock(Try);
                dbs.StartLocation = t.Location;
                Statement(dbs, false, true);
                dbs.EndLocation = t.EndLocation;
                if ((dbs as IBlockNode).Count > 0) par.Add(dbs);

                if (!(la.Kind == (Catch) || la.Kind == (Finally)))
                    SynErr(Catch, "catch or finally expected");

                // Catches
            do_catch:
                if (la.Kind == (Catch))
                {
                    Step();
                    dbs = new DStatementBlock(Catch);
                    dbs.StartLocation = t.Location;

                    // CatchParameter
                    if (la.Kind == (OpenParenthesis))
                    {
                        Step();
                        var catchVar = new DVariable();
                        DToken tt = t;
                        catchVar.Type = BasicType();
                        if (la.Kind != Identifier)
                        {
                            lexer.CurrentToken = tt;
                            catchVar.Type =new NormalDeclaration( "Exception");
                        }
                        Expect(Identifier);
                        catchVar.Name = t.Value;
                        Expect(CloseParenthesis);
                        dbs.Add(catchVar);
                    }

                    Statement(dbs, false, true);
                    dbs.EndLocation = t.EndLocation;
                    if ((dbs as IBlockNode).Count > 0) par.Add(dbs);

                    if (la.Kind == (Catch))
                        goto do_catch;
                }

                if (la.Kind == (Finally))
                {
                    Step();

                    dbs = new DStatementBlock(Finally);
                    dbs.StartLocation = t.Location;
                    Statement(dbs, false, true);
                    dbs.EndLocation = t.EndLocation;
                    if ((dbs as IBlockNode).Count > 0) par.Add(dbs);
                }
            }
            #endregion

            #region ThrowStatement
            else if (la.Kind == (Throw))
            {
                Step();
                Expression();
                Expect(Semicolon);
            }
            #endregion

            // ScopeGuardStatement
            else if (la.Kind == (Scope))
            {
                Step();
				if (la.Kind == OpenParenthesis)
				{
					Expect(OpenParenthesis);
					Expect(Identifier); // exit, failure, success
					Expect(CloseParenthesis);
				}
				Statement(par, false, true);
            }

            // AsmStatement
            else if (la.Kind == (Asm))
            {
                Step();
                Expect(OpenCurlyBrace);

                while (!IsEOF && la.Kind != (CloseCurlyBrace))
                {
                    Step();
                }

                Expect(CloseCurlyBrace);
            }

            // PragmaStatement
            else if (la.Kind == (Pragma))
            {
                _Pragma();
                Statement(par, true, true);
            }

            // MixinStatement
            //TODO: Handle this one in terms of adding it to the node structure
            else if (la.Kind == (Mixin))
            {
                    MixinDeclaration();
            }

            // (Static) AssertExpression
            else if (la.Kind==Assert || (la.Kind == Static && PK(Assert)))
            {
                if (LA(Static))
                    Step();

                AssignExpression();
                Expect(Semicolon);
            }

            #region VersionStatement | DebugCondition
            else if (la.Kind == Version || la.Kind==Debug)
            {
                Step();

                // a debug attribute doesn't require a '('!
                if (t.Kind == Version || la.Kind == OpenParenthesis)
                {
                    Expect(OpenParenthesis);
                    while (!IsEOF && !LA(CloseParenthesis))
                        Step();
                    Expect(CloseParenthesis);
                }

                if (LA(Colon)) 
                    Step();
                else 
                    Statement(par, false, true);

                if (la.Kind == Else)
                {
                    Step();
                    Statement(par, false, true);
                }
            }
            #endregion

            // Blockstatement
            else if (la.Kind == (OpenCurlyBrace))
                BlockStatement(ref par);

            else if (!(ClassLike[la.Kind] || la.Kind == Enum || Modifiers[la.Kind] || Attributes[la.Kind] || la.Kind == Alias || la.Kind==Typedef) && IsAssignExpression())
            {
                var ex=AssignExpression();
                Expect(Semicolon);
            }
            else
                Declaration(par);
        }

        void BlockStatement(ref IBlockNode par)
        {
            if(String.IsNullOrEmpty( par.Description))par.Description = GetComments();
            var OldPreviousCommentString = PreviousComment;
            PreviousComment = "";

            Expect(OpenCurlyBrace);
            par.BlockStartLocation = t.Location;
            if (la.Kind != CloseCurlyBrace)
            {
                if (ParseStructureOnly)
                    lexer.SkipCurrentBlock();
                else
                    while (!IsEOF && la.Kind != (CloseCurlyBrace))
                    {
                        Statement(par, true, true);
                    }
            }
            Expect(CloseCurlyBrace);
            par.EndLocation = t.EndLocation;

            PreviousComment = OldPreviousCommentString;
        }
        #endregion

        #region Structs & Unions
        private INode AggregateDeclaration()
        {
            if (!(la.Kind==(Union) || la.Kind==(Struct)))
                SynErr(t.Kind, "union or struct required");
            Step();

            var ret = new DClassLike(t.Kind);
			ApplyAttributes(ret);

            // Allow anonymous structs&unions
            if (la.Kind == Identifier)
            {
                Expect(Identifier);
                ret.Name = t.Value;
            }

            if (la.Kind==(Semicolon))
            {
                Step();
                return ret;
            }

            // StructTemplateDeclaration
            if (la.Kind==(OpenParenthesis))
            {
                ret .TemplateParameters = TemplateParameterList();

                // Constraint[opt]
                if (la.Kind==(If))
                    Constraint();
            }

            ClassBody(ret);

            return ret;
        }
        #endregion

        #region Classes
        private INode ClassDeclaration()
        {
            Expect(Class);

            IBlockNode dc = new DClassLike(Class);
			ApplyAttributes(dc as DNode);
            dc.StartLocation = t.Location;

            Expect(Identifier);
            dc.Name = t.Value;

            if (la.Kind==(OpenParenthesis))
                (dc as DNode).TemplateParameters = TemplateParameterList();

            if (la.Kind==(Colon))
                (dc as DClassLike).BaseClasses = BaseClassList();

            ClassBody(dc);

            dc.EndLocation = t.EndLocation;
            return dc;
        }

        private List<ITypeDeclaration> BaseClassList()
        {
            return BaseClassList(true);
        }

        private List<ITypeDeclaration> BaseClassList(bool ExpectColon)
        {
            if(ExpectColon)Expect(Colon);

            var ret = new List<ITypeDeclaration>();

            bool init = true;
            while (init || la.Kind==(Comma))
            {
                if (!init) Step();
                init = false;
                if (IsProtectionAttribute() && la.Kind!=(Protected))
                    Step();

                ret.Add(IdentifierList());
            }
            return ret;
        }

        private void ClassBody(IBlockNode ret)
        {
            if (String.IsNullOrEmpty(ret.Description)) ret.Description = GetComments();
            var OldPreviousCommentString = PreviousComment;
            PreviousComment = "";

            Expect(OpenCurlyBrace);
            ret.BlockStartLocation = t.Location;
            while (!IsEOF && la.Kind!=(CloseCurlyBrace))
            {
                DeclDef(ret);
            }
            Expect(CloseCurlyBrace);
            ret.EndLocation = t.EndLocation;
            PreviousComment = OldPreviousCommentString;
        }

        INode Constructor(bool IsStruct)
        {
            Expect(This);
            var dm = new DMethod();
            dm.SpecialType = DMethod.MethodType.Constructor;
            dm.StartLocation = t.Location;
            dm.Name = "this";

            if (IsStruct && lexer.CurrentPeekToken.Kind==(This) && la.Kind==(OpenParenthesis))
            {
                var dv = new DVariable();
                dv.Name = "this";
                dm.Parameters.Add(dv);
                Step();
                Step();
                Expect(CloseParenthesis);
            }
            else
            {
                if (IsTemplateParameterList())
                    dm.TemplateParameters = TemplateParameterList();

                dm.Parameters = Parameters(dm);
            }

            // handle post argument attributes
            while (IsAttributeSpecifier())
            {
                AttributeSpecifier();
            }

            if (la.Kind==(If))
                Constraint();

            // handle post argument attributes
            while (IsAttributeSpecifier())
            {
                AttributeSpecifier();
            }

            FunctionBody(dm);
            return dm;
        }

        INode Destructor()
        {
            Expect(Tilde);
            Expect(This);
            var dm = new DMethod();
            dm.SpecialType = DMethod.MethodType.Destructor;
            dm.StartLocation = lexer.LastToken.Location;
            dm.Name = "~this";

            if (IsTemplateParameterList())
                dm.TemplateParameters = TemplateParameterList();

            dm.Parameters = Parameters(dm);

            if (la.Kind==(If))
                Constraint();

            FunctionBody(dm);
            return dm;
        }
        #endregion

        #region Interfaces
        private IBlockNode InterfaceDeclaration()
        {
            Expect(Interface);
            var dc = new DClassLike();
            dc.StartLocation = t.Location;
            ApplyAttributes(dc);

            Expect(Identifier);
            dc.Name = t.Value;

            if (la.Kind==(OpenParenthesis))
                dc.TemplateParameters = TemplateParameterList();

            if (la.Kind==(If))
                Constraint();

            if (la.Kind==(Colon))
                dc.BaseClasses = BaseClassList();

            // Empty interfaces are allowed
            if (la.Kind == Semicolon)
                Step();
            else
                ClassBody(dc);

            dc.EndLocation = t.EndLocation;
            return dc;
        }

        void Constraint()
        {
            Expect(If);
            Expect(OpenParenthesis);
            Expression();
            Expect(CloseParenthesis);
        }
        #endregion

        #region Enums
        private void EnumDeclaration(ref IBlockNode par)
        {
            Expect(Enum);

            DEnum mye = new DEnum();
            mye.StartLocation = t.Location;
            ApplyAttributes(mye);

            if (IsBasicType() && la.Kind != Identifier)
                mye.Type = Type();
            else if (la.Kind == Auto)
            {
                Step();
                mye.Type = new DTokenDeclaration(Auto);
            }

            if (la.Kind==(Identifier))
            {
                if (lexer.CurrentPeekToken.Kind==(Assign) || lexer.CurrentPeekToken.Kind==(OpenCurlyBrace) || lexer.CurrentPeekToken.Kind==(Semicolon) || lexer.CurrentPeekToken.Kind==Colon)
                {
                    Step();
                    mye.Name = t.Value;
                }
                else
                {
                    mye.Type = Type();

                    Expect(Identifier);
                    mye.Name = t.Value;
                }
            }

            if (la.Kind==(Colon))
            {
                Step();
                mye.Type = Type();
            }

            if (la.Kind==(Assign) || la.Kind==(Semicolon))
            {
            another_enumvalue:
                DVariable enumVar = new DVariable();
                enumVar.Assign(mye);
                enumVar.Attributes.Add(new DAttribute( Enum));
                if (mye.Type != null)
                    enumVar.Type = mye.Type;
                else
                    enumVar.Type = new DTokenDeclaration(Enum);

                if (la.Kind==(Comma))
                {
                    Step();
                    Expect(Identifier);
                    enumVar.Name = t.Value;
                }

                if (la.Kind==(Assign))
                {
                    Step();
                    enumVar.Initializer = AssignExpression();
                }
                enumVar.EndLocation = t.Location;
                par.Add(enumVar);

                if (la.Kind==(Comma))
                    goto another_enumvalue;

                Expect(Semicolon);
            }
            else
            {
                Expect(OpenCurlyBrace);
                mye.BlockStartLocation = t.Location;

                bool init = true;
                while ((init && la.Kind!=(Comma)) || la.Kind==(Comma))
                {
                    if (!init) Step();
                    init = false;

                    if (la.Kind==(CloseCurlyBrace)) break;

                    DEnumValue ev = new DEnumValue();
                    ev.StartLocation = t.Location;
                    if (la.Kind==(Identifier) && (lexer.CurrentPeekToken.Kind==(Assign) || lexer.CurrentPeekToken.Kind==(Comma) || lexer.CurrentPeekToken.Kind==(CloseCurlyBrace)))
                    {
                        Step();
                        ev.Name = t.Value;
                    }
                    else
                    {
                        ev.Type = Type();
                        Expect(Identifier);
                        ev.Name = t.Value;
                    }

                    if (la.Kind==(Assign))
                    {
                        Step();
                        ev.Initializer = AssignExpression();
                    }

                    ev.EndLocation = t.EndLocation;

                    if (String.IsNullOrEmpty(mye.Name))
                        par.Add(ev);
                    else
                        mye.Add(ev);
                }
                Expect(CloseCurlyBrace);
                mye.EndLocation = t.EndLocation;
                if (!String.IsNullOrEmpty(mye.Name))
                    par.Add(mye);
            }
        }
        #endregion

        #region Functions
        void FunctionBody(IBlockNode par)
        {
            bool HadIn = false, HadOut = false;

        check_again:
            if (!HadIn && la.Kind==(In))
            {
                HadIn = true;
                Step();
                BlockStatement(ref par);

                if (!HadOut && la.Kind==(Out))
                    goto check_again;
            }

            if (!HadOut && la.Kind==(Out))
            {
                HadOut = true;
                Step();

                if (la.Kind==(OpenParenthesis))
                {
                    Step();
                    Expect(Identifier);
                    Expect(CloseParenthesis);
                }

                BlockStatement(ref par);

                if (!HadIn && la.Kind==(In))
                    goto check_again;
            }

            if (HadIn || HadOut)
                Expect(Body);
            else if (la.Kind==(Body))
                Step();

            if (la.Kind == Semicolon) // A function declaration can be empty, of course. This here represents a simple abstract or virtual function
            {
                Step();
                par.Description += CheckForPostSemicolonComment();
            }
            else
                BlockStatement(ref par);

        }
        #endregion

        #region Templates
        /*
         * American beer is like sex on a boat - Fucking close to water;)
         */

        private INode TemplateDeclaration()
        {
            Expect(Template);
            var dc = new DClassLike(Template);
            ApplyAttributes(dc);
            dc.StartLocation = t.Location;

            Expect(Identifier);
            dc.Name = t.Value;

            dc.TemplateParameters = TemplateParameterList();

            if (la.Kind==(If))
                Constraint();

            if (la.Kind==(Colon))
                dc.BaseClasses = BaseClassList();

            ClassBody( dc);

            dc.EndLocation = t.EndLocation;
            return dc;
        }

        /// <summary>
        /// Be a bit lazy here with checking whether there're templates or not
        /// </summary>
        private bool IsTemplateParameterList()
        {
            lexer.StartPeek();
            int r = 0;
            while (r >= 0 && lexer.CurrentPeekToken.Kind != EOF)
            {
                if (lexer.CurrentPeekToken.Kind == OpenParenthesis) r++;
                else if (lexer.CurrentPeekToken.Kind == CloseParenthesis)
                {
                    r--;
                    if (r <= 0)
                        if (Peek().Kind == OpenParenthesis)
                            return true;
                        else return false;
                }
                Peek();
            }
            return false;
        }

        private INode[] TemplateParameterList()
        {
            return TemplateParameterList(true);
        }

        private INode[] TemplateParameterList(bool MustHaveSurroundingBrackets)
        {
            if(MustHaveSurroundingBrackets) Expect(OpenParenthesis);

            var ret = new List<INode>();

            if (la.Kind==(CloseParenthesis))
            {
                Step();
                return ret.ToArray();
            }

            bool init = true;
            while (init || la.Kind==(Comma))
            {
                if (!init) Step();
                init = false;

                INode dv = new DVariable();

                // TemplateThisParameter
                if (la.Kind==(This))
                    Step();

                // TemplateTupleParameter
                if (la.Kind==(Identifier) && lexer.CurrentPeekToken.Kind==TripleDot)
                {
                    Step();
                    dv.Type = new VarArgDecl();
                    dv.Name = t.Value;
                    Step();
                }

                // TemplateAliasParameter
                else if (la.Kind==(Alias))
                {
                    Step();
                    dv.Type = new DTokenDeclaration(Alias);
                    Expect(Identifier);
                    dv.Name = t.Value;

                    // TemplateAliasParameterSpecialization
                    if (la.Kind==(Colon))
                    {
                        Step();

                        dv.Type = new InheritanceDecl(dv.Type);
                        (dv.Type as InheritanceDecl).InheritedClass = Type();
                    }

                    // TemplateAliasParameterDefault
                    if (la.Kind==(Assign))
                    {
                        Step();
                        if (la.Kind == Literal)
                        {
                            Step();
                            (dv as DVariable).Initializer = new IdentExpression(t.Value);
                        }else
                        (dv as DVariable).Initializer = new TypeDeclarationExpression(Type());
                    }
                }

                // TemplateTypeParameter
                else if (la.Kind==(Identifier) && (lexer.CurrentPeekToken.Kind==(Colon) || lexer.CurrentPeekToken.Kind==(Assign) || lexer.CurrentPeekToken.Kind==(Comma) || lexer.CurrentPeekToken.Kind==(CloseParenthesis)))
                {
                    Step();
                    dv.Name = t.Value;

                    if (la.Kind==(Colon))
                    {
                        Step();
                        dv.Type = new InheritanceDecl(dv.Type);
                        (dv.Type as InheritanceDecl).InheritedClass = Type();
                    }

                    if (la.Kind==(Assign))
                    {
                        Step();
                        (dv as DVariable).Initializer = new TypeDeclarationExpression(Type());
                    }
                }

                else
                {
                    var bt = BasicType();
                    dv = Declarator(false);

                    if (dv.Type == null)
                        dv.Type = bt;
                    else
                        dv.Type.Base = bt;

                    if (la.Kind==(Colon))
                    {
                        Step();
                        ConditionalExpression();
                    }

                    if (la.Kind==(Assign))
                        (dv as DVariable).Initializer = Initializer();
                }
                ret.Add(dv);
            }

            if (MustHaveSurroundingBrackets) Expect(CloseParenthesis);

            return ret.ToArray();
        }

        private AbstractTypeDeclaration TemplateInstance()
        {
            Expect(Identifier);
            var td = new TemplateDecl(new NormalDeclaration(t.Value));
            Expect(Not);
            if (la.Kind==(OpenParenthesis))
            {
                Step();
                if (la.Kind != CloseParenthesis)
                {
                    bool init = true;
                    while (init || la.Kind == (Comma))
                    {
                        if (!init) Step();
                        init = false;

                        if (IsAssignExpression())
                            td.Template.Add(new DExpressionDecl(AssignExpression()));
                        else
                            td.Template.Add(Type());
                    }
                }
                Expect(CloseParenthesis);
            }
            else
            {
                Step();
                if (t.Kind==(Identifier) || t.Kind==(Literal))
                    td.Template.Add(new NormalDeclaration(t.Value));
                else
                    td.Template.Add(new DTokenDeclaration(t.Kind));
            }
            return td;
        }
        #endregion

        #region Traits
        DExpression TraitsExpression()
        {
            Expect(__traits);
            Expect(OpenParenthesis);
            var ce = new ClampExpression(new TokenExpression(__traits), ClampExpression.ClampType.Round);

            //TODO: traits keywords
            Expect(Identifier);
            string TraitKey = t.Value;
            
            while (la.Kind == Comma)
            {
                Step();
                if (IsAssignExpression())
                    AssignExpression();
                else
                    Type();
            }

            Expect(CloseParenthesis);
            return ce;
        }
        #endregion
	}
}
