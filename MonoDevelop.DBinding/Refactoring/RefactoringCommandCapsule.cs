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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Refactoring;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Parser;
using MonoDevelop.D.Resolver;


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
		public static bool SortImportsSeparatePackagesFromEachOther
		{
			get{ return PropertyService.Get (SortImportsSeparatePackagesFromEachOtherPropId, false); }
			set{ PropertyService.Set (SortImportsSeparatePackagesFromEachOtherPropId, value); }
		}

		public static void SortImports()
		{
			var doc = IdeApp.Workbench.ActiveDocument;

			if (doc == null)
				return;

			var ddoc = doc.ParsedDocument as ParsedDModule;
			if (ddoc == null || ddoc.DDom == null)
				return;

			var scope = DResolver.SearchBlockAt (ddoc.DDom, new CodeLocation(doc.Editor.Caret.Column, doc.Editor.Caret.Line)) as DBlockNode;

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

			// Split imports into groups divided by shared attributes
			var impDict = new Dictionary<List<DAttribute>, List<ImportStatement>> ();
			var noAttrImports = new List<ImportStatement> ();

			for (int i = importsToSort.Count - 1; i >= 0; i--) {
				var ii = importsToSort [i];

				if (ii.Attributes == null || ii.Attributes.Length == 0) {
					noAttrImports.Add (ii);
					continue;
				}

				for (int k = importsToSort.Count - 1; k >= 0; k--) {
					var ki = importsToSort [k];

					if (k == i || ki.Attributes == null)
						continue;

					var sharedAttrs = new List<DAttribute>(ii.Attributes.Intersect(ki.Attributes));
					var sharedAttrsCount = sharedAttrs.Count;
					if (sharedAttrsCount == 0)
						continue;

					bool hasAdded = false;

					foreach (var kv in impDict) {
						if (kv.Key.Count > sharedAttrsCount ||
							kv.Key.Any ((e) => !sharedAttrs.Contains (e)))
							continue;

						if (kv.Key.Count == sharedAttrsCount) {
							if (!kv.Value.Contains (ii))
								kv.Value.Add (ii);
							if (!kv.Value.Contains (ki))
								kv.Value.Add (ki);
							hasAdded = true;
							break;
						}
							
						kv.Value.Remove (ii);
						kv.Value.Remove (ki);
						break;
					}

					if (!hasAdded)
						impDict.Add (sharedAttrs, new List<ImportStatement>{ ii, ki });
				}
			}

			var editor = doc.Editor.Document;
			using (editor.OpenUndoGroup (Mono.TextEditor.OperationType.Undefined)) {
				ResortImports (noAttrImports, editor, new List<DAttribute>());
				foreach (var kv in impDict)
					ResortImports (kv.Value, editor, kv.Key);
			}
		}

		static void ResortImports(List<ImportStatement> importsToSort, Mono.TextEditor.TextDocument editor, List<DAttribute> attributesNotToWrite)
		{
			if (importsToSort.Count < 2)
				return;
				
			int firstOffset = int.MaxValue;

			// Remove all of them from the document; Memorize where the first import was
			for (int i = importsToSort.Count - 1; i >= 0; i--) {
				var ss = importsToSort [i];
				var ssLocation = ss.Location;
				var ssEndLocation = ss.EndLocation;

				DAttribute attr;
				if (ss.Attributes != null && ss.Attributes.Length > 0) {
					attr = ss.Attributes.FirstOrDefault ((e) => !attributesNotToWrite.Contains (e));
					if(attr != null && attr.Location < ssLocation)
						ssLocation = attr.Location;

					attr = ss.Attributes.LastOrDefault ((e) => !attributesNotToWrite.Contains (e));
					if(attr != null && attr.EndLocation > ssEndLocation)
						ssEndLocation = attr.EndLocation;
				}

				var l1 = editor.LocationToOffset (ssLocation.Line, ssLocation.Column);
				var l2 = editor.LocationToOffset (ssEndLocation.Line, ssEndLocation.Column);
				var n = editor.TextLength - 1;

				// Remove indents and trailing semicolon.
				for (char c; l1 > 0 && ((c = editor.GetCharAt (l1-1)) == ' ' || c == '\t'); l1--);
				for (char c; l2 < n && ((c = editor.GetCharAt (l2+1)) == ' ' || c == '\t' || c == ';'); l2++);
				for (char c; l2 < n && ((c = editor.GetCharAt (l2+1)) == '\n' || c == '\r'); l2++);

				editor.Remove (l1, l2 - l1);
				firstOffset = Math.Min(l1, firstOffset);
			}
			var firstOffsetLocation = editor.OffsetToLocation (firstOffset);

			// Sort
			importsToSort.Sort (new ImportComparer ());

			// Write all imports beneath each other.
			var indent = editor.GetLineIndent (firstOffsetLocation.Line);
			var sb = new StringBuilder ();
			bool separatePackageRoots = SortImportsSeparatePackagesFromEachOther;
			ITypeDeclaration prevId = null;

			foreach (var i in importsToSort) {
				sb.Append (indent);

				if (i.Attributes != null) {
					foreach (var attr in i.Attributes) {
						if (attributesNotToWrite.Contains (attr))
							continue;

						sb.Append (attr.ToString()).Append(' ');
					}
				}
					
				sb.Append (i.ToCode(false)).AppendLine(";");

				if (separatePackageRoots) {
					var iid = ImportComparer.ExtractPrimaryId (i);
					if (prevId != null && iid != null &&
						(iid.InnerDeclaration ?? iid).ToString (true) != (prevId.InnerDeclaration ?? prevId).ToString (true))
						sb.AppendLine ();

					prevId = iid;
				}
			}

			editor.Insert (firstOffset, sb.ToString ());
		}
	}
}

