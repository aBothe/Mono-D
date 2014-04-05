using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Misc;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.Ide;
using MonoDevelop.Ide.FindInFiles;
using D_Parser.Resolver;
using D_Parser.Refactoring;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Refactoring
{
	public class ReferenceFinding
	{
		ISearchProgressMonitor monitor;
		bool alsoSearchDerivatives = false;

		public static void StartReferenceSearchAsync(INode n, bool searchInDerivatives = false)
		{
			var rf = new ReferenceFinding {
				alsoSearchDerivatives = searchInDerivatives,
				monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor(true, true)
			};

			ThreadPool.QueueUserWorkItem(rf.FindReferencesThread,n);
		}

		void FindReferencesThread(object state)
		{
			try
			{
				foreach (var sr in ReferenceFinding.FindReferences(
					IdeApp.Workbench.ActiveDocument.HasProject ?
					IdeApp.Workbench.ActiveDocument.Project as DProject : null,
					(INode)state, monitor))
					monitor.ReportResult(sr);
			}
			catch (Exception ex)
			{
				if (monitor != null)
					monitor.ReportError("Error finding references", ex);
				else
					LoggingService.LogError("Error finding references", ex);
			}
			finally
			{
				if (monitor != null)
					monitor.Dispose();
			}
		}

		static IEnumerable<SearchResult> FindReferences(
			AbstractDProject project,
			INode member,
			ISearchProgressMonitor monitor = null)
		{
			var searchResults = new List<SearchResult>();

			var parseCache = project != null ?
				project.ParseCache : DCompilerService.Instance.GetDefaultCompiler().GenParseCacheView();

			var modules = new List<DModule>();

			if (project != null)
				foreach (var p in project.GetSourcePaths(IdeApp.Workspace.ActiveConfiguration))
					modules.AddRange (GlobalParseCache.EnumModulesRecursively (p, null));
			else
				modules.Add ((IdeApp.Workbench.ActiveDocument.ParsedDocument as MonoDevelop.D.Parser.ParsedDModule).DDom);

			if (monitor != null)
				monitor.BeginStepTask ("Scan for references", modules.Count, 1);

			List<ISyntaxRegion> references = null;
			var ctxt = ResolutionContext.Create (parseCache, null);
			foreach (var mod in modules)
			{
				if (mod == null)
					continue;
				try
				{
					references = ReferencesFinder.Scan(mod, member, ctxt).ToList();

					if (references.Count < 1)
					{
						if (monitor != null)
							monitor.Step(1);
						continue;
					}

					// Sort the references by code location
					references.Sort(new IdLocationComparer());

					// Get actual document code
					var targetDoc = TextFileProvider.Instance.GetTextEditorData(new FilePath(mod.FileName));

					foreach (var reference in references)
					{
						CodeLocation loc;

						if (reference is AbstractTypeDeclaration)
							loc = ((AbstractTypeDeclaration)reference).NonInnerTypeDependendLocation;
						else if (reference is IExpression)
							loc = reference.Location;
						else
							continue;

						searchResults.Add(new SearchResult(new FileProvider(mod.FileName, project),
							targetDoc.LocationToOffset(loc.Line,
														loc.Column),
							member.Name.Length));
					}
				}
				catch (Exception ex) { LoggingService.LogWarning("Error during reference search", ex); }

				if (monitor != null)
					monitor.Step(1);
			}

			if (monitor != null)
				monitor.EndTask();

			return searchResults;
		}

		public class IdLocationComparer : IComparer<ISyntaxRegion>
		{
			bool asc;
			public IdLocationComparer(bool asc = true)
			{
				this.asc = asc;
			}

			public int Compare(ISyntaxRegion x, ISyntaxRegion y)
			{
				return asc ? x.Location.CompareTo(y.Location) : y.Location.CompareTo(x.Location);
			}
		}
	}
}
