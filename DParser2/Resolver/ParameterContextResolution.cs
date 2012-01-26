using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	public class ArgumentsResolutionResult
	{
		public bool IsMethodArguments;
		public bool IsTemplateInstanceArguments;

		public IExpression ParsedExpression;
		public ITypeDeclaration MethodIdentifier;

		public ResolveResult[] ResolvedTypesOrMethods;

		public readonly Dictionary<IExpression, ResolveResult[]> TemplateArguments = new Dictionary<IExpression, ResolveResult[]>();
		/// <summary>
		/// Stores the already typed arguments (Expressions) + their resolved types.
		/// The value part will be null if nothing could get returned.
		/// </summary>
		public readonly Dictionary<IExpression, ResolveResult[]> Arguments = new Dictionary<IExpression, ResolveResult[]>();

		/// <summary>
		///	Identifies the currently called method overload. Is an index related to <see cref="ResolvedTypesOrMethods"/>
		/// </summary>
		public int CurrentlyCalledMethod;
		public IExpression CurrentlyTypedArgument
		{
			get
			{
				if (Arguments != null && Arguments.Count > CurrentlyTypedArgumentIndex)
				{
					int i = 0;
					foreach (var kv in Arguments)
					{
						if (i == CurrentlyTypedArgumentIndex)
							return kv.Key;
						i++;
					}
				}
				return null;
			}
		}
		public int CurrentlyTypedArgumentIndex;
	}

	public class ParameterContextResolution
	{
		/// <summary>
		/// Reparses the given method's fucntion body until the cursor position,
		/// searches the last occurring method call or template instantiation,
		/// counts its already typed arguments
		/// and returns a wrapper containing all the information.
		/// </summary>
		/// <param name="code"></param>
		/// <param name="caret"></param>
		/// <param name="caretLocation"></param>
		/// <param name="MethodScope"></param>
		/// <param name="scopedStatement"></param>
		/// <returns></returns>
		public static ArgumentsResolutionResult LookupArgumentRelatedStatement(
			string code, 
			int caret, 
			CodeLocation caretLocation,
			DMethod MethodScope)
		{
			IStatement scopedStatement = null;

			var curMethodBody = MethodScope.GetSubBlockAt(caretLocation);

			if (curMethodBody == null && MethodScope.Parent is DMethod)
			{
				MethodScope = MethodScope.Parent as DMethod;
				curMethodBody = MethodScope.GetSubBlockAt(caretLocation);
			}

			if (curMethodBody == null)
				return null;

			var blockOpenerLocation = curMethodBody.StartLocation;
			var blockOpenerOffset = blockOpenerLocation.Line <= 0 ? blockOpenerLocation.Column :
				DocumentHelper.LocationToOffset(code, blockOpenerLocation);

			if (blockOpenerOffset >= 0 && caret - blockOpenerOffset > 0)
			{
				var codeToParse = code.Substring(blockOpenerOffset, caret - blockOpenerOffset);

				curMethodBody = DParser.ParseBlockStatement(codeToParse, blockOpenerLocation, MethodScope);

				if (curMethodBody != null)
					scopedStatement = curMethodBody.SearchStatementDeeply(caretLocation);
			}

			if (curMethodBody == null || scopedStatement == null)
				return null;

			var e= SearchForMethodCallsOrTemplateInstances(scopedStatement, caretLocation);

			/*
			 * 1) foo(			-- normal arguments only
			 * 2) foo!(...)(	-- normal arguments + template args
			 * 3) foo!(		-- template args only
			 * 4) new myclass(  -- ctor call
			 * 5) new myclass!( -- ditto
			 * 6) new myclass!(...)(
			 * 7) mystruct(		-- opCall call
			 */
			var res = new ArgumentsResolutionResult() { ParsedExpression = e };

			// 1), 2)
			if (e is PostfixExpression_MethodCall)
			{
				res.IsMethodArguments = true;
				var call = e as PostfixExpression_MethodCall;

				if (call.Arguments != null)
				{
					int i = 0;
					foreach (var arg in call.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

				res.MethodIdentifier = call.PostfixForeExpression.ExpressionTypeRepresentation;

			}
			// 3)
			else if (e is TemplateInstanceExpression)
			{
				var templ = e as TemplateInstanceExpression;

				res.IsTemplateInstanceArguments = true;

				if (templ.Arguments != null)
				{
					int i = 0;
					foreach (var arg in templ.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

				res.MethodIdentifier = new IdentifierDeclaration(templ.TemplateIdentifier.Value) { InnerDeclaration = templ.InnerDeclaration };
			}
			else if (e is NewExpression)
			{
				var ne = e as NewExpression;

				if (ne.Arguments != null)
				{
					int i = 0;
					foreach (var arg in ne.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

				res.MethodIdentifier = ne.ExpressionTypeRepresentation;
			}

			return res;
		}

		public static ArgumentsResolutionResult ResolveArgumentContext(
			string code,
			int caretOffset,
			CodeLocation caretLocation,
			DMethod MethodScope,
			IEnumerable<IAbstractSyntaxTree> parseCache, IEnumerable<IAbstractSyntaxTree> ImportCache)
		{
			var ctxt = new ResolverContext { ScopedBlock = MethodScope, ParseCache = parseCache, ImportCache = ImportCache };

			var res = LookupArgumentRelatedStatement(code, caretOffset, caretLocation, MethodScope);

			if (res.MethodIdentifier == null)
				return null;

			// Resolve all types, methods etc. which belong to the methodIdentifier
			res.ResolvedTypesOrMethods = DResolver.ResolveType(res.MethodIdentifier, ctxt);

			if (res.ResolvedTypesOrMethods == null)
				return res;

			// 4),5),6)
			if (res.ParsedExpression is NewExpression)
			{
				var substitutionList = new List<ResolveResult>();
				foreach (var rr in res.ResolvedTypesOrMethods)
					if (rr is TypeResult)
					{
						var classDef = (rr as TypeResult).ResolvedTypeDefinition as DClassLike;

						if (classDef == null)
							continue;

						//TODO: Regard protection attributes for ctor members
						foreach (var i in classDef)
							if (i is DMethod && (i as DMethod).SpecialType == DMethod.MethodType.Constructor)
								substitutionList.Add(DResolver.HandleNodeMatch(i, ctxt, resultBase: rr));
					}

				if (substitutionList.Count > 0)
					res.ResolvedTypesOrMethods = substitutionList.ToArray();
			}

			// 7)
			else if (res.ParsedExpression is PostfixExpression_MethodCall)
			{
				var substitutionList = new List<ResolveResult>();

				var nonAliases = DResolver.TryRemoveAliasesFromResult(res.ResolvedTypesOrMethods);

				foreach (var rr in nonAliases)
					if (rr is TypeResult)
					{
						var classDef = (rr as TypeResult).ResolvedTypeDefinition as DClassLike;

						if (classDef == null)
							continue;

						//TODO: Regard protection attributes for opCall members
						foreach (var i in classDef)
							if (i is DMethod && i.Name == "opCall")
								substitutionList.Add(DResolver.HandleNodeMatch(i, ctxt, resultBase: rr));
					}

				if (substitutionList.Count > 0)
					nonAliases = substitutionList.ToArray();

				res.ResolvedTypesOrMethods = nonAliases;
			}

			return res;
		}

		public static IExpression SearchForMethodCallsOrTemplateInstances(IStatement Statement, CodeLocation Caret)
		{
			IExpression curExpression = null;
			INode curDeclaration = null;

			/*
			 * Step 1: Step down the statement hierarchy to find the stmt that's most next to Caret
			 * Note: As long we haven't found any fitting elements, go on searching
			 */
			while (Statement != null && curExpression == null && curDeclaration == null)
			{
				if (Statement is IExpressionContainingStatement)
				{
					var exprs = (Statement as IExpressionContainingStatement).SubExpressions;

					if (exprs != null && exprs.Length > 0)
						foreach (var expr in exprs)
							if (expr != null && Caret >= expr.Location && Caret <= expr.EndLocation)
							{
								curExpression = expr;
								break;
							}
				}

				if (Statement is IDeclarationContainingStatement)
				{
					var decls = (Statement as IDeclarationContainingStatement).Declarations;

					if (decls != null && decls.Length > 0)
						foreach (var decl in decls)
							if (decl != null && Caret >= decl.StartLocation && Caret <= decl.EndLocation)
							{
								curDeclaration = decl;
								break;
							}
				}

				if (Statement is StatementContainingStatement)
				{
					var stmts = (Statement as StatementContainingStatement).SubStatements;

					bool foundDeeperStmt = false;

					if (stmts != null && stmts.Length > 0)
						foreach (var stmt in stmts)
							if (stmt != null && Caret >= stmt.StartLocation && Caret <= stmt.EndLocation)
							{
								foundDeeperStmt = true;
								Statement = stmt;
								break;
							}

					if (foundDeeperStmt)
						continue;
				}

				break;
			}

			if (curDeclaration == null && curExpression == null)
				return null;


			/*
			 * Step 2: If a declaration was found, check for its inner elements
			 */
			if (curDeclaration != null)
			{
				if (curDeclaration is DVariable)
				{
					var dv = curDeclaration as DVariable;

					if (dv.Initializer != null && Caret >= dv.Initializer.Location && Caret <= dv.Initializer.EndLocation)
						curExpression = dv.Initializer;
				}

				//TODO: Watch the node's type! Over there, there also can be template instances..
			}

			if (curExpression != null)
			{
				IExpression curMethodOrTemplateInstance = null;

				while (curExpression != null)
				{
					if (!(curExpression.Location <= Caret || curExpression.EndLocation >= Caret))
						break;

					if (curExpression is PostfixExpression_MethodCall)
						curMethodOrTemplateInstance = curExpression;

					else if (curExpression is TemplateInstanceExpression)
						curMethodOrTemplateInstance = curExpression;
					else if (curExpression is PostfixExpression_Access &&
						(curExpression as PostfixExpression_Access).TemplateOrIdentifier is TemplateInstanceExpression)
						curMethodOrTemplateInstance = curExpression.ExpressionTypeRepresentation as TemplateInstanceExpression;

					else if (curExpression is NewExpression)
						curMethodOrTemplateInstance = curExpression;

					if (curExpression is ContainerExpression)
					{
						var currentContainer = curExpression as ContainerExpression;

						var subExpressions = currentContainer.SubExpressions;
						bool foundMatch = false;
						if (subExpressions != null && subExpressions.Length > 0)
							foreach (var se in subExpressions)
								if (se != null && Caret >= se.Location && Caret <= se.EndLocation)
								{
									curExpression = se;
									foundMatch = true;
									break;
								}

						if (foundMatch)
							continue;
					}
					break;
				}

				return curMethodOrTemplateInstance;
			}


			return null;
		}
	}
}
