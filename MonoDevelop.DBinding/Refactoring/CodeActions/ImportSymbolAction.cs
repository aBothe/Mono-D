using D_Parser.Dom;
using D_Parser.Refactoring;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.CodeActions;
using MonoDevelop.Ide.Gui;
using System.Collections.Generic;
using MonoDevelop.Ide.TypeSystem;
using ICSharpCode.NRefactory;

namespace MonoDevelop.D.Refactoring.CodeActions
{
    /// <summary>
    /// Provides a CodeAction for fast import in D language
    /// </summary>
    public class ImportSymbolAction : CodeActionProvider
    {
        public ImportSymbolAction()
        {
            this.Title = "Import symbol";
            this.MimeType = "text/x-d";
            this.Description = "";
        }

		public override IEnumerable<CodeAction> GetActions(Document document, object refactoringContext, TextLocation loc, System.Threading.CancellationToken cancellationToken)
        {
            if (!DLanguageBinding.IsDFile(document.FileName)) 
				yield break;

			var refCtxt = refactoringContext as DRefactoringContext;

			var res = refCtxt.CurrentResults;
			if (res != null && refCtxt.resultResolutionAttempt == LooseResolution.NodeResolutionAttempt.RawSymbolLookup) {
				foreach (var t in res)
					if (t is DSymbol)
						yield return new InnerAction ((t as DSymbol).Definition, refCtxt);
			}
        }

        /// <summary>
        /// Represents position in menu of one possible import
        /// </summary>
        class InnerAction : CodeAction
        {
			DNode dn;
			DRefactoringContext refCtxt;

			public InnerAction(DNode dn, DRefactoringContext drefCtxt)
            {
				this.refCtxt = drefCtxt;
				this.dn = dn;
				this.Title = "import " + (dn.NodeRoot as DModule).ModuleName + ";";
            }

			public override string ToString ()
			{
				return Title;
			}

			public override void Run (IRefactoringContext _c, object _s)
			{
				ImportStmtCreation.GenerateImportStatementForNode (dn, refCtxt.ed, new TextDocumentAdapter(refCtxt.Doc.Editor));
			}
        }
	}
}
