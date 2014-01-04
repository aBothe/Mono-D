using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Refactoring;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using Gtk;
using MonoDevelop.CodeActions;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.TypeSystem;
using D_Parser.Completion;
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
			if (res != null && refCtxt.resultResolutionAttempt == DResolver.NodeResolutionAttempt.RawSymbolLookup) {
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
				var editor = refCtxt.Doc.Editor;
				ImportStmtCreation.GenerateImportStatementForNode (dn, refCtxt.ed, (loc, s) => {
					editor.Insert (editor.LocationToOffset (loc.Line, loc.Column), s);
				});
			}
        }
	}
}
