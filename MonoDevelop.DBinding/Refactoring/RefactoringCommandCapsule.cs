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
using MonoDevelop.Ide.FindInFiles;
using System.Threading;
using MonoDevelop.D.Resolver;
using System.Collections.Generic;
using System.Text;


namespace MonoDevelop.D.Refactoring
{
	class RefactoringCommandCapsule
	{
		public ResolutionContext ctxt;
		public D_Parser.Completion.IEditorData ed;
		public AbstractType[] lastResults;
		public DResolver.NodeResolutionAttempt resultResolutionAttempt;
		public Document lastDoc;

		public bool Update(Document doc = null)
		{
			lastResults = DResolverWrapper.ResolveHoveredCodeLoosely(out ctxt, out ed, out resultResolutionAttempt, lastDoc = (doc ?? IdeApp.Workbench.ActiveDocument));

			return lastResults != null && lastResults.Length > 0;
		}

		AbstractType GetResult()
		{
			if (lastResults.Length == 1)
				return lastResults [0];

			return ImportSymbolSelectionDlg.Show (lastResults, GettextCatalog.GetString("Select symbol"), 
				(object o) => o is DSymbol ? (o as DSymbol).Definition.ToString(false, true) : o.ToString());
		}

		public void GotoDeclaration()
		{
			AbstractType res = GetResult();
			if (res != null)
				GotoDeclaration(res);
		}

		internal static void GotoDeclaration(AbstractType lastResult)
		{
			GotoDeclaration (DResolver.GetResultMember (lastResult, true));
		}

		public static void GotoDeclaration(INode n)
		{
			if (n != null && n.NodeRoot is DModule)
				IdeApp.Workbench.OpenDocument(
					((DModule)n.NodeRoot).FileName,
					n.NameLocation.Line,
					n.NameLocation.Column);
		}

		public void FindReferences(bool allOverloads = false)
		{
			AbstractType res = GetResult();
			INode n;
			if (res != null && (n = DResolver.GetResultMember(res, true)) != null)
				ReferenceFinding.StartReferenceSearchAsync(n, allOverloads);
		}

		public void FindDerivedClasses()
		{
			ThreadPool.QueueUserWorkItem(FindDerivedClassesThread);
		}

		void FindDerivedClassesThread(object s)
		{
			var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor(true, true);
			monitor.BeginStepTask (GettextCatalog.GetString ("Find Derived Classes"), lastResults.Length,1);

			foreach (var t in lastResults)
			{
				var t_ = DResolver.StripMemberSymbols(t);
				if (!(t_ is ClassType || t_ is InterfaceType)) {
					monitor.Step (1);
					continue;
				}

				foreach (var res in ClassInterfaceDerivativeFinder.SearchForClassDerivatives(t_ as TemplateIntermediateType, ctxt)) {
					var dc = res.Definition;
					var file = (dc.NodeRoot as DModule).FileName;
					var targetDoc = TextFileProvider.Instance.GetTextEditorData(new FilePath(file));

					monitor.ReportResult(new SearchResult(new FileProvider(file, IdeApp.Workspace.GetProjectContainingFile(file)),
						targetDoc.LocationToOffset(dc.NameLocation.Line,dc.NameLocation.Column),dc.Name.Length));
				}
				monitor.Step (1);
			}

			monitor.EndTask();
			monitor.Dispose ();
		}

		public void RenameSymbol()
		{
			AbstractType res = GetResult();
			INode n;
			if (res != null && (n = DResolver.GetResultMember(res, true)) != null &&
				DRenameRefactoring.CanRenameNode(n))
				new DRenameHandler().Start(n);
		}

		public void TryImportMissingSymbol()
		{
			var t = GetResult ();
			if (t is DSymbol)
				ImportStmtCreation.GenerateImportStatementForNode ((t as DSymbol).Definition, ed, (loc, s) => {
					var doc = IdeApp.Workbench.ActiveDocument.Editor;
					doc.Insert (doc.LocationToOffset (loc.Line, loc.Column), s);
				});
		}

		public bool HasDBlockNodeSelected
		{
			get{
				return DResolver.SearchBlockAt (ctxt.CurrentContext.ScopedBlock.NodeRoot as IBlockNode, ed.CaretLocation) is DBlockNode; 
			}
		}

