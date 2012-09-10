using System;
using D_Parser.Dom;
using D_Parser.Refactoring;
using Gtk;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Refactoring
{
	public class SymbolImportRefactoring
	{
		public static void CreateImportStatementForCurrentCodeContext()
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			var edData = DResolverWrapper.GetEditorData(doc);

			try
			{
				ImportGen_CustImplementation.CreateImportDirectiveForHighlightedSymbol(edData, new ImportGen_CustImplementation(doc));
			}
			catch (Exception ex)
			{
				MessageService.ShowError(IdeApp.Workbench.RootWindow,"Error during import directive creation",ex.Message);
			}
		}

		class ImportGen_CustImplementation : ImportDirectiveCreator
		{
			public ImportGen_CustImplementation(Document doc)
			{
				this.doc = doc;
			}

			Document doc;

			public override INode HandleMultipleResults(INode[] results)
			{
				var dlg = new ImportSymbolSelectionDlg(results);

				if(MessageService.RunCustomDialog(dlg, Ide.IdeApp.Workbench.RootWindow) != (int)ResponseType.Ok)
					return null;

				var n = dlg.SelectedNode;
				dlg.Destroy();
				return n;
			}

			public override void InsertIntoCode(CodeLocation location, string codeToInsert)
			{
				doc.Editor.Insert(doc.Editor.GetLine(location.Line).Offset, codeToInsert.Trim() + doc.Editor.EolMarker);
			}
		}
	}
}
