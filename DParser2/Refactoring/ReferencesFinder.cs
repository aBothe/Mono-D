using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Refactoring
{
	public class ReferencesFinder : DeepASTVisitor
	{
		#region Properties
		readonly ResolverContextStack ctxt;
		readonly List<ISyntaxRegion> l = new List<ISyntaxRegion>();
		readonly INode symbol;
		readonly IAbstractSyntaxTree ast;
		readonly string searchId;

		/// <summary>
		/// Used when searching references of a variable.
		/// </summary>
		readonly bool handleSingleIdentifiersOnly;
		#endregion

		#region Constructor / External
		ReferencesFinder(INode symbol, IAbstractSyntaxTree ast, ResolverContextStack ctxt)
		{
			this.ast = ast;
			this.symbol = symbol;
			searchId = symbol.Name;
			this.handleSingleIdentifiersOnly = symbol is DVariable /* && ((DVariable)symbol).IsAlias */;
			this.ctxt = ctxt;
		}

		public static IEnumerable<ISyntaxRegion> Scan(INode symbol, ResolverContextStack ctxt)
		{
			return Scan(symbol.NodeRoot as IAbstractSyntaxTree, symbol, ctxt);
		}

		/// <summary>
		/// </summary>
		/// <param name="ast">The syntax tree to scan</param>
		/// <param name="symbol">Might not be a child symbol of ast</param>
		/// <param name="ctxt">The context required to search for symbols</param>
		/// <returns></returns>
		public static IEnumerable<ISyntaxRegion> Scan(IAbstractSyntaxTree ast, INode symbol, ResolverContextStack ctxt)
		{
			if (ast == null || symbol == null || ctxt == null)
				return null;

			ctxt.PushNewScope(ast);

			var f = new ReferencesFinder(symbol, ast, ctxt);

			f.S(ast);

			ctxt.Pop();

			return f.l;
		}
		#endregion

		protected override void OnScopeChanged(Dom.Statements.IStatement scopedStatement)
		{
			ctxt.CurrentContext.ScopedStatement = scopedStatement;
		}

		protected override void OnScopeChanged(IBlockNode scopedBlock)
		{
			ctxt.CurrentContext.ScopedBlock = scopedBlock;
		}

		protected override void Handle(ISyntaxRegion o)
		{
			Handle(o,null);
		}

		protected void Handle(ISyntaxRegion o, DSymbol resolvedSymbol)
		{
			if (o is IdentifierDeclaration)
			{
				var id = (IdentifierDeclaration)o;

				if (id.Id != searchId)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = TypeDeclarationResolver.ResolveSingle(id, ctxt) as DSymbol;
			}
			else if (o is TemplateInstanceExpression)
			{
				var tix = (TemplateInstanceExpression)o;

				if (tix.TemplateIdentifier.Id != searchId)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = Evaluation.EvaluateType(tix, ctxt) as DSymbol;
			}
			else if (o is IdentifierExpression)
			{
				var id = (IdentifierExpression)o;

				if ((string)id.Value != searchId)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = Evaluation.EvaluateType(id, ctxt) as DSymbol;
			}
			else if (o is PostfixExpression_Access)
			{
				var acc = (PostfixExpression_Access)o;

				if ((acc.AccessExpression is IdentifierExpression &&
				(string)((IdentifierExpression)acc.AccessExpression).Value != searchId) ||
				(acc.AccessExpression is TemplateInstanceExpression &&
				(string)((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id != searchId))
				{
					Handle(acc.PostfixForeExpression, null);
					return;
				}
				else if (acc.AccessExpression is NewExpression)
				{
					var nex = (NewExpression)acc.AccessExpression;

					if ((nex.Type is IdentifierDeclaration &&
						((IdentifierDeclaration)nex.Type).Id != searchId) ||
						(nex.Type is TemplateInstanceExpression &&
						(string)((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id != searchId))
					{
						Handle(acc.PostfixForeExpression, null);
						return;
					}
					// Are there other types to test for?
				}

				var s = resolvedSymbol ?? Evaluation.EvaluateType(acc, ctxt) as DerivedDataType;

				if (s is DSymbol)
				{
					if (((DSymbol)s).Definition == symbol)
						l.Add(acc.AccessExpression);
				}
				else if (s == null || !(s.Base is DSymbol))
					return;

				// Scan down for other possible symbols
				Handle(acc.PostfixForeExpression, s.Base as DSymbol);
				return;
			}

			// The resolved node must be equal to the symbol definition that is looked for.
			if (resolvedSymbol == null ||
				resolvedSymbol.Definition != symbol)
				return;

			l.Add(o);
		}
	}
}
