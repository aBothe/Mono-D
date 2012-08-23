using System.Collections.Generic;
using D_Parser.Dom;
using System.Collections;

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
		IList<T> rawList;
		HashSet<string> names;
		/// <summary>
		/// Contains items that passed the filter successfully.
		/// </summary>
		public List<T> filteredList=new List<T>();

		public MatchFilterVisitor(ResolverContextStack ctxt, IList<T> rawList) : base(ctxt) {
			this.rawList = rawList;

			names = new HashSet<string>();
			foreach (var i in rawList)
				names.Add(i.Name);
		}

		public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			foreach (var n in names)
			{
				var ch = bn[n];
				if (ch != null)
					foreach (var c in ch)
						yield return c;
			}
		}

		protected override bool HandleItem(INode n)
		{
			if (n is T && rawList.Contains((T)n))
				filteredList.Add((T)n);

			return false;
		}
	}
}
