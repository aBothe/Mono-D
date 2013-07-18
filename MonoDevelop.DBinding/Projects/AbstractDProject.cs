using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using MonoDevelop.D.Building;
using MonoDevelop.D.Resolver;
using MonoDevelop.Projects;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonoDevelop.D.Projects
{
	public abstract class AbstractDProject : Project
	{
		#region Properties
		public virtual DCompilerConfiguration Compiler { get { return DCompilerService.Instance.GetDefaultCompiler(); } set { } }
		public override string ProjectType { get { return "Native"; } }
		public override string[] SupportedLanguages { get { return new[] { "D", "" }; } }
		public virtual DProjectReferenceCollection References { get {return null;} }

		/// <summary>
		/// Stores parse information from project-wide includes
		/// </summary>
		public readonly ParseCache LocalIncludeCache = new ParseCache { EnableUfcsCaching = false };

		/// <summary>
		/// Stores parse information from files inside the project's base directory
		/// </summary>
		public readonly ParseCache LocalFileCache = new ParseCache { EnableUfcsCaching = false };
		protected readonly List<DModule> _filelinkModulesToInsert = new List<DModule>();

		public virtual ParseCacheList ParseCache
		{
			get
			{
				return DResolverWrapper.CreateCacheList(this);
			}
		}

		public virtual IEnumerable<string> GetSourcePaths(ConfigurationSelector sel)
		{
			yield return BaseDirectory;
		}

		public virtual IEnumerable<string> IncludePaths
		{
			get
			{
				foreach (var p in Compiler.ParseCache.ParsedDirectories)
					yield return p;
				foreach (var p in LocalIncludeCache.ParsedDirectories)
					yield return p;
				var sel = Ide.IdeApp.Workspace.ActiveConfiguration;
				foreach (var dep in GetReferencedItems(sel))
					if (dep is AbstractDProject)
						foreach (var s in (dep as AbstractDProject).GetSourcePaths(sel))
							yield return s;
			}
		}
		#endregion

		public AbstractDProject()
		{
			LocalFileCache.FinishedParsing += new ParseCache.ParseFinishedHandler(LocalFileCache_FinishedParsing);
			LocalIncludeCache.FinishedParsing += new ParseCache.ParseFinishedHandler(LocalIncludeCache_FinishedParsing);
		}

		#region Parsed project modules
		public void UpdateLocalIncludeCache()
		{
			analysisFinished_LocalIncludes = false;
			LocalIncludeCache.SolutionPath = ParentSolution == null ? BaseDirectory.ToString() : ParentSolution.BaseDirectory.ToString();
			LocalIncludeCache.FallbackPath = BaseDirectory;
			DCompilerConfiguration.UpdateParseCacheAsync(LocalIncludeCache);
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

			var paths = GetSourcePaths(Ide.IdeApp.Workspace.ActiveConfiguration);
			LocalFileCache.BeginParse(paths, paths.FirstOrDefault());
			//LocalFileCache.WaitForParserFinish();

			/*
			 * Since we don't want to include all link files' directories for performance reasons,
			 * parse them separately and let the entire reparsing procedure wait for them to be successfully parsed.
			 * Ufcs completion preparation will be done afterwards in the TryBuildUfcsCache() method.
			 */
			if (hasFileLinks.Count != 0)
				new System.Threading.Thread((object o) =>
				{
					foreach (var f in (List<ProjectFile>)o)
					{
						_filelinkModulesToInsert.Add(DParser.ParseFile(f.FilePath) as DModule);
					}

					analysisFinished_FileLinks = true;
					_InsertFileLinkModulesIntoLocalCache();
					TryBuildUfcsCache();
				}) { IsBackground = true }.Start(hasFileLinks);
		}

		bool analysisFinished_GlobalCache, analysisFinished_LocalIncludes, analysisFinished_LocalCache, analysisFinished_FileLinks;

		void _InsertFileLinkModulesIntoLocalCache()
		{
			if (analysisFinished_FileLinks && analysisFinished_LocalCache)
			{
				lock (_filelinkModulesToInsert)
					foreach (var mod in _filelinkModulesToInsert)
						LocalFileCache.AddOrUpdate(mod);

				_filelinkModulesToInsert.Clear();
			}
		}

		void LocalIncludeCache_FinishedParsing(ParsePerformanceData[] PerformanceData)
		{
			if (References != null)
				References.FireUpdate();
			analysisFinished_LocalIncludes = true;
			TryBuildUfcsCache();
		}

		void LocalFileCache_FinishedParsing(ParsePerformanceData[] PerformanceData)
		{
			analysisFinished_LocalCache = true;
			_InsertFileLinkModulesIntoLocalCache();
			TryBuildUfcsCache();
		}

		void GlobalParseCache_FinishedParsing(ParsePerformanceData[] PerformanceData)
		{
			analysisFinished_GlobalCache = true;
			TryBuildUfcsCache();
		}

		void TryBuildUfcsCache()
		{
			//TODO: Establish a 'common' includes list.
			if (analysisFinished_GlobalCache && !Compiler.ParseCache.IsParsing &&
				analysisFinished_LocalCache && analysisFinished_LocalIncludes &&
				analysisFinished_FileLinks)
			{
				LocalIncludeCache.UfcsCache.Update(ParseCacheList.Create(Compiler.ParseCache, LocalIncludeCache), null, LocalIncludeCache);
				LocalFileCache.UfcsCache.Update(ParseCache, null, LocalFileCache);
			}
		}

		protected override void OnFileRemovedFromProject(ProjectFileEventArgs e)
		{
			base.OnFileRemovedFromProject(e);

			foreach (var pf in e)
				LocalFileCache.Remove (pf.ProjectFile.FilePath);
		}

		protected override void OnFileRenamedInProject(ProjectFileRenamedEventArgs e)
		{
			base.OnFileRenamedInProject(e);

			foreach (var pf in e){
				LocalFileCache.Remove (pf.OldName);
				//FIXME: Re-add new file
			}
		}

		protected override void OnEndLoad()
		{
			base.OnEndLoad();

			Compiler.ParseCache.FinishedParsing += new D_Parser.Misc.ParseCache.ParseFinishedHandler(GlobalParseCache_FinishedParsing);

			UpdateLocalIncludeCache();
			UpdateParseCache();
		}
		#endregion

		/// <summary>
		/// Returns dependent projects in a topological order (from least to most dependent)
		/// </summary>
		public static List<SolutionItem> GetSortedProjectDependencies(SolutionItem p, ConfigurationSelector cfg=null)
		{
			if (cfg == null)
				cfg = Ide.IdeApp.Workspace.ActiveConfiguration;

			var l = new List<SolutionItem>();
			var r = new List<SolutionItem>(p.GetReferencedItems(cfg));
			var r_deps = new List<IEnumerable<SolutionItem>> ();
			var skippedItems = new List<int>();

			for(var i = r.Count - 1; i >= 0; i--){
				r_deps [i] = r [i].GetReferencedItems(cfg);

				using(var tempEnum = r_deps[i].GetEnumerator())
					if(!tempEnum.MoveNext())
				{
					l.Add(r[i]);
					r.RemoveAt(i);
				}
			}

			// If l.count == 0, there is at least one cycle..

			while(r.Count != 0)
			{
				for(int i = r.Count -1 ; i>=0; i--)
				{
					bool hasNotYetEnlistedChild = true;
					foreach(var ch in r_deps[i])
						if(!l.Contains(ch))
					{
						hasNotYetEnlistedChild = false;
						break;
					}

					if(!hasNotYetEnlistedChild){

						if(skippedItems.Contains(i))
							return new List<SolutionItem>();
						skippedItems.Add(i);
						continue;
					}

					l.Add(r[i]);
					r.RemoveAt(i);
				}
			}

			return l;
		}
	}
}
