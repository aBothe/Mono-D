using System;
using D_Parser.Misc;
using System.Collections.Generic;
using D_Parser.Dom;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D
{
	public class MonoDParseCacheView : ParseCacheView
	{
		Dictionary<DModule, List<RootPackage>> cache = new Dictionary<DModule, List<RootPackage>>();

		public override IEnumerable<RootPackage> EnumRootPackagesSurroundingModule (DModule module)
		{
			RootPackage pack;
			List<RootPackage> results;
			if (cache.TryGetValue (module, out results))
				return results;

			results = new List<RootPackage> ();

			foreach (var prj in Ide.IdeApp.Workspace.GetProjectsContainingFile(module.FileName)) {
				var dprj = prj as AbstractDProject;

				if (dprj == null)
					continue;

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

				foreach (var p in dprj.References.Includes)
					if((pack = GlobalParseCache.GetRootPackage (p)) != null) {
						if(!results.Contains(pack))
							results.Add (pack);
					}

				foreach (var depPrj in dprj.GetReferencedDProjects(Ide.IdeApp.Workspace.ActiveConfiguration))
					foreach (var p in depPrj.GetSourcePaths())
						if((pack = GlobalParseCache.GetRootPackage (p)) != null) {
							if(!results.Contains(pack))
								results.Add (pack);
						}
			}

			cache [module] = results;
			return results;
		}
	}
}

