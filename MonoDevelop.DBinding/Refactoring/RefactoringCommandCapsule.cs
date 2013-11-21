//
// RefactoringCommandCapsule.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;
using MonoDevelop.Ide;
using D_Parser.Refactoring;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;

namespace MonoDevelop.D.Refactoring
{
	class RefactoringCommandCapsule
	{
		public ResolutionContext ctxt;
		public D_Parser.Completion.IEditorData ed;
		public AbstractType[] lastResults;

		AbstractType GetResult()
		{
			if (lastResults.Length == 1)
				return lastResults [0];

			return ImportSymbolSelectionDlg.Show (lastResults, GettextCatalog.GetString("Select symbol"), 
				(object o) => o is DSymbol ? (o as DSymbol).Definition.ToString(false, true) : o.ToString());
		}

		public void OpenDDoc()
		{
			AbstractType res = GetResult();
			if (res == null)
				return;

			var url=DDocumentationLauncher.GetReferenceUrl(res, ctxt, ed.CaretLocation);

			if (url != null)
				Refactoring.DDocumentationLauncher.LaunchRelativeDUrl(url);
		}

		public void GotoDeclaration()
		{
			AbstractType res = GetResult();
			if (res != null)
				GotoDeclaration(res);
		}

		internal static void GotoDeclaration(AbstractType lastResult)
		{
			var n = DResolver.GetResultMember (lastResult);
			// Redirect to the actual definition on import bindings
			if (n is ImportSymbolAlias)
				n = DResolver.GetResultMember ((lastResult as DSymbol).Base);

			GotoDeclaration (n);
		}

		public static void GotoDeclaration(INode n)
		{
			if (n != null && n.NodeRoot is DModule)
				MonoDevelop.Ide.IdeApp.Workbench.OpenDocument(
					((DModule)n.NodeRoot).FileName,
					n.Location.Line,
					n.Location.Column);
		}

		public void FindReferences(bool allOverloads = false)
		{
			AbstractType res = GetResult();
			INode n;
			if (res != null && (n = DResolver.GetResultMember(res)) != null)
				ReferenceFinding.StartReferenceSearchAsync(n, allOverloads);
		}

		public void FindDerivedClasses()
		{
			var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor(true, true);

			foreach (var t in lastResults)
			{
				var t_ = DResolver.StripMemberSymbols(t);
				if (!(t_ is ClassType || t_ is InterfaceType))
					continue;

				var ds = DResolver.StripMemberSymbols(t) as TemplateIntermediateType;
				var dc = ds.Definition;
			}

			monitor.EndTask();
		}

		public void RenameSymbol()
		{
			AbstractType res = GetResult();
			INode n;
			if (res != null && (n = DResolver.GetResultMember(res)) != null &&
				DRenameRefactoring.CanRenameNode(n))
				new DRenameHandler().Start(n);
		}

		#region Imports
		public void TryImportMissingSymbol()
		{
			try
			{
				ImportGen_CustImplementation.CreateImportDirectiveForHighlightedSymbol(ed, new ImportGen_CustImplementation(IdeApp.Workbench.ActiveDocument));
			}
			catch (Exception ex)
			{
				MessageService.ShowError(IdeApp.Workbench.RootWindow,GettextCatalog.GetString("Error during import directive creation"),ex.Message);
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
				return ImportSymbolSelectionDlg.Show (results, GettextCatalog.GetString("Select symbol to import"));
			}

			public override void InsertIntoCode(CodeLocation location, string codeToInsert)
			{
				doc.Editor.Insert(doc.Editor.GetLine(location.Line).Offset, codeToInsert.Trim() + doc.Editor.EolMarker);
			}
		}
		#endregion
	}
}

