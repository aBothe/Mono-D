using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public partial class DResolver
	{
		public static ResolveResult[] ResolveType(IEditorData editor,
			ResolverContextStack ctxt,
			bool alsoParseBeyondCaret = false,
			bool onlyAssumeIdentifierList = false)
		{
			var code = editor.ModuleCode;

			int start = 0;
			CodeLocation startLocation=CodeLocation.Empty;
			bool IsExpression = false;
			
			if (ctxt.CurrentContext.ScopedStatement is IExpressionContainingStatement)
			{
				var exprs=(ctxt.CurrentContext.ScopedStatement as IExpressionContainingStatement).SubExpressions;
				IExpression targetExpr = null;

				if(exprs!=null)
					foreach (var ex in exprs)
						if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, editor.CaretLocation))
							!=ex)
							break;

				if (targetExpr != null && editor.CaretLocation >= targetExpr.Location && editor.CaretLocation <= targetExpr.EndLocation)
				{
					startLocation = targetExpr.Location;
					start = DocumentHelper.LocationToOffset(editor.ModuleCode, startLocation);
					IsExpression = true;
				}
			}
			
			if(!IsExpression)
			{
				// First check if caret is inside a comment/string etc.
				int lastStart=-1;
				int lastEnd=-1;
				var caretContext = CaretContextAnalyzer.GetTokenContext(code, editor.CaretOffset, out lastStart, out lastEnd);

				// Return if comment etc. found
				if (caretContext != TokenContext.None)
					return null;

				start = CaretContextAnalyzer.SearchExpressionStart(code, editor.CaretOffset - 1,
					(lastEnd > 0 && lastEnd < editor.CaretOffset) ? lastEnd : 0);
				startLocation = DocumentHelper.OffsetToLocation(editor.ModuleCode, start);
			}

			if (start < 0 || editor.CaretOffset<=start)
				return null;

			var expressionCode = code.Substring(start, alsoParseBeyondCaret ? code.Length - start : editor.CaretOffset - start);

			var parser = DParser.Create(new StringReader(expressionCode));
			parser.Lexer.SetInitialLocation(startLocation);
			parser.Step();

			if (!IsExpression && onlyAssumeIdentifierList && parser.Lexer.LookAhead.Kind == DTokens.Identifier)
				return TypeDeclarationResolver.Resolve(parser.IdentifierList(), ctxt);
			else if (IsExpression || parser.IsAssignExpression())
			{
				var expr = parser.AssignExpression();

				if (expr != null)
				{
					// Do not accept number literals but (100.0) etc.
					if (expr is IdentifierExpression && (expr as IdentifierExpression).Format.HasFlag(LiteralFormat.Scalar))
						return null;

					expr = ExpressionHelper.SearchExpressionDeeply(expr, editor.CaretLocation);

					return ExpressionTypeResolver.Resolve(expr, ctxt);
				}
			}
			else
				return TypeDeclarationResolver.Resolve(parser.Type(), ctxt);

			return null;
		}

		static int bcStack = 0;
		public static TypeResult[] ResolveBaseClass(DClassLike ActualClass, ResolverContextStack ctxt)
		{
			if (bcStack > 8)
			{
				bcStack--;
				return null;
			}

			if (ActualClass == null || ((ActualClass.BaseClasses == null || ActualClass.BaseClasses.Count < 1) && ActualClass.Name != null && ActualClass.Name.ToLower() == "object"))
				return null;

			var ret = new List<TypeResult>();
			// Implicitly set the object class to the inherited class if no explicit one was done
			var type = (ActualClass.BaseClasses == null || ActualClass.BaseClasses.Count < 1) ? new IdentifierDeclaration("Object") : ActualClass.BaseClasses[0];

			// A class cannot inherit itself
			if (type == null || type.ToString(false) == ActualClass.Name || ActualClass.NodeRoot == ActualClass)
				return null;

			bcStack++;

			/*
			 * If the ActualClass is defined in an other module (so not in where the type resolution has been started),
			 * we have to enable access to the ActualClass's module's imports!
			 * 
			 * module modA:
			 * import modB;
			 * 
			 * class A:B{
			 * 
			 *		void bar()
			 *		{
			 *			fooC(); // Note that modC wasn't imported publically! Anyway, we're still able to access this method!
			 *			// So, the resolver must know that there is a class C.
			 *		}
			 * }
			 * 
			 * -----------------
			 * module modB:
			 * import modC;
			 * 
			 * // --> When being about to resolve B's base class C, we have to use the imports of modB(!), not modA
			 * class B:C{}
			 * -----------------
			 * module modC:
			 * 
			 * class C{
			 * 
			 * void fooC();
			 * 
			 * }
			 */
			ctxt.PushNewScope(ActualClass.Parent as IBlockNode);

			var results = TypeDeclarationResolver.Resolve(type, ctxt);

			ctxt.Pop();

			if (results != null)
				foreach (var i in results)
					if (i is TypeResult)
						ret.Add(i as TypeResult);
			bcStack--;

			return ret.Count > 0 ? ret.ToArray() : null;
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
					if (n.NodeRoot == ctxt.CurrentContext.ScopedBlock.NodeRoot)
						newRes.Add(rb);
				}
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
