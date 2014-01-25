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
		internal static List<string> AlreadyLoadedPackages;

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			return PackageJsonParser.ReadFile_(file, expectedType, monitor);
		}

		public static object ReadFile_(string file, Type expectedType, IProgressMonitor monitor)
		{
			bool clearLoadedPrjList = AlreadyLoadedPackages == null;
			if (clearLoadedPrjList)
				AlreadyLoadedPackages = new List<string> ();

			if (AlreadyLoadedPackages.Contains (file))
				return null;
			AlreadyLoadedPackages.Add (file);

			DubProject defaultPackage;
			try{
				using (var s = File.OpenText (file))
				using (var r = new JsonTextReader (s))
					defaultPackage = ReadPackageInformation(file, r, monitor);
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

			sln.LoadUserProperties();

			if (clearLoadedPrjList) {
				AlreadyLoadedPackages = null;

				// Clear 'dub list' outputs
				DubReferencesCollection.DubListOutputs.Clear ();
			}

			return sln;
		}

		internal static void LoadDubProjectReferences(DubProject defaultPackage, IProgressMonitor monitor, Solution sln = null)
		{
			var sub = defaultPackage as DubSubPackage;
			foreach (var dep in defaultPackage.DubReferences)
			{
				if (string.IsNullOrWhiteSpace(dep.Path))
					continue;

				if (sub != null)
					sub.useOriginalBasePath = true;
				var packageDir = defaultPackage.GetAbsPath(Building.ProjectBuilder.EnsureCorrectPathSeparators(dep.Path));

				if(sub != null)
					sub.useOriginalBasePath = false;

				string packageJsonToLoad;
				if (File.Exists(packageJsonToLoad = Path.Combine(packageDir, PackageJsonFile)) || 
					File.Exists(packageJsonToLoad = Path.Combine(packageDir, DubJsonFile)))
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

		public static DubProject ReadPackageInformation(FilePath packageJsonPath,JsonReader r, IProgressMonitor monitor)
		{
			var defaultPackage = new DubProject();
			defaultPackage.FileName = packageJsonPath;
			defaultPackage.BaseDirectory = packageJsonPath.ParentDirectory;

			defaultPackage.BeginLoad ();

			defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			while (r.Read ()) {
				if (r.TokenType == JsonToken.PropertyName) {
					var propName = r.Value as string;
					defaultPackage.TryPopulateProperty (propName, r, monitor);
				}
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}

			defaultPackage.Items.Add(new ProjectFile(packageJsonPath, BuildAction.None));

			defaultPackage.EndLoad ();
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