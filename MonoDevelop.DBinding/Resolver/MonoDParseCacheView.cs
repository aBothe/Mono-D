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
		readonly Dictionary<DModule, ISet<RootPackage>> cache = new Dictionary<DModule, ISet<RootPackage>>();
		readonly ISet<RootPackage> globalIncludes = new HashSet<RootPackage>();

		public MonoDParseCacheView()
		{
			Add (globalIncludes, DCompilerService.Instance.GetDefaultCompiler ().IncludePaths);
		}

		static void Add(ISet<RootPackage> results, IEnumerable<string> paths)
		{
			RootPackage pack;
			foreach(var p in paths)
				if((pack = GlobalParseCache.GetRootPackage (p)) != null) {
					results.Add (pack);
				}
		}

		public override IEnumerable<RootPackage> EnumRootPackagesSurroundingModule (DModule module)
		{
			if (module == null)
				return globalIncludes;

			ISet<RootPackage> results;
			if (cache.TryGetValue (module, out results))
				return results;

			results = new HashSet<RootPackage> ();

			foreach(var prj in Ide.IdeApp.Workspace.GetAllProjects()) {
				var dprj = prj as AbstractDProject;

				if (dprj == null || !prj.IsFileInProject(module.FileName))
					continue;

				Add (results, dprj.GetSourcePaths ());

				Add (results, dprj.IncludePaths);

				if (dprj.LinkedFilePackage != null)
					results.Add (dprj.LinkedFilePackage);
			}

			if (results.Count == 0)
				results = globalIncludes;

			cache [module] = results;
			return results;
		}
	}
}

