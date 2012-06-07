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
		}

		/// <summary>
		/// Reparses the code of the current scope and returns the object (either IExpression or ITypeDeclaration derivative)
		/// that is beneath the caret location.
		/// 
		/// Used for code completion/symbol resolution.
		/// </summary>
		/// <param name="ctxt">Can be null</param>
		public static object GetScopedCodeObject(IEditorData editor,
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
				var exprs = (ctxt.CurrentContext.ScopedStatement as IExpressionContainingStatement).SubExpressions;
				IExpression targetExpr = null;

				if (exprs != null)
					foreach (var ex in exprs)
						if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, editor.CaretLocation))
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
					return ExpressionHelper.SearchExpressionDeeply(parser.AssignExpression(), editor.CaretLocation);
			}
			else
				return parser.Type();
		}

		public static ResolveResult[] ResolveType(IEditorData editor,AstReparseOptions Options=0)
		{
			return ResolveType(editor,ResolverContextStack.Create(editor),Options);
		}

		public static ResolveResult[] ResolveType(IEditorData editor, ResolverContextStack ctxt, AstReparseOptions Options=0)
		{
			if (ctxt == null)
				return null;

			var o = GetScopedCodeObject(editor, ctxt, Options);

			if (o is IExpression)
			{
				// Do not accept number literals but (100.0) etc.
				if (o is IdentifierExpression && ((IdentifierExpression)o).Format.HasFlag(LiteralFormat.Scalar))
					return null;

				return ExpressionTypeResolver.Resolve((IExpression)o, ctxt);
			}
			else if(o is ITypeDeclaration)
				return TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);

			return null;
		}

		static int bcStack = 0;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces
		/// </summary>
		public static void ResolveBaseClasses(TypeResult tr, ResolverContextStack ctxt, bool ResolveFirstBaseIdOnly=false)
		{
			if (bcStack > 8)
			{
				bcStack--;
				return;
			}

			var dc = tr.Node as DClassLike;
			// Return immediately if searching base classes of the Object class
			if (dc == null || ((dc.BaseClasses == null || dc.BaseClasses.Count < 1) && dc.Name == "Object"))
				return;

			// If no base class(es) specified, and if it's no interface that is handled, return the global Object reference
			if(dc.BaseClasses == null || dc.BaseClasses.Count < 1)
			{
				if(dc.ClassType != DTokens.Interface) // Only Non-interfaces can inherit from non-interfaces
					tr.BaseClass= ctxt.ParseCache.ObjectClassResult;
				return;
			}

			var interfaces = new List<TypeResult[]>();
			for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
			{
				var type = dc.BaseClasses[i];

				// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
				if (type is IdentifierDeclaration && ((IdentifierDeclaration)type).Id == "Object")
				{
					if (tr.BaseClass != null)
					{
						// Error: Two base classes!
						continue;
					}
					else if (i != 0)
					{
						// Error: Super class must be at first position!
						continue;
					}

					tr.BaseClass = ctxt.ParseCache.ObjectClassResult;
					continue;
				}

				if (type == null || type.ToString(false) == dc.Name || dc.NodeRoot == dc)
				{
					// Error: A class cannot inherit itself
					continue;
				}

				ctxt.PushNewScope(dc.Parent as IBlockNode);

				bcStack++;

				var res=TypeDeclarationResolver.Resolve(type, ctxt);

				if (res == null)
				{
					// Error: Couldn't resolve 'type'
				}
				else
				{
					var curInterface = new List<TypeResult>();
					bool isClass = false;
					foreach (var r in res)
					{
						var ttr = r as TypeResult;

						if (ttr == null)
						{
							// Error: Invalid base type
							continue;
						}

						var dc_ = ttr.Node as DClassLike;

						if (dc_ == null)
						{
							// Error: Invalid base type
							continue;
						}

						switch (dc_.ClassType)
						{
							case DTokens.Class:
								if (i == 0)
								{
									isClass = true;
									curInterface.Add(ttr);

									if (curInterface.Count > 1)
									{
										// Error: Ambiguous declaration!
									}
								}
								else
								{
									// Error: Base class can only be supplied in the first position
								}
								break;
							case DTokens.Interface:
								curInterface.Add(ttr);
								
								if (isClass || curInterface.Count > 1)
								{
									// Error: Ambiguous declaration
								}
								
								break;
							default:
								// Error: Cannot inherit from other types than 'class' and interface!
								break;
						}
					}

					if (isClass)
						tr.BaseClass = curInterface.ToArray();
					else
						interfaces.Add(curInterface.ToArray());
				}

				bcStack--;

				ctxt.Pop();
			}

			tr.ImplementedInterfaces = interfaces.ToArray();
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where, out IStatement ScopedStatement)
		{
			ScopedStatement = null;

			if (Parent != null && Parent.Count > 0)
			{
				var pi = Parent.Children;
				foreach (var n in pi)
					if (n is IBlockNode && Where >= n.StartLocation && Where <= n.EndLocation)
						return SearchBlockAt(n as IBlockNode, Where, out ScopedStatement);
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
					if (!(n is DClassLike)) continue;

					var b = n as IBlockNode;
					if (Where >= b.BlockStartLocation && Where <= b.EndLocation)
						return SearchClassLikeAt(b, Where);
				}

			return Parent;
		}

		public static IEnumerable<ResolveResult> FilterOutByResultPriority(
			ResolverContextStack ctxt,
			IEnumerable<ResolveResult> results)
		{
			if (results == null)
				return null;

			var newRes = new List<ResolveResult>();

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
						foreach (var r in newRes)
						{
							bool omit = false;

							var k = GetResultMember(r);
							if (k != null && k.NodeRoot == ctxt.CurrentContext.ScopedBlock.NodeRoot)
							{
								omit = true;
								break;
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

		public static INode GetResultMember(ResolveResult res)
		{
			if (res is MemberResult)
				return ((MemberResult)res).Node;
			else if (res is TypeResult)
				return ((TypeResult)res).Node;
			else if (res is ModuleResult)
				return ((ModuleResult)res).Module;

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
		public static ResolveResult[] TryRemoveAliasesFromResult(IEnumerable<ResolveResult> initialResults)
		{
			if (initialResults == null)
				return null;

			var ret = new List<ResolveResult>(initialResults);
			var l2 = new List<ResolveResult>();

			while (ret.Count > 0)
			{
				foreach (var res in ret)
				{
					var mr = res as MemberResult;
					if (mr != null &&

						// Alias check
						mr.Node is DVariable &&	((DVariable)mr.Node).IsAlias &&

						// Check if it has resolved base types
						mr.MemberBaseTypes != null &&
						mr.MemberBaseTypes.Length > 0)
						l2.AddRange(mr.MemberBaseTypes);
				}

				if (l2.Count < 1)
					break;

				ret.Clear();
				ret.AddRange(l2);
				l2.Clear();
			}

			return ret.ToArray();
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		/// <param name="resolvedMember">True if a member (not an alias!) had to be bypassed</param>
		public static ResolveResult[] ResolveMembersFromResult(IEnumerable<ResolveResult> initialResults, out bool resolvedMember)
		{
			resolvedMember = false;

            if (initialResults == null)
                return null;

			var ret = new List<ResolveResult>(initialResults);
			var l2 = new List<ResolveResult>();

			while (ret.Count > 0)
			{
				foreach (var res in ret)
				{
					var mr = res as MemberResult;
					if (mr != null && mr.MemberBaseTypes != null)
					{
						l2.AddRange(mr.MemberBaseTypes);

						if(!(mr.Node is DVariable) || !((DVariable)mr.Node).IsAlias)
							resolvedMember = true;
					}
				}

				if (l2.Count < 1)
					break;

				ret.Clear();
				ret.AddRange(l2);
				l2.Clear();
			}

			return ret.Count == 0? null : ret.ToArray();
		}
	}
}
