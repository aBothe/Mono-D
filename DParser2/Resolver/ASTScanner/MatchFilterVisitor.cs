using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// Filters out items that are not available in the current code context.
	/// Takes a list of unfiltered items, wanders through the code and checks if each item occurs at least once in the import tree.
	/// Items which passed the occurrency check are stored in the "filteredList" property.
	/// </summary>
	public class MatchFilterVisitor<T>: AbstractVisitor where T : INode
	{
		/// <summary>
		/// Contains items that shall be tested for existence in the current scope tree.
		/// </summary>
		public IList<T> rawList;
		/// <summary>
		/// Contains items that passed the filter successfully.
		/// </summary>
		public List<T> filteredList=new List<T>();

		public MatchFilterVisitor(ResolverContextStack ctxt) : base(ctxt) { }

		protected override bool HandleItem(INode n)
		{
			if (n is T && rawList.Contains((T)n))
				filteredList.Add((T)n);

			return false;
		}
	}
}
