using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
{
	public class ArgumentsResolutionResult
	{
		public bool IsMethodArguments;
		public bool IsTemplateInstanceArguments;

		public IExpression ParsedExpression;

		/// <summary>
		/// Usually some part of the ParsedExpression.
		/// For instance in a PostfixExpression_MethodCall it'd be the PostfixForeExpression.
		/// </summary>
		public object MethodIdentifier;

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

	public class ParameterInsightResolution
	{
		/// <summary>
		/// Reparses the given method's fucntion body until the cursor position,
		/// searches the last occurring method call or template instantiation,
		/// counts its already typed arguments
		/// and returns a wrapper containing all the information.
		/// </summary>
		public static ArgumentsResolutionResult LookupArgumentRelatedStatement(
			string code, 
			int caret, 
			CodeLocation caretLocation,
			ResolverContextStack ctxt)
		{
			IStatement scopedStatement = null;
			var MethodScope = ctxt.ScopedBlock as DMethod;

			if (MethodScope == null)
				return null;

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
					ctxt.ScopedStatement = scopedStatement = curMethodBody.SearchStatementDeeply(caretLocation);
				else
					return null;
			}

			if (scopedStatement == null)
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
			var res = new ArgumentsResolutionResult() { 
				ParsedExpression = e
			};

			// 1), 2)
			if (e is PostfixExpression_MethodCall)
			{
				res.IsMethodArguments = true;
				var call = (PostfixExpression_MethodCall) e;

				res.ResolvedTypesOrMethods = ExpressionTypeResolver.Resolve(call.PostfixForeExpression, ctxt);

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

			}
			else if (e is PostfixExpression_Access)
			{
				var acc = e as PostfixExpression_Access;

				var baseTypes = ExpressionTypeResolver.Resolve(acc.PostfixForeExpression, ctxt);

				if (baseTypes == null)
					return res;

				if (acc.AccessExpression is NewExpression)
					Handle(acc.AccessExpression as NewExpression, res, caretLocation, ctxt, baseTypes);
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
			}
			else if (e is NewExpression)
				Handle(e as NewExpression, res, caretLocation, ctxt);

			return res;
		}

		static void Handle(NewExpression nex, 
			ArgumentsResolutionResult res, 
			CodeLocation caretLocation, 
			ResolverContextStack ctxt,
			IEnumerable<ResolveResult> resultBases=null)
		{
			if (nex.Arguments != null)
			{
				int i = 0;
				foreach (var arg in nex.Arguments)
				{
					if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
					{
						res.CurrentlyTypedArgumentIndex = i;
						break;
					}
					i++;
				}
			}
		}

		public static ArgumentsResolutionResult ResolveArgumentContext(
			string code,
			int caretOffset,
			CodeLocation caretLocation,
			DMethod MethodScope,
			D_Parser.Misc.ParseCacheList parseCache)
		{
			IStatement stmt = null;
			var ctxt = new ResolverContextStack(parseCache, new ResolverContext { 
				ScopedBlock = DResolver.SearchBlockAt(MethodScope, caretLocation, out stmt),
				ScopedStatement = stmt
			});

			return LookupArgumentRelatedStatement(code, caretOffset, caretLocation, ctxt);
		}

		static IExpression SearchForMethodCallsOrTemplateInstances(IStatement Statement, CodeLocation Caret)
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

					else if (curExpression is PostfixExpression_Access)
					{
						var acc = curExpression as PostfixExpression_Access;

						if (acc.AccessExpression is TemplateInstanceExpression)
							curMethodOrTemplateInstance = (TemplateInstanceExpression)acc.AccessExpression;
						else if (acc.AccessExpression is NewExpression)
							curMethodOrTemplateInstance = (NewExpression)acc.AccessExpression;
					}

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
