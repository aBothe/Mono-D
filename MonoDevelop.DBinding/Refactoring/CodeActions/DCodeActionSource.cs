using System.Collections.Generic;
using MonoDevelop.CodeActions;

namespace MonoDevelop.D.Refactoring.CodeActions
{
	public class DCodeActionSource : ICodeActionProviderSource
	{
		readonly List<CodeActionProvider> providers = new List<CodeActionProvider> ();

		public DCodeActionSource ()
		{
			providers.Add (new ImportSymbolAction ());
		}

		public IEnumerable<CodeActionProvider> GetProviders ()
		{
			return providers;
		}
	}
}
