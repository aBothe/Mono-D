using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	[Flags]
	public enum MemberTypes
	{
		Imports = 1,
		Variables = 1 << 1,
		Methods = 1 << 2,
		Types = 1 << 3,
		Keywords = 1 << 4,

		All = Imports | Variables | Methods | Types | Keywords
	}

	public class ItemEnumeration : RootsEnum
	{
		protected ItemEnumeration(ResolverContext ctxt): base(ctxt) { }

		public static IEnumerable<INode> EnumAllAvailableMembers(IBlockNode ScopedBlock
			, IStatement ScopedStatement,
			CodeLocation Caret,
			IEnumerable<IAbstractSyntaxTree> CodeCache,
			MemberTypes VisibleMembers)
		{
			return EnumAllAvailableMembers(new ResolverContext
			{
				ParseCache = CodeCache,
				ImportCache = DResolver.ResolveImports(ScopedBlock.NodeRoot as DModule, CodeCache),
				ScopedBlock = ScopedBlock,
				ScopedStatement = ScopedStatement
			}, Caret, VisibleMembers);
		}

		public static IEnumerable<INode> EnumAllAvailableMembers(
			ResolverContext ctxt,
			CodeLocation Caret,
			MemberTypes VisibleMembers)
		{
			var en = new ItemEnumeration(ctxt);

			en.IterateThroughScopeLayers(Caret, VisibleMembers);

			return en.Nodes.Count <1 ? null : en.Nodes;
		}

		public List<INode> Nodes = new List<INode>();
		protected override void HandleItem(INode n)
		{
			Nodes.Add(n);
		}

		protected override void HandleItems(IEnumerable<INode> nodes)
		{
			Nodes.AddRange(nodes);
		}
	}
}
