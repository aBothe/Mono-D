using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Misc;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.FindInFiles;
using D_Parser.Dom.Expressions;

namespace MonoDevelop.D.Refactoring
{
	public class ReferenceFinder : D_Parser.Refactoring.ReferenceFinder
	{
		public static IEnumerable<SearchResult> FindReferences(
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
