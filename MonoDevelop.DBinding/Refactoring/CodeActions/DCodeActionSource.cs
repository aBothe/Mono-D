using System.Collections.Generic;
using MonoDevelop.CodeActions;

namespace MonoDevelop.D.Refactoring.CodeActions
{
	public class DCodeActionSource : ICodeActionProviderSource
	{
		readonly List<CodeActionProvider> providers = new List<CodeActionProvider> ();

		public DCodeActionSource ()
		{
			//TODO: Add nice code refactorings like "Extract Method", "Create Class of selected identifier", "Optimize out while(true)" and so on..
		}

		public IEnumerable<CodeActionProvider> GetProviders ()
		{
			return providers;
		}
	}
}