		class ImportComparer : IComparer<ImportStatement>
		{
			public static ITypeDeclaration ExtractPrimaryId(ImportStatement i)
			{
				if (i.Imports.Count != 0)
					return i.Imports [0].ModuleIdentifier;

				if(i.ImportBindList != null)
					return i.ImportBindList.Module.ModuleIdentifier;

				return null;
			}

			public int Compare (ImportStatement x, ImportStatement y)
			{
				if (x == y)
					return 0;
				var sx = ExtractPrimaryId (x);
				if (sx == null)
					return 0;
				var sy = ExtractPrimaryId (y);
				if (sy == null)
					return 0;
				return sx.ToString(true).CompareTo (sy.ToString(true));
			}
		}

		const string SortImportsSeparatePackagesFromEachOtherPropId = "MonoDevelop.D.SortImportsSeparatePackagesFromEachOther";
		public bool SortImportsSeparatePackagesFromEachOther
		{
			get{ return PropertyService.Get (SortImportsSeparatePackagesFromEachOtherPropId, false); }
			set{ PropertyService.Set (SortImportsSeparatePackagesFromEachOtherPropId, value); }
		}

		public void SortImports()
		{
			var scope = DResolver.SearchBlockAt (ctxt.CurrentContext.ScopedBlock.NodeRoot as IBlockNode, ed.CaretLocation) as DBlockNode;

			if (scope == null)
				return;

			// Get the first scope that contains import statements
			var importsToSort = new List<ImportStatement> ();
			while (scope != null && importsToSort.Count == 0) {
				foreach (var ss in scope.StaticStatements) {
					var iss = ss as ImportStatement;
					if (iss != null)
						importsToSort.Add (iss);
				}
			}

			if (importsToSort.Count == 0)
				return;

			CodeLocation firstLoc = CodeLocation.Empty, endLoc = CodeLocation.Empty;
			var editor = lastDoc.Editor.Document;
			using (editor.OpenUndoGroup (Mono.TextEditor.OperationType.Undefined)) {
				// Remove all of them from the document; Memorize where the first import was
				for (int i = importsToSort.Count - 1; i >= 0; i--) {
					var ss = importsToSort [i];
					firstLoc = ss.Location;
					endLoc = ss.EndLocation;

					if (ss.Attributes != null && ss.Attributes.Length > 0) {
						//ISSUE: Meta declaration attributes that are either section or block meta attributes will corrupt this calculation
						if(ss.Attributes[0].Location < firstLoc)
							firstLoc = ss.Attributes [0].Location;
						if(ss.Attributes [ss.Attributes.Length - 1].EndLocation > endLoc)
							endLoc = ss.Attributes [ss.Attributes.Length - 1].EndLocation;
					}

					var l1 = editor.LocationToOffset (firstLoc.Line, firstLoc.Column);
					var l2 = editor.LocationToOffset (endLoc.Line, endLoc.Column);
					var n = editor.TextLength - 1;

					// Remove indents and trailing semicolon.
					for (char c; l1 > 0 && ((c = editor.GetCharAt (l1-1)) == ' ' || c == '\t'); l1--);
					for (char c; l2 < n && ((c = editor.GetCharAt (l2+1)) == ' ' || c == '\t' || c == ';'); l2++);
					for (char c; l2 < n && ((c = editor.GetCharAt (l2+1)) == '\n' || c == '\r'); l2++);

					editor.Remove (l1, l2 - l1);
				}

				// Sort
				importsToSort.Sort (new ImportComparer ());

				// Write all imports beneath each other.
				var indent = editor.GetLineIndent (firstLoc.Line);
				var sb = new StringBuilder ();
				bool separatePackageRoots = SortImportsSeparatePackagesFromEachOther;
				ITypeDeclaration prevId = null;

				foreach (var i in importsToSort) {
					sb.Append (indent).Append (i.ToCode()).AppendLine(";");

					if (separatePackageRoots) {
						var iid = ImportComparer.ExtractPrimaryId (i);
						if (prevId != null && iid != null &&
						   (iid.InnerDeclaration ?? iid).ToString (true) != (prevId.InnerDeclaration ?? prevId).ToString (true))
							sb.AppendLine ();

						prevId = iid;
					}
				}

				editor.Insert (editor.LocationToOffset (firstLoc.Line, firstLoc.Column), sb.ToString ());
			}
		}
	}
}

