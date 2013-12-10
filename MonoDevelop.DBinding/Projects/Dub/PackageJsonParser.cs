using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Converters;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class PackageJsonParser : IFileFormat
	{
		public const string PackageJsonFile = "package.json";

		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.FileName == PackageJsonFile &&
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

		public Core.FilePath GetValidFormatName(object obj, Core.FilePath fileName)
		{
			return fileName.ParentDirectory.Combine(PackageJsonFile);
		}

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			DubProject defaultPackage;
			using (var s = File.OpenText (file))
			using (var r = new JsonTextReader (s))
				defaultPackage = ReadPackageInformation(file, r);

			if (expectedType.IsInstanceOfType(defaultPackage))
				return defaultPackage;

			var sln = new DubSolution();

			if (!expectedType.IsInstanceOfType(sln))
				return null;

			sln.RootFolder.AddItem(defaultPackage, false);
			sln.StartupItem = defaultPackage;

			// Introduce solution configurations
			foreach (var cfg in defaultPackage.Configurations)
				sln.AddConfiguration(cfg.Name, false).Platform = cfg.Platform;

			//TODO: Recursively load all subdependencies? I guess not, huh?
			foreach (var dep in defaultPackage.DubReferences)
			{
				if (string.IsNullOrWhiteSpace(dep.Path))
					continue;

				var packageJsonToLoad = Path.Combine(defaultPackage.GetAbsPath(Building.ProjectBuilder.EnsureCorrectPathSeparators(dep.Path)), PackageJsonFile);
				if (File.Exists(packageJsonToLoad))
				{
					var prj = ReadFile(new FilePath(packageJsonToLoad), typeof(Project), monitor) as SolutionEntityItem;
					if (prj != null)
						sln.RootFolder.AddItem(prj, false);
				}
			}

			sln.LoadUserProperties();

			return sln;
		}

		public static DubProject ReadPackageInformation(FilePath packageJsonPath,JsonReader r)
		{
			var defaultPackage = new DubProject();
			defaultPackage.FileName = packageJsonPath;
			defaultPackage.BaseDirectory = packageJsonPath.ParentDirectory;

			defaultPackage.BeginLoad ();

			defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			while (r.Read ()) {
				if (r.TokenType == JsonToken.PropertyName) {
					var propName = r.Value as string;
					defaultPackage.TryPopulateProperty (propName, r);
				}
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}

			defaultPackage.Items.Add(new ProjectFile(packageJsonPath, BuildAction.None));

			foreach (var f in defaultPackage.GetItemFiles(true))
				defaultPackage.Items.Add(new ProjectFile(f));

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

		public void WriteFile(Core.FilePath file, object obj, Core.IProgressMonitor monitor)
		{
			//monitor.ReportError ("Can't write dub package information! Change it manually in the definition file!", new InvalidOperationException ());
		}
	}
}