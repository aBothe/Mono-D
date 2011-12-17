using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.Projects;
using D_Parser.Dom;
using D_Parser.Resolver;
using MonoDevelop.Core;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using System.Collections;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Refactoring
{
	public class DReferenceFinder
	{
		public static IEnumerable<SearchResult> FindReferences(
			DProject project, 
			INode member, 
			ISearchProgressMonitor monitor=null)
		{
			var searchResults = new List<SearchResult>();

            var parseCache = project != null ? 
				project.ParseCache : 
				DCompiler.Instance.GetDefaultCompiler().GlobalParseCache.ParseCache;

            var modules = project!=null? 
				project.ParsedModules : 
				new[]{ (Ide.IdeApp.Workbench.ActiveDocument.ParsedDocument as MonoDevelop.D.Parser.ParsedDModule).DDom };

			if(monitor!=null)
				monitor.BeginStepTask("Scan for references", modules.Count(), 1);

			foreach (var mod in modules)
			{
                if (mod == null)
                    continue;

				var references= ScanNodeReferencesInModule(mod,
					parseCache,
					DResolver.ResolveImports(mod as DModule, parseCache),
					member);

				if ((member.NodeRoot as IAbstractSyntaxTree).FileName==mod.FileName)
					references.Insert(0,new IdentifierDeclaration(member.Name) { 
						Location=member.NameLocation ,
						EndLocation=new CodeLocation(member.NameLocation.Column+member.Name.Length,
							member.NameLocation.Line)
				});

				if(references.Count<1)
				{
					if(monitor!=null)
						monitor.Step(1);
					continue;
				}

				// Sort the references by code location
				references.Sort(new IdLocationComparer());

				// Get actual document code
				var targetDoc=Ide.TextFileProvider.Instance.GetTextEditorData(new FilePath(mod.FileName));

				foreach (var reference in references)
				{
					searchResults.Add( new SearchResult(new FileProvider(mod.FileName,project),
						targetDoc.LocationToOffset(	reference.NonInnerTypeDependendLocation.Line,
													reference.NonInnerTypeDependendLocation.Column),
						member.Name.Length));
				}

				if(monitor!=null)
					monitor.Step(1);
			}

			if (monitor != null)
				monitor.EndTask();

			return searchResults;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>Array of expressions/type declarations referencing the given node</returns>
		public static List<IdentifierDeclaration> ScanNodeReferencesInModule(
			IAbstractSyntaxTree scannedFileAST,
			IEnumerable<IAbstractSyntaxTree> parseCache,
			IEnumerable<IAbstractSyntaxTree> scannedFileImports,
			params INode[] declarationsToCompareWith)
		{
			var namesToCompareWith = new List<string>();

			foreach (var n in declarationsToCompareWith)
				namesToCompareWith.Add(n.Name);

			var matchedReferences = new List<IdentifierDeclaration>();

			var identifiers=CodeScanner.ScanForTypeIdentifiers(scannedFileAST);

			var resolveContext=new ResolverContext{
				ResolveAliases=false,
				ResolveBaseTypes=true,

				ParseCache=parseCache,
				ImportCache=scannedFileImports
			};

			foreach (var o in identifiers)
			{
				IdentifierDeclaration id = null;

				if (o is IdentifierDeclaration)
					id = (o as IdentifierDeclaration);
				else if (o is TemplateInstanceExpression)
					id = (o as TemplateInstanceExpression).TemplateIdentifier;
				else
					continue;

				if (!namesToCompareWith.Contains(id.Value as string))
					continue;

				// Get the context of the used identifier
				resolveContext.ScopedBlock = DResolver.SearchBlockAt(scannedFileAST, (o as ITypeDeclaration).Location, out resolveContext.ScopedStatement);

				// Resolve the symbol to which the identifier is related to
				var resolveResults = DResolver.ResolveType(o as ITypeDeclaration, resolveContext);

				if (resolveResults == null)
					continue;

                foreach (var targetSymbol in resolveResults)
                {
                    // Get the associated declaration node
                    var targetSymbolNode = Resolver.DResolverWrapper.GetResultMember(targetSymbol);

                    if(targetSymbolNode==null)
                        break;

                    // Compare with the members whose references shall be looked up
                    if (declarationsToCompareWith.Length==1? 
						targetSymbolNode==declarationsToCompareWith[0] : 
						declarationsToCompareWith.Contains(targetSymbolNode))
                    {
                        // ... Reference found!
                        matchedReferences.Add(id);
                    }
                }
			}



			/* Sort matches
			if(sortResults)
				matchedReferences.Sort(new IdLocationComparer());
			*/


			return matchedReferences;
		}

		public class IdLocationComparer : IComparer<IdentifierDeclaration>
		{
			bool rev;
			public IdLocationComparer(bool reverse = false)
			{
				rev = reverse;
			}

			public int Compare(IdentifierDeclaration x, IdentifierDeclaration y)
			{
				if (x == null || y == null || x.Location==y.Location)
					return 0;

				return (rev? x.Location<y.Location : x.Location>y.Location)?1:-1;
			}
		}
	}
}
