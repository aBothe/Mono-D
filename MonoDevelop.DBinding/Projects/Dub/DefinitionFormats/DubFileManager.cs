using MonoDevelop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public class DubFileManager
	{
		#region Properties
		public static readonly DubFileManager Instance = new DubFileManager();
		[ThreadStatic]
		static Dictionary<string, DubProject> filesBeingLoaded;
		static Dictionary<string, DubProject> FilesBeingLoaded
		{
			get {
				if (filesBeingLoaded == null)
					filesBeingLoaded = new Dictionary<string, DubProject>();
				return filesBeingLoaded;
			}
		}

		class FilesBeingLoadedCleanser : IDisposable
		{
			readonly bool clean;

			public FilesBeingLoadedCleanser()
			{
				clean = filesBeingLoaded == null;
			}

			public void Dispose()
			{
				if (clean) {
					filesBeingLoaded = null;

					// Clear 'dub list' outputs
					DubReferencesCollection.DubListOutputs.Clear();
				}
			}
		}

		readonly HashSet<DubFileReader> supportedDubFileFormats = new HashSet<DubFileReader>
		{
			new DubJson(),
		};
		#endregion

		DubFileManager() { }

		public bool CanLoad(string file)
		{
			return supportedDubFileFormats.Any((i) => i.CanLoad(file));
		}

		public DubSolution LoadAsSolution(string file, IProgressMonitor monitor)
		{
			using (new FilesBeingLoadedCleanser())
			{

			}
			
			var sln = new DubSolution();
			sln.RootFolder.AddItem(defaultPackage, false);
			sln.StartupItem = defaultPackage;

			// Introduce solution configurations
			foreach (var cfg in defaultPackage.Configurations)
				sln.AddConfiguration(cfg.Name, false).Platform = cfg.Platform;

			LoadDubProjectReferences(defaultPackage, monitor, sln);

			// Apply subConfigurations
			var subConfigurations = new Dictionary<string, string>(defaultPackage.CommonBuildSettings.subConfigurations);

			foreach (var item in sln.Items)
			{
				var prj = item as SolutionEntityItem;
				if (prj == null)
					continue;

				foreach (var cfg in sln.Configurations)
				{
					var prjItem = cfg.GetEntryForItem(prj);
					string cfgId;
					if (subConfigurations.TryGetValue(prj.ItemId, out cfgId))
						prjItem.ItemConfiguration = cfgId;

					var prjCfg = defaultPackage.GetConfiguration(cfg.Selector) as DubProjectConfiguration;
					if (prjCfg != null && prjCfg.BuildSettings.subConfigurations.TryGetValue(prj.ItemId, out cfgId))
						prjItem.ItemConfiguration = cfgId;
				}
			}

			sln.LoadUserProperties();

			if (clearLoadedPrjList)
			{
				AlreadyLoadedPackages = null;

				// Clear 'dub list' outputs
				DubReferencesCollection.DubListOutputs.Clear();
			}

			return sln;
		}

		public DubProject LoadProject(string file, DubSolution parentSolution, IProgressMonitor monitor, bool loadDubPrjReferences = true)
		{
			DubProject prj;

			if (FilesBeingLoaded.TryGetValue(file, out prj))
				return prj;

			try
			{
				prj = supportedDubFileFormats.First((i) => i.CanLoad(file)).Load(file);
			}
			catch (Exception ex)
			{
				if (clearLoadedPrjList)
					AlreadyLoadedPackages = null;
				monitor.ReportError("Couldn't load dub package \"" + file + "\"", ex);
				return null;
			}

			LoadDubProjectReferences(defaultPackage, monitor);

		}

		public void LoadSubProjects(DubProject defaultPackage, IProgressMonitor monitor)
		{
			foreach (var dep in defaultPackage.DubReferences)
			{
				if (string.IsNullOrWhiteSpace(dep.Path))
					continue;

				string packageJsonToLoad = GetDubJsonFilePath(defaultPackage, dep.Path);
				if (packageJsonToLoad != null && packageJsonToLoad != defaultPackage.FileName)
				{
					var prj = ReadFile_(packageJsonToLoad, typeof(Project), monitor) as DubProject;
					if (prj != null)
					{
						if (sln != null)
						{
							if (sln is DubSolution)
								(sln as DubSolution).AddProject(prj);
							else
								sln.RootFolder.AddItem(prj, false);
						}
						else
							defaultPackage.packagesToAdd.Add(prj);
					}
				}
			}
		}

		public static DubSubPackage ReadAndAdd(DubProject superProject, JsonReader r, IProgressMonitor monitor)
		{
			DubSubPackage sub;
			switch (r.TokenType)
			{
				case JsonToken.StartObject:
					break;
				case JsonToken.String:

					sub = DubFileFormat.ReadPackageInformation(DubFileFormat.GetDubJsonFilePath(superProject, r.Value as string), monitor, null, superProject) as DubSubPackage;
					return sub;
				default:
					throw new JsonReaderException("Illegal token on subpackage definition beginning");
			}

			sub = new DubSubPackage();
			sub.FileName = superProject.FileName;

			sub.OriginalBasePath = superProject is DubSubPackage ? (superProject as DubSubPackage).OriginalBasePath :
				superProject.BaseDirectory;
			sub.VirtualBasePath = sub.OriginalBasePath;

			sub.BeginLoad();

			sub.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			superProject.packagesToAdd.Add(sub);

			while (r.Read())
			{
				if (r.TokenType == JsonToken.PropertyName)
					sub.TryPopulateProperty(r.Value as string, r, monitor);
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}

			sub.packageName = superProject.packageName + ":" + (sub.packageName ?? string.Empty);

			var sourcePaths = sub.GetSourcePaths().ToArray();
			if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
				sub.VirtualBasePath = new FilePath(sourcePaths[0]);

			DubFileFormat.LoadDubProjectReferences(sub, monitor);

			// TODO: What to do with new configurations that were declared in this sub package? Add them to all other packages as well?
			sub.EndLoad();

			if (r.TokenType != JsonToken.EndObject)
				throw new JsonReaderException("Illegal token on subpackage definition end");
			return sub;
		}
	}
}
