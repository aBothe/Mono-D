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

namespace MonoDevelop.D.Refactoring
{
	public class ReferenceFinding : D_Parser.Refactoring.ReferenceFinder
	{
		ISearchProgressMonitor monitor;

		public static void StartReferenceSearchAsync(INode n)
		{
			var rf = new ReferenceFinding {
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
			DProject project,
			INode member,
			ISearchProgressMonitor monitor = null)
		{
			var searchResults = new List<SearchResult>();

			var parseCache = project != null ?
				project.ParseCache :
				ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);

			var modules = project == null ?
				project.LocalFileCache as IEnumerable<IAbstractSyntaxTree> :
				new[] { (Ide.IdeApp.Workbench.ActiveDocument.ParsedDocument as MonoDevelop.D.Parser.ParsedDModule).DDom };

			if (monitor != null)
				monitor.BeginStepTask("Scan for references", modules.Count(), 1);

			foreach (var mod in modules)
			{
				if (mod == null)
					continue;

				var references = ScanNodeReferencesInModule(mod, parseCache, member);

				if (member != null && member.NodeRoot != null &&
					(member.NodeRoot as IAbstractSyntaxTree).FileName == mod.FileName)
					references.Insert(0, new IdentifierDeclaration(member.Name)
					{
						Location = member.NameLocation,
						EndLocation = new CodeLocation(member.NameLocation.Column + member.Name.Length,
							member.NameLocation.Line)
					});

				if (references.Count < 1)
				{
					if (monitor != null)
						monitor.Step(1);
					continue;
				}

				// Sort the references by code location
				references.Sort(new IdLocationComparer());

				// Get actual document code
				var targetDoc = Ide.TextFileProvider.Instance.GetTextEditorData(new FilePath(mod.FileName));

				foreach (var reference in references)
				{
					CodeLocation loc;

					if (reference is AbstractTypeDeclaration)
						loc = ((AbstractTypeDeclaration)reference).NonInnerTypeDependendLocation;
					else if (reference is IExpression)
						loc = ((IExpression)reference).Location;
					else
						continue;

					searchResults.Add(new SearchResult(new FileProvider(mod.FileName, project),
						targetDoc.LocationToOffset(loc.Line,
													loc.Column),
						member.Name.Length));
				}

				if (monitor != null)
					monitor.Step(1);
			}

			if (monitor != null)
				monitor.EndTask();

			return searchResults;
		}
	}
}
