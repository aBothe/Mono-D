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
using D_Parser.Misc;


namespace MonoDevelop.D.Refactoring
{
	class RefactoringCommandCapsule
	{
		public D_Parser.Completion.IEditorData ed;
		public AbstractType[] lastResults;
		public LooseResolution.NodeResolutionAttempt resultResolutionAttempt;
		public Document lastDoc;
		public ISyntaxRegion lastSyntaxObject;

		public bool Update(Document doc = null)
		{
			lastResults = DResolverWrapper.ResolveHoveredCodeLoosely(out ed, out resultResolutionAttempt, out lastSyntaxObject, lastDoc = (doc ?? IdeApp.Workbench.ActiveDocument));

			return lastResults != null && lastResults.Length > 0;
		}

		AbstractType GetResult()
		{
			if (lastResults == null || lastResults.Length < 1)
				return null;

			if (lastResults.Length == 1)
				return lastResults [0];

			return ImportSymbolSelectionDlg.Show (lastResults, GettextCatalog.GetString("Select symbol"), 
				(object o) => o is DSymbol ? (o as DSymbol).Definition.ToString(false, true) : o.ToString());
		}

		public void GotoDeclaration()
		{
			var res = GetResult();
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
			var res = GetResult();
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

			var ctxt = ResolutionContext.Create (ed, true);

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

					monitor.ReportResult(new SearchResult(new FileProvider(file, IdeApp.Workspace.GetProjectsContainingFile (file).FirstOrDefault ()),
						targetDoc.LocationToOffset(dc.NameLocation.Line,dc.NameLocation.Column),dc.Name.Length));
				}
				monitor.Step (1);
			}

			monitor.EndTask();
			monitor.Dispose ();
		}

		public void RenameSymbol()
		{
			var res = GetResult();
			INode n;
			if (res != null && (n = DResolver.GetResultMember(res, true)) != null &&
				DRenameRefactoring.CanRenameNode(n))
				new DRenameHandler().Start(n);
		}

		public void TryImportMissingSymbol()
		{
			DModule n;
			var nodesToChooseFrom = GetImportableModulesForLastResults ();

			var count = nodesToChooseFrom.Count ();
			if (count == 0)
				return;

			if (count == 1)
				n = nodesToChooseFrom.First();
			else
				n = ImportSymbolSelectionDlg.Show (nodesToChooseFrom, GettextCatalog.GetString ("Select symbol"));

			if(n != null)
				ImportStmtCreation.GenerateImportStatementForNode(n, ed, new TextDocumentAdapter(IdeApp.Workbench.ActiveDocument.Editor));
		}

		public IEnumerable<DModule> GetImportableModulesForLastResults()
		{
			var nodesToChooseFrom = new List<DModule> ();

			if (lastResults == null || lastResults.Length < 1)
				return nodesToChooseFrom;

			foreach (var res in lastResults) {
				var n = DResolver.GetResultMember (res, true);
				if (n != null) {
					var mod = n.NodeRoot as DModule;
					if (mod != null && !nodesToChooseFrom.Contains (mod)) {
						var i = Math.Max(0, nodesToChooseFrom.Count-1);
						foreach(var packageMod in TryGetGenericImportingPackageForSymbol (n))
							if (!nodesToChooseFrom.Contains (packageMod))
								nodesToChooseFrom.Insert (i, packageMod);

						nodesToChooseFrom.Add (mod);
					}

				}
			}

			return nodesToChooseFrom;
		}

		static IEnumerable<DModule> TryGetGenericImportingPackageForSymbol(DNode nodeToTestImportabilityFor)
		{
			if (nodeToTestImportabilityFor == null)
				yield break;
			
			var moduleContainingPackage = GlobalParseCache.GetPackage(nodeToTestImportabilityFor.NodeRoot as DModule);

			while(moduleContainingPackage != null){
				// Search for package.d-Modules;
				var packageModule = (moduleContainingPackage.Parent ?? moduleContainingPackage).GetModule(moduleContainingPackage.NameHash);

				if (packageModule == null) {
					moduleContainingPackage = moduleContainingPackage.Parent;
					continue;
				}

				// Try to get from found package module to destination node
				var ctxt = new ResolutionContext(new LegacyParseCacheView(new[]{ moduleContainingPackage.Root }), null, packageModule);
				ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.ReturnMethodReferencesOnly | ResolutionOptions.DontResolveBaseTypes;

				var td = D_Parser.Parser.DParser.ParseBasicType(DNode.GetNodePath (nodeToTestImportabilityFor, true));
				var res = TypeDeclarationResolver.ResolveSingle (td, ctxt, false);

				foreach (var ov in AmbiguousType.TryDissolve(res))
					if (ov is DSymbol && (ov as DSymbol).Definition == nodeToTestImportabilityFor) {
						yield return packageModule;
						break;
					}

				moduleContainingPackage = moduleContainingPackage.Parent;
			}
		}
	}
}

