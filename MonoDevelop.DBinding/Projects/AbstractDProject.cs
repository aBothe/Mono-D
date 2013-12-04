using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using MonoDevelop.D.Building;
using MonoDevelop.D.Resolver;
using MonoDevelop.Projects;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using System.Reflection;
using System.Collections.ObjectModel;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.D.Projects
{
	public abstract class AbstractDProject : Project
	{
		#region Properties
		public override string ProjectType { get { return "Native"; } }
		public override string[] SupportedLanguages { get { return new[] { "D", "" }; } }

		public virtual IEnumerable<string> GlobalIncludes { get { return DCompilerService.Instance.GetDefaultCompiler().IncludePaths; } }
		public IEnumerable<string> LocalIncludes {get{return References.Includes;}}
		public abstract DProjectReferenceCollection References { get; }

		protected MutableRootPackage fileLinkModulesRoot;

		public virtual ParseCacheView ParseCache
		{
			get
			{
				var r = new ParseCacheView(IncludePaths);
				r.Add (GetSourcePaths ());

				if(fileLinkModulesRoot != null)
					r.Add (fileLinkModulesRoot);

				return r;
			}
		}

		public IEnumerable<string> GetSourcePaths()
		{
			return GetSourcePaths (Ide.IdeApp.Workspace.ActiveConfiguration);
		}

		public virtual IEnumerable<string> GetSourcePaths(ConfigurationSelector sel)
		{
			yield return BaseDirectory;
		}

		public override IEnumerable<SolutionItem> GetReferencedItems(ConfigurationSelector configuration)
		{
			SolutionItem p;
			foreach (var dep in References.ReferencedProjectIds)
				if ((p = ParentSolution.GetSolutionItem(dep)) != null)
					yield return p;
		}

		public IEnumerable<string> IncludePaths
		{
			get
			{
				foreach (var p in GlobalIncludes)
					yield return p;
				foreach (var p in LocalIncludes)
					yield return p;
				var sel = Ide.IdeApp.Workspace.ActiveConfiguration;
				foreach (var dep in GetReferencedItems(sel))
					if (dep is AbstractDProject)
						foreach (var s in (dep as AbstractDProject).GetSourcePaths(sel))
							yield return s;
			}
		}

		bool needsFullRebuild;
		public bool NeedsFullRebuild
		{
			get{return needsFullRebuild;}
			set{ 
				if (!value)
					needsFullRebuild = false;
				else
					needsFullRebuild = true;
			}
		}
		#endregion

		#region Parsed project modules
		protected void InformGlobalParseCacheFilled()
		{
			analysisFinished_GlobalCache = true;
			TryBuildUfcsCache ();
		}

		public void UpdateLocalIncludeCache()
		{
			analysisFinished_LocalIncludes = false;
			DCompilerConfiguration.UpdateParseCacheAsync(LocalIncludes, BaseDirectory,
			                                             ParentSolution == null ? 
			                                             	BaseDirectory.ToString() : 
			                                             	ParentSolution.BaseDirectory.ToString(), true,
			                                             LocalIncludeCache_FinishedParsing);
		}

		/// <summary>
		/// Updates the project's parse cache and reparses all of its D sources
		/// </summary>
		public void UpdateParseCache()
		{
			analysisFinished_LocalCache = analysisFinished_FileLinks = false;

			var hasFileLinks = new List<ProjectFile>();
			foreach (var f in Files)
				if ((f.IsLink || f.IsExternalToProject) && File.Exists(f.ToString()))
					hasFileLinks.Add(f);

			// To prevent race condition bugs, test if links exist _before_ the actual local file parse procedure starts.
			if (hasFileLinks.Count == 0)
				analysisFinished_FileLinks = true;

			DCompilerConfiguration.UpdateParseCacheAsync (GetSourcePaths(), true, LocalFileCache_FinishedParsing);

			//EDIT: What if those file links refer to other project's files? Or what if more than one project reference the same files?
			//Furthermore, what if those files become edited and reparsed? Will their reference in the projects be updated either?
			// -> make a root for every file link in the global parse cache and build up a virtual root containing all file links right before a cache view is requested?

			/*
			 * Since we don't want to include all link files' directories for performance reasons,
			 * parse them separately and let the entire reparsing procedure wait for them to be successfully parsed.
			 * Ufcs completion preparation will be done afterwards in the TryBuildUfcsCache() method.
			 */
			if (hasFileLinks.Count != 0)
				new System.Threading.Thread(() =>
				{
					var r = new MutableRootPackage();
					foreach (var f in hasFileLinks)
						r.AddModule(DParser.ParseFile(f.FilePath) as DModule);
					fileLinkModulesRoot = r;

					analysisFinished_FileLinks = true;
					TryBuildUfcsCache();
				}) { IsBackground = true }.Start();
		}

		bool analysisFinished_GlobalCache, analysisFinished_LocalIncludes, analysisFinished_LocalCache, analysisFinished_FileLinks;

		protected void LocalIncludeCache_FinishedParsing(ParsingFinishedEventArgs PerformanceData)
		{
			if (References != null)
				References.FireUpdate();
			analysisFinished_LocalIncludes = true;
			TryBuildUfcsCache();
		}

		void LocalFileCache_FinishedParsing(ParsingFinishedEventArgs PerformanceData)
		{
			analysisFinished_LocalCache = true;
			TryBuildUfcsCache();
		}

		void TryBuildUfcsCache()
		{
			if (analysisFinished_GlobalCache &&
			    analysisFinished_LocalCache && 
			    analysisFinished_LocalIncludes &&
				analysisFinished_FileLinks)
			{
				var pcw = new ParseCacheView (GlobalIncludes);
				pcw.Add (LocalIncludes);

				foreach (var p in LocalIncludes) {
					var r = GlobalParseCache.GetRootPackage (p);
					if (r == null)
						continue;

					r.UfcsCache.BeginUpdate (pcw);
				}

				// Don't reuse the existing view for obvious threading problems!
				pcw = new ParseCacheView (GlobalIncludes);
				pcw.Add (LocalIncludes);
				var src = GetSourcePaths ();
				pcw.Add (src);

				foreach (var p in src) {
					var r = GlobalParseCache.GetRootPackage (p);
					if (r == null)
						continue;

					r.UfcsCache.BeginUpdate (pcw);
				}
			}
		}

		protected override void OnFileRemovedFromProject(ProjectFileEventArgs e)
		{
			base.OnFileRemovedFromProject(e);
			NeedsFullRebuild = true;
			foreach (var pf in e)
				GlobalParseCache.RemoveModule (pf.ProjectFile.FilePath);
		}

		protected override void OnFileRenamedInProject(ProjectFileRenamedEventArgs e)
		{
			NeedsFullRebuild = true;
			//FIXME: Bug when renaming files..the new file won't be reachable somehow.. see https://bugzilla.xamarin.com/show_bug.cgi?id=13360
			var fi = Files.GetType().GetField("files",BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			var files = fi.GetValue (Files) as Dictionary<FilePath, ProjectFile>;

			/*foreach (var pf in e){
				// The old file won't be removed from that internal files dictionary - so go enforce this!
				files.Remove (pf.OldName);
				var ast = LocalFileCache.GetModuleByFileName (pf.OldName);
				if (ast == null)
					continue;

				LocalFileCache.Remove (ast, true);
				ast.FileName = pf.NewName.ToString ();

				if (ast.OptionalModuleStatement == null) {
					string parsedDir = null;
					foreach (var dir in LocalFileCache.ParsedDirectories)
						if (ast.FileName.StartsWith (dir)) {
							parsedDir = dir;
							break;
						}
					ast.ModuleName = DModule.GetModuleName (parsedDir, ast);
				}
				LocalFileCache.AddOrUpdate (ast);
			}*/

			base.OnFileRenamedInProject(e);
		}

		protected override void OnEndLoad()
		{
			base.OnEndLoad();

			//Compiler.ParseCache.FinishedParsing += new D_Parser.Misc.ParseCache.ParseFinishedHandler(GlobalParseCache_FinishedParsing);

			UpdateLocalIncludeCache();
			UpdateParseCache();
			NeedsFullRebuild = true;
		}
		#endregion

		public override void Dispose ()
		{
			// Remove roots that aren't required anymore!?
			var g = LocalIncludes.ToList();
			foreach (var prj in Ide.IdeApp.Workspace.GetAllProjects()) {
				var p = prj as AbstractDProject;
				if (p!=null && p != this) {
					foreach (var g_ in p.LocalIncludes)
						g.Remove (g_);
				}
			}

			foreach (var path in g)
				GlobalParseCache.RemoveRoot (path);

			base.Dispose ();
		}

		protected override bool OnGetCanExecute(ExecutionContext context, ConfigurationSelector configuration)
		{
			return context.ExecutionHandler.CanExecute(CreateExecutionCommand(configuration));
		}

		public virtual NativeExecutionCommand CreateExecutionCommand(ConfigurationSelector conf)
		{
			return new NativeExecutionCommand(GetOutputFileName(conf));
		}

		public string GetAbsPath(string path)
		{
			try{
				return Path.IsPathRooted (path) ? path : Path.GetFullPath(BaseDirectory.Combine (path).ToString ());
			}catch {
				return path;
			}
		}
	}
}
