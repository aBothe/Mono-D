using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Refactoring
{
	public class ReferenceFinder
	{
		#region Properties
		ResolverContextStack ctxt;
		ParseCacheList parseCaches;
		List<ISyntaxRegion> matchedReferences = new List<ISyntaxRegion>();
		IAbstractSyntaxTree ast;
		List<string> namesToCompareWith;
		INode[] declarationsToCompareWith;
		#endregion

		protected ReferenceFinder() { }

		/// <summary>
		/// 
		/// </summary>
		/// <returns>Array of expressions/type declarations referencing the given node</returns>
		public static List<ISyntaxRegion> ScanNodeReferencesInModule(
			IAbstractSyntaxTree scannedFileAST,
			ParseCacheList parseCache,
			params INode[] declarationsToCompareWith)
		{
			var namesToCompareWith = new List<string>();

			foreach (var n in declarationsToCompareWith)
				namesToCompareWith.Add(n.Name);

			var identifiers=CodeSymbolsScanner.IdentifierScan.ScanForTypeIdentifiers(scannedFileAST);

			var reff = new ReferenceFinder { 
				ast=scannedFileAST,
				parseCaches=parseCache,
				declarationsToCompareWith=declarationsToCompareWith,
				namesToCompareWith=namesToCompareWith
			};

			foreach (var o in identifiers)
				reff.HandleSyntaxNode(o);

			return reff.matchedReferences;
		}

		void HandleSyntaxNode(ISyntaxRegion o)
		{
			ISyntaxRegion id = null;

			if (o is ITypeDeclaration)
				id = ExtractId((ITypeDeclaration)o);
			else if (o is IExpression)
				id = ExtractId((IExpression)o);

			if (id == null)
				return;

			ResolveAndTestIdentifierObject(o,id);
		}

		/// <summary>
		/// Scans a type declaration object for possible name matches.
		/// Uses the namesToCompareWith list to decide if worth a resolution or not.
		/// </summary>
		/// <returns>Returns null if nothing nearly matching was found.</returns>
		ISyntaxRegion ExtractId(ITypeDeclaration td)
		{
			IdentifierDeclaration id = null;

			while (td != null && id == null)
			{
				if (td is IdentifierDeclaration)
					id = (td as IdentifierDeclaration);
				else if (td is TemplateInstanceExpression)
					id = (td as TemplateInstanceExpression).TemplateIdentifier;

				if (id!=null && namesToCompareWith.Contains(id.Id))
					return td;
				
				id = null;
				td = td.InnerDeclaration;
			}

			return null;
		}

		ISyntaxRegion ExtractId(IExpression x)
		{
			if (x is IdentifierExpression)
			{
				var idx = (IdentifierExpression)x;

				if (namesToCompareWith.Contains((string)idx.Value))
					return idx;
			}
			else if (x is PostfixExpression_Access)
			{
				var pfa = (PostfixExpression_Access)x;

				if (pfa.AccessExpression is IdentifierExpression)
				{
					var idx = (IdentifierExpression)pfa.AccessExpression;

					if(namesToCompareWith.Contains((string)idx.Value))
						return idx;
				}
				else if (pfa.AccessExpression is NewExpression)
				{
					var nt = ((NewExpression)pfa.AccessExpression).Type;

					return ExtractId(nt);
				}
				else if (pfa.AccessExpression is TemplateInstanceExpression)
				{
					var tix = (TemplateInstanceExpression)pfa.AccessExpression;

					if (namesToCompareWith.Contains(tix.TemplateIdentifier.ToString()))
						return tix;
				}
				
				return ExtractId(pfa.PostfixForeExpression);
			}

			return null;
		}

		/// <summary>
		/// Resolve the symbol to which the identifier is related to
		/// </summary>
		void ResolveAndTestIdentifierObject(ISyntaxRegion o,ISyntaxRegion idObject=null)
		{
			if (idObject == null)
				idObject = o;

			UpdateOrCreateIdentifierContext(o);

			var r = o is ITypeDeclaration ?
				TypeDeclarationResolver.ResolveSingle(o as ITypeDeclaration, ctxt) :
				(o is IExpression ? Evaluation.EvaluateType((IExpression)o, ctxt) : null);

			if (r != null)
				HandleResolveResult(r, o, idObject);
		}

		/// <summary>
		/// Get the context of the used identifier
		/// </summary>
		void UpdateOrCreateIdentifierContext(ISyntaxRegion o)
		{
			if (ctxt == null)
			{
				IStatement stmt = null;
				ctxt = new ResolverContextStack(parseCaches, new ResolverContext
				{
					ScopedBlock = DResolver.SearchBlockAt(ast, o.Location, out stmt),
					ScopedStatement = stmt
				});
			}
			else
				ctxt.ScopedBlock = DResolver.SearchBlockAt(ast, o.Location, out ctxt.CurrentContext.ScopedStatement);
		}

		void HandleResolveResult(AbstractType rr, ISyntaxRegion o, ISyntaxRegion idObject)
		{
			var tsym = rr;

			// Track down result bases until one associated to 'o' has been found - and finally mark it as a reference
			while (tsym is DerivedDataType && tsym.DeclarationOrExpressionBase != o)
				tsym = ((DerivedDataType)tsym).Base;

			// Get the associated declaration node
			var targetSymbolNode = DResolver.GetResultMember(tsym);

			if (targetSymbolNode == null)
				return;

			// Compare with the members whose references shall be looked up
			if (declarationsToCompareWith.Length == 1 ?
				targetSymbolNode == declarationsToCompareWith[0] :
				declarationsToCompareWith.Contains(targetSymbolNode))
			{
				// ... Reference found!
				matchedReferences.Add(idObject);
			}
		}

		public class IdLocationComparer : IComparer<ISyntaxRegion>
		{
			bool rev;
			public IdLocationComparer(bool reverse = false)
			{
				rev = reverse;
			}

			public int Compare(ISyntaxRegion x, ISyntaxRegion y)
			{
				if (x == null || y == null || y==x)
					return 0;

				return (rev? x.Location<y.Location : x.Location>y.Location)?1:-1;
			}
		}
	}
}
