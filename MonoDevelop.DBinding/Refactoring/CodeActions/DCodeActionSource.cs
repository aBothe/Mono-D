using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.CodeActions;
namespace MonoDevelop.D.Refactoring.CodeActions
{
    public class DCodeActionSource : ICodeActionProviderSource
    {
        List<CodeActionProvider> providers;
        public DCodeActionSource()
        {
            providers = new List<CodeActionProvider>();
            providers.Add(new ImportSymbol());
        }
        public IEnumerable<CodeActionProvider> GetProviders()
        {
            return providers;
        }
    }
}
