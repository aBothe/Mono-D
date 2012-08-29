using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.Linq;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public partial class DResolver
	{
		[Flags]
		public enum AstReparseOptions
		{
			AlsoParseBeyondCaret=1,
			OnlyAssumeIdentifierList=2,

			/// <summary>
			/// Returns the expression without scanning it down depending on the caret location
			/// </summary>
			ReturnRawParsedExpression=4,
			/// <summary>
			/// If passed, the last call, template instance or new() expression will be returned
			/// </summary>
			WatchForParamExpressions=8,
		}

		/// <summary>
		/// Reparses the code of the current scope and returns the object (either IExpression or ITypeDeclaration derivative)
		/// that is beneath the caret location.
		/// 
		/// Used for code completion/symbol resolution.
		/// Mind the extra options that might be passed via the Options parameter.
		/// </summary>
		/// <param name="ctxt">Can be null</param>
		public static ISyntaxRegion GetScopedCodeObject(IEditorData editor,
			ResolverContextStack ctxt=null,
			AstReparseOptions Options=0)
		{
			if (ctxt == null)
				ctxt = ResolverContextStack.Create(editor);

			var code = editor.ModuleCode;

			int start = 0;
			var startLocation = CodeLocation.Empty;
			bool IsExpression = false;

			if (ctxt.CurrentContext.ScopedStatement is IExpressionContainingStatement)
			{
				var exprs = ((IExpressionContainingStatement)ctxt.CurrentContext.ScopedStatement).SubExpressions;
				IExpression targetExpr = null;

				if (exprs != null)
					foreach (var ex in exprs)
						if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, editor.CaretLocation, true))
							!= ex)
							break;

				if (targetExpr != null && editor.CaretLocation >= targetExpr.Location && editor.CaretLocation <= targetExpr.EndLocation)
				{
					startLocation = targetExpr.Location;
					start = DocumentHelper.LocationToOffset(editor.ModuleCode, startLocation);
					IsExpression = true;
				}
			}

			if (!IsExpression)
			{
				// First check if caret is inside a comment/string etc.
				int lastStart = -1;
				int lastEnd = -1;
				var caretContext = CaretContextAnalyzer.GetTokenContext(code, editor.CaretOffset, out lastStart, out lastEnd);

				// Return if comment etc. found
				if (caretContext != TokenContext.None)
					return null;

				start = CaretContextAnalyzer.SearchExpressionStart(code, editor.CaretOffset - 1,
					(lastEnd > 0 && lastEnd < editor.CaretOffset) ? lastEnd : 0);
				startLocation = DocumentHelper.OffsetToLocation(editor.ModuleCode, start);
			}

			if (start < 0 || editor.CaretOffset <= start)
				return null;

			var expressionCode = code.Substring(start, Options.HasFlag(AstReparseOptions.AlsoParseBeyondCaret) ? code.Length - start : editor.CaretOffset - start);

			var parser = DParser.Create(new StringReader(expressionCode));
			parser.Lexer.SetInitialLocation(startLocation);
			parser.Step();

			if (!IsExpression && Options.HasFlag(AstReparseOptions.OnlyAssumeIdentifierList) && parser.Lexer.LookAhead.Kind == DTokens.Identifier)
			{
				return parser.IdentifierList();
			}
			else if (IsExpression || parser.IsAssignExpression())
			{
				if (Options.HasFlag(AstReparseOptions.ReturnRawParsedExpression))
					return parser.AssignExpression();
				else
					return ExpressionHelper.SearchExpressionDeeply(parser.AssignExpression(), editor.CaretLocation, Options.HasFlag(AstReparseOptions.WatchForParamExpressions));
			}
			else
				return parser.Type();
		}

		public static AbstractType[] ResolveType(IEditorData editor,AstReparseOptions Options=0)
		{
			return ResolveType(editor, ResolverContextStack.Create(editor), Options);
		}

		public static AbstractType[] ResolveType(IEditorData editor, ResolverContextStack ctxt, AstReparseOptions Options=0)
		{
			if (ctxt == null)
				return null;

			var o = GetScopedCodeObject(editor, ctxt, Options);

			if (o is IExpression)
			{
				var t=Evaluation.EvaluateType((IExpression)o, ctxt);
				
				if (t != null)
					return new[] { t };
			}
			else if(o is ITypeDeclaration)
				return TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);

			return null;
		}

		static int bcStack = 0;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces.
		/// Also usable for enums.
		/// 
		/// Never returns null. Instead, the original 'tr' object will be returned if no base class was resolved.
		/// Will clone 'tr', whereas the new object will contain the base class.
		/// </summary>
		public static UserDefinedType ResolveBaseClasses(UserDefinedType tr, ResolverContextStack ctxt, bool ResolveFirstBaseIdOnly=false)
		{
			if (bcStack > 8)
			{
				bcStack--;
				return tr;
			}

			if (tr is EnumType)
			{
				var et = tr as EnumType;

				AbstractType bt = null;

				if(et.Definition.Type == null)
					bt = new PrimitiveType(DTokens.Int);
				else
				{
					if(tr.Definition.Parent is IBlockNode)
						ctxt.PushNewScope((IBlockNode)tr.Definition.Parent);

					var bts=TypeDeclarationResolver.Resolve(et.Definition.Type, ctxt);

					if (tr.Definition.Parent is IBlockNode)
						ctxt.Pop();

					ctxt.CheckForSingleResult(bts, et.Definition.Type);

					if(bts!=null && bts.Length!=0)
						bt=bts[0];
				}

				return new EnumType(et.Definition, bt, et.DeclarationOrExpressionBase);
			}

			var dc = tr.Definition as DClassLike;
			// Return immediately if searching base classes of the Object class
			if (dc == null || ((dc.BaseClasses == null || dc.BaseClasses.Count < 1) && dc.Name == "Object"))
				return tr;

			// If no base class(es) specified, and if it's no interface that is handled, return the global Object reference
			// -- and do not throw any error message, it's ok
			if(dc.BaseClasses == null || dc.BaseClasses.Count < 1)
			{
				if(tr is ClassType) // Only Classes can inherit from non-interfaces
					return new ClassType(dc, tr.DeclarationOrExpressionBase, ctxt.ParseCache.ObjectClassResult);
				return tr;
			}

			#region Base class & interface resolution
			TemplateIntermediateType baseClass=null;
			var interfaces = new List<InterfaceType>();

			if (!(tr is ClassType || tr is InterfaceType))
			{
				if (dc.BaseClasses.Count != 0)
					ctxt.LogError(dc,"Only classes and interfaces may inherit from other classes/interfaces");
				return tr;
			}

			for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
			{
				var type = dc.BaseClasses[i];

				// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
				if (type is IdentifierDeclaration && ((IdentifierDeclaration)type).Id == "Object")
				{
					if (baseClass!=null)
					{
						ctxt.LogError(new ResolutionError(dc, "Class must not have two base classes"));
						continue;
					}
					else if (i != 0)
					{
						ctxt.LogError(new ResolutionError(dc, "The base class name must preceed base interfaces"));
						continue;
					}

					baseClass = ctxt.ParseCache.ObjectClassResult;
					continue;
				}

				if (type == null || type.ToString(false) == dc.Name || dc.NodeRoot == dc)
				{
					ctxt.LogError(new ResolutionError(dc, "A class cannot inherit from itself"));
					continue;
				}

				ctxt.PushNewScope(dc.Parent as IBlockNode);

				bcStack++;

				var res=TypeDeclarationResolver.Resolve(type, ctxt);

				ctxt.CheckForSingleResult(res, type);

				if(res!=null && res.Length != 0)
				{
					var r = res[0];
					if (r is ClassType || r is TemplateType)
					{
						if (tr is InterfaceType)
							ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
						else if (i == 0)
						{
							baseClass = (TemplateIntermediateType)r;
						}
						else
							ctxt.LogError(new ResolutionError(dc, "The base "+(r is ClassType ?  "class" : "template")+" name must preceed base interfaces"));
					}
					else if (r is InterfaceType)
					{
						interfaces.Add((InterfaceType)r);
					}
					else
					{
						ctxt.LogError(new ResolutionError(type, "Resolved class is neither a class nor an interface"));
						continue;
					}
				}

				bcStack--;

				ctxt.Pop();
			}
			#endregion

			if (baseClass == null && interfaces.Count == 0)
				return tr;

			if (tr is ClassType)
				return new ClassType(dc, tr.DeclarationOrExpressionBase, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), tr.DeducedTypes);
			else if (tr is InterfaceType)
				return new InterfaceType(dc, tr.DeclarationOrExpressionBase, interfaces.Count == 0 ? null : interfaces.ToArray(), tr.DeducedTypes);
			
			// Method should end here
			return tr;
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where, out IStatement ScopedStatement)
		{
			ScopedStatement = null;

			if (Parent != null && Parent.Count > 0)
			{
				foreach (var n in Parent.Children)
					if (n is IBlockNode && Where > n.Location && Where < n.EndLocation)
						return SearchBlockAt((IBlockNode)n, Where, out ScopedStatement);
			}

			if (Parent is DMethod)
			{
				var dm = Parent as DMethod;

				var body = dm.GetSubBlockAt(Where);

				// First search the deepest statement under the caret
				if (body != null)
					ScopedStatement = body.SearchStatementDeeply(Where);
			}

			return Parent;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
				{
					var dc = n as DClassLike;
					if (dc==null)
						continue;

					if (Where > dc.BlockStartLocation && Where < dc.EndLocation)
						return SearchClassLikeAt(dc, Where);
				}

			return Parent;
		}

		public static IEnumerable<T> FilterOutByResultPriority<T>(
			ResolverContextStack ctxt,
			IEnumerable<T> results) where T : AbstractType
		{
			if (results == null)
				return null;

			var newRes = new List<T>();

			foreach (var rb in results)
			{
				var n = GetResultMember(rb);
				if (n != null)
				{
					// Put priority on locals
					if (n is DVariable &&
						(n as DVariable).IsLocal)
						return new[] { rb };

					// If member/type etc. is part of the actual module, omit external symbols
					if (n.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot)
					{
						bool omit = false;
						foreach (var r in newRes)
						{
							var k = GetResultMember(r);
							if (k != null && k.NodeRoot == ctxt.CurrentContext.ScopedBlock.NodeRoot)
							{
								omit = true;
								break;
							}
						}

						if (omit)
							continue;
					}
					else
						foreach (var r in newRes.ToArray())
						{
							var k = GetResultMember(r);
							if (k != null && k.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot)
								newRes.Remove(r);
						}
				}
				
				newRes.Add(rb);
			}

			return newRes.Count > 0 ? newRes.ToArray():null;
		}

		public static DNode GetResultMember(ISemantic res)
		{
			if(res is DSymbol)
				return ((DSymbol)res).Definition;

			return null;
		}

		/// <summary>
		/// If an aliased type result has been passed to this method, it'll return the resolved type.
		/// If aliases were done multiple times, it also tries to skip through these.
		/// 
		/// alias char[] A;
		/// alias A B;
		/// 
		/// var resolvedType=TryRemoveAliasesFromResult(% the member result from B %);
		/// --> resolvedType will be StaticTypeResult from char[]
		/// 
		/// </summary>
		public static AbstractType StripAliasSymbol(AbstractType r)
		{
			while(r is AliasedType)
				r = (r as DerivedDataType).Base;

			return r;
		}

		public static AbstractType[] StripAliasSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
					l.Add(StripAliasSymbol(r));

			return l.ToArray();
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		/// <param name="resolvedMember">True if a member (not an alias!) had to be bypassed</param>
		public static AbstractType StripMemberSymbols(AbstractType r)
		{
			r = StripAliasSymbol(r);

			if(r is MemberSymbol)
				r = ((DSymbol)r).Base;

			return StripAliasSymbol(r);
		}

		public static AbstractType[] StripMemberSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
				{
					l.Add(StripMemberSymbols(r));
				}

			return l.ToArray();
		}
	}
}
