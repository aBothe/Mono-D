using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
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
		public static ArgumentsResolutionResult ResolveArgumentContext(
			IEditorData data,
			ResolverContextStack ctxt)
		{
			var e = DResolver.GetScopedCodeObject(data, ctxt, DResolver.AstReparseOptions.ReturnRawParsedExpression);
			
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
				ParsedExpression = e as IExpression
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
						if (data.CaretLocation >= arg.Location && data.CaretLocation <= arg.EndLocation)
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

				res.ResolvedTypesOrMethods = ExpressionTypeResolver.Resolve(acc.PostfixForeExpression, ctxt);

				if (res.ResolvedTypesOrMethods == null)
					return res;

				if (acc.AccessExpression is NewExpression)
					CalculateCurrentArgument(acc.AccessExpression as NewExpression, res, data.CaretLocation, ctxt, res.ResolvedTypesOrMethods);
			}
			// 3)
			else if (e is TemplateInstanceExpression)
			{
				var templ = e as TemplateInstanceExpression;

				res.IsTemplateInstanceArguments = true;

				res.ResolvedTypesOrMethods = TypeDeclarationResolver.ResolveIdentifier(templ.TemplateIdentifier.Id, ctxt, e);

				if (templ.Arguments != null)
				{
					int i = 0;
					foreach (var arg in templ.Arguments)
					{
						if (data.CaretLocation >= arg.Location && data.CaretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}
			}
			else if (e is NewExpression)
				CalculateCurrentArgument(e as NewExpression, res, data.CaretLocation, ctxt);

			/*
			 * alias int function(int a, bool b) myDeleg;
			 * alias myDeleg myDeleg2;
			 * 
			 * myDeleg dg;
			 * 
			 * dg( -- it's not needed to have myDeleg but the base type for what it stands for
			 * 
			 * ISSUE:
			 * myDeleg( -- not allowed though
			 * myDeleg2( -- allowed neither!
			 */
			if (res.ResolvedTypesOrMethods != null)
			{
				var finalResults = new List<ResolveResult>();

				foreach (var _r in res.ResolvedTypesOrMethods)
				{
					var r = _r;
					while (r is MemberResult && !(((MemberResult)r).Node is DMethod))
					{
						var mr = (MemberResult)r;

						if (mr.MemberBaseTypes == null || mr.MemberBaseTypes.Length == 0)
							break;

						r = mr.MemberBaseTypes[0];
					}
					finalResults.Add(r);
				}

				res.ResolvedTypesOrMethods = finalResults.ToArray();
			}

			return res;
		}

		static void CalculateCurrentArgument(NewExpression nex, 
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

		public static ArgumentsResolutionResult ResolveArgumentContext(IEditorData editorData)
		{
			return ResolveArgumentContext(editorData, ResolverContextStack.Create(editorData));
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
