using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class PackageJsonParser : IFileFormat
	{
		public const string PackageJsonFile = "package.json";
		public const string DubJsonFile = "dub.json";

		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return (file.FileName == DubJsonFile || file.FileName == PackageJsonFile) &&
				(expectedObjectType.Equals(typeof(WorkspaceItem)) ||
				expectedObjectType.Equals(typeof(SolutionEntityItem)));
		}

		public bool CanWriteFile(object obj)
		{
			return true; // Everything has to be manipulated manually (atm)!
		}

		public void ConvertToFormat(object obj)
		{
			
		}

		public IEnumerable<string> GetCompatibilityWarnings(object obj)
		{
			yield return string.Empty;
		}

		public List<FilePath> GetItemFiles(object obj)
		{
			return new List<FilePath>();
		}

		public FilePath GetValidFormatName(object obj, FilePath fileName)
		{
			return fileName;
		}

		[ThreadStatic]
		internal static Dictionary<string, DubProject> AlreadyLoadedPackages;

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			return PackageJsonParser.ReadFile_(file, expectedType, monitor);
		}

		public static object ReadFile_(string file, Type expectedType, IProgressMonitor monitor)
		{
			DubProject defaultPackage;
			bool clearLoadedPrjList = AlreadyLoadedPackages == null;
			if (clearLoadedPrjList)
				AlreadyLoadedPackages = new Dictionary<string, DubProject> ();

			if (AlreadyLoadedPackages.TryGetValue (file, out defaultPackage))
				return defaultPackage;


			try{
				defaultPackage = ReadPackageInformation(file, monitor);
			}catch(Exception ex){
				if (clearLoadedPrjList)
					AlreadyLoadedPackages = null;
				monitor.ReportError ("Couldn't load dub package \"" + file + "\"", ex);
				return null;
			}

			if (expectedType.IsInstanceOfType (defaultPackage)) {
				LoadDubProjectReferences (defaultPackage, monitor);

				if (clearLoadedPrjList)
					AlreadyLoadedPackages = null;

				return defaultPackage;
			}

			var sln = new DubSolution();

			if (!expectedType.IsInstanceOfType (sln)) {
				if (clearLoadedPrjList)
					AlreadyLoadedPackages = null;
				return null;
			}

			sln.RootFolder.AddItem(defaultPackage, false);
			sln.StartupItem = defaultPackage;

			// Introduce solution configurations
			foreach (var cfg in defaultPackage.Configurations)
				sln.AddConfiguration(cfg.Name, false).Platform = cfg.Platform;

			LoadDubProjectReferences (defaultPackage, monitor, sln);

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

			if (clearLoadedPrjList) {
				AlreadyLoadedPackages = null;

				// Clear 'dub list' outputs
				DubReferencesCollection.DubListOutputs.Clear ();
			}

			return sln;
		}

		public static string GetDubJsonFilePath(AbstractDProject @base,string subPath)
		{
			var sub = @base as DubSubPackage;
			if (sub != null)
				sub.useOriginalBasePath = true;
			var packageDir = @base.GetAbsPath(Building.ProjectBuilder.EnsureCorrectPathSeparators(subPath));

			if(sub != null)
				sub.useOriginalBasePath = false;

			string packageJsonToLoad;
			if (File.Exists (packageJsonToLoad = Path.Combine (packageDir, PackageJsonFile)) ||
			    File.Exists (packageJsonToLoad = Path.Combine (packageDir, DubJsonFile)))
				return packageJsonToLoad;

			return null;
		}

		internal static void LoadDubProjectReferences(DubProject defaultPackage, IProgressMonitor monitor, Solution sln = null)
		{
			foreach (var dep in defaultPackage.DubReferences)
			{
				if (string.IsNullOrWhiteSpace(dep.Path))
					continue;
					
				string packageJsonToLoad = GetDubJsonFilePath(defaultPackage, dep.Path);
				if (packageJsonToLoad != null && packageJsonToLoad != defaultPackage.FileName)
				{
					var prj = ReadFile_(packageJsonToLoad, typeof(Project), monitor) as DubProject;
					if (prj != null) {
						if (sln != null)
							sln.RootFolder.AddItem (prj, false);
						else
							defaultPackage.packagesToAdd.Add (prj);
					}
				}
			}
		}

		public static DubProject ReadPackageInformation(FilePath packageJsonPath,IProgressMonitor monitor,JsonReader r = null, DubProject superPackage = null)
		{
			DubProject defaultPackage;
			bool free;
			StreamReader s = null;
			bool cleanupAlreadyLoadedPacks = AlreadyLoadedPackages == null;
			if (cleanupAlreadyLoadedPacks)
				AlreadyLoadedPackages = new Dictionary<string, DubProject> ();

			if (free = (r == null)) {
				if (AlreadyLoadedPackages.TryGetValue (packageJsonPath, out defaultPackage)) {
					if (cleanupAlreadyLoadedPacks)
						AlreadyLoadedPackages = null;
					return defaultPackage;
				}

				s = File.OpenText (packageJsonPath);
				r = new JsonTextReader (s);
			}

			defaultPackage = superPackage != null ? new DubSubPackage() : new DubProject();
			try
			{
				defaultPackage.FileName = packageJsonPath;
				AlreadyLoadedPackages[packageJsonPath] = defaultPackage;
				defaultPackage.BaseDirectory = packageJsonPath.ParentDirectory;

				defaultPackage.BeginLoad ();

				defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

				if(superPackage != null)
					superPackage.packagesToAdd.Add(defaultPackage);

				while (r.Read ()) {
					if (r.TokenType == JsonToken.PropertyName) {
						var propName = r.Value as string;
						defaultPackage.TryPopulateProperty (propName, r, monitor);
					}
					else if (r.TokenType == JsonToken.EndObject)
						break;
				}

				if(superPackage != null)
					defaultPackage.packageName = superPackage.packageName + ":" + (defaultPackage.packageName ?? string.Empty);

				defaultPackage.Items.Add(new ProjectFile(packageJsonPath, BuildAction.None));

				defaultPackage.EndLoad ();
			}
			catch(Exception ex) {
				monitor.ReportError ("Exception while reading dub package "+packageJsonPath,ex);
			}
			finally{
				if (free) {
					r.Close ();
					s.Dispose ();
				}

				if (cleanupAlreadyLoadedPacks)
					AlreadyLoadedPackages = null;
			}
			return defaultPackage;
		}

		public bool SupportsFramework(Core.Assemblies.TargetFramework framework)
		{
			return false;
		}

		public bool SupportsMixedFormats
		{
			get { return true; }
		}

		public void WriteFile(FilePath file, object obj, IProgressMonitor monitor)
		{
			//monitor.ReportError ("Can't write dub package information! Change it manually in the definition file!", new InvalidOperationException ());
		}
	}
}