using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public bool ResolveBaseTypes = true;
		public bool ResolveAliases = true;

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;

			ResolveBaseTypes = other.ResolveBaseTypes;
			ResolveAliases = other.ResolveAliases;
		}
	}

}
