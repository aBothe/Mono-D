using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Resolver.ASTScanner
{
	public class ReferencesFinder
	{
		#region Properties
		readonly List<ISyntaxRegion> l = new List<ISyntaxRegion>();
		readonly INode symbol;
		readonly string searchId;
		/// <summary>
		/// Used when searching references of a variable.
		/// </summary>
		readonly bool handleSingleIdentifiersOnly;
		#endregion

		#region Ctor/IO
		public static IEnumerable<ISyntaxRegion> Scan(INode symbol)
		{
			return Scan(symbol.NodeRoot as IAbstractSyntaxTree, symbol);
		}

		public static IEnumerable<ISyntaxRegion> Scan(IAbstractSyntaxTree ast, INode symbol)
		{
			return null;
		}

		private ReferencesFinder(INode symbol)
		{
			this.symbol = symbol;
			searchId = symbol.Name;
			handleSingleIdentifiersOnly = symbol is DVariable;
		}
		#endregion
	}
}
