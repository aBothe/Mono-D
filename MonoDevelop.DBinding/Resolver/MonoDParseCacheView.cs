using System;
using D_Parser.Misc;
using System.Collections.Generic;
using D_Parser.Dom;
using MonoDevelop.D.Projects;
using MonoDevelop.D.Building;
using MonoDevelop.D.Projects.Dub;

namespace MonoDevelop.D
{
	public class MonoDParseCacheView : ParseCacheView
	{
		readonly Dictionary<DModule, List<RootPackage>> cache = new Dictionary<DModule, List<RootPackage>>();
		readonly List<RootPackage> globalIncludes = new List<RootPackage>();

		public MonoDParseCacheView()
		{
			Add (globalIncludes, DCompilerService.Instance.GetDefaultCompiler ().IncludePaths);
		}

		static void Add(List<RootPackage> results, IEnumerable<string> paths)
		{
			RootPackage pack;
			foreach(var p in paths)
				if((pack = GlobalParseCache.GetRootPackage (p)) != null) {
					if(!results.Contains(pack))
						results.Add (pack);
				}
		}

		public override IEnumerable<RootPackage> EnumRootPackagesSurroundingModule (DModule module)
		{
			if (module == null)
				return globalIncludes;

			RootPackage pack;
			List<RootPackage> results;
			if (cache.TryGetValue (module, out results))
				return results;

			results = new List<RootPackage> ();

			foreach(var prj in Ide.IdeApp.Workspace.GetAllProjects()) {
				var dprj = prj as AbstractDProject;

				if (dprj == null || !prj.IsFileInProject(module.FileName))
					continue;

				if (dprj is DProject)
					Add (results, (dprj as DProject).Compiler.IncludePaths);
				else if (dprj is DubProject)
					results.AddRange (globalIncludes);

				foreach (var p in dprj.GetSourcePaths())
					if ((pack = GlobalParseCache.GetRootPackage (p)) != null) {
						if (!results.Contains (pack)) {
							foreach (DModule m in pack)
								cache [m] = results;

							results.Add (pack);
						}
					}

				if (dprj.LinkedFilePackage != null)
					results.Add (dprj.LinkedFilePackage);

				Add(results, dprj.References.Includes);

				foreach (var depPrj in dprj.GetReferencedDProjects(Ide.IdeApp.Workspace.ActiveConfiguration))
					Add(results, depPrj.GetSourcePaths());
			}

			if (results.Count == 0)
				results = globalIncludes;

			cache [module] = results;
			return results;
		}
	}
}

