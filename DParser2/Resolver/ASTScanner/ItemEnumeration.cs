using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Misc;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// A whitelisting filter for members to show in completion menus.
	/// </summary>
	[Flags]
	public enum MemberFilter
	{
		Imports = 1,
		Variables = 1 << 1,
		Methods = 1 << 2,
		Types = 1 << 3,
		Keywords = 1 << 4,

		All = Imports | Variables | Methods | Types | Keywords
	}

	public class ItemEnumeration : AbstractAstScanner
	{
		protected ItemEnumeration(ResolverContextStack ctxt): base(ctxt) { }

		public static IEnumerable<INode> EnumAllAvailableMembers(IBlockNode ScopedBlock
			, IStatement ScopedStatement,
			CodeLocation Caret,
			ParseCacheList CodeCache,
			MemberFilter VisibleMembers)
		{
			return EnumAllAvailableMembers(new ResolverContextStack(CodeCache,new ResolverContext
			{
				ScopedBlock = ScopedBlock,
				ScopedStatement = ScopedStatement
			}), 
			Caret, 
			VisibleMembers);
		}

		public static IEnumerable<INode> EnumAllAvailableMembers(
			ResolverContextStack ctxt,
			CodeLocation Caret,
			MemberFilter VisibleMembers)
		{
			var en = new ItemEnumeration(ctxt);

			en.IterateThroughScopeLayers(Caret, VisibleMembers);

			return en.Nodes.Count <1 ? null : en.Nodes;
		}

		public List<INode> Nodes = new List<INode>();
		protected override bool HandleItem(INode n)
		{
			Nodes.Add(n);
			return false;
		}

		protected override bool HandleItems(IEnumerable<INode> nodes)
		{
			Nodes.AddRange(nodes);
			return false;
		}
	}
}
