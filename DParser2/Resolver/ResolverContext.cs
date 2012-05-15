using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public ResolutionOptions Options = ResolutionOptions.Default;

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;
			Options = other.Options;
		}
	}

}
