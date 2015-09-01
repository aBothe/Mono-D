using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	class DubJson : DubFileReader
	{
		public const string PackageJsonFile = "package.json";
		public const string DubJsonFile = "dub.json";
		public const string DubSelectionsJsonFile = "dub.selections.json";



		public static bool CanReadFile(string file)
		{
			file = System.IO.Path.GetFileName(file).ToLower();
			return file == DubJsonFile || file == PackageJsonFile;
		}


		public static string GetDubJsonFilePath(AbstractDProject @base, string subPath)
		{
			var sub = @base as DubSubPackage;
			if (sub != null)
				sub.useOriginalBasePath = true;
			var packageDir = @base.GetAbsPath(Building.ProjectBuilder.EnsureCorrectPathSeparators(subPath));

			if (sub != null)
				sub.useOriginalBasePath = false;

			string packageJsonToLoad;
			if (File.Exists(packageJsonToLoad = Path.Combine(packageDir, PackageJsonFile)) ||
				File.Exists(packageJsonToLoad = Path.Combine(packageDir, DubJsonFile)))
				return packageJsonToLoad;

			return null;
		}

		public bool TryPopulateProperty(string propName, JsonReader j, IProgressMonitor monitor)
		{
			switch (propName.ToLowerInvariant())
			{
				case "displayname":
					displayName = j.ReadAsString();
					break;
				case "name":
					packageName = j.ReadAsString();
					break;
				case "description":
					Description = j.ReadAsString();
					break;
				case "copyright":
					Copyright = j.ReadAsString();
					break;
				case "homepage":
					Homepage = j.ReadAsString();
					break;
				case "authors":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Authors");
					authors.Clear();
					while (j.Read() && j.TokenType != JsonToken.EndArray)
						if (j.TokenType == JsonToken.String)
							authors.Add(j.Value as string);
					break;
				case "dependencies":
					if (!j.Read() || j.TokenType != JsonToken.StartObject)
						throw new JsonReaderException("Expected { when parsing Authors");

					DubReferences.DeserializeDubPrjDependencies(j, monitor);
					break;
				case "configurations":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Configurations");

					if (ParentSolution != null && ParentSolution.Configurations.Count == 1 && ParentSolution.Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
						ParentSolution.Configurations.Clear();
					if (Configurations.Count == 1 && Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
						Configurations.Clear();

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						AddProjectAndSolutionConfiguration(DubProjectConfiguration.DeserializeFromPackageJson(j));
					break;
				case "subpackages":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing subpackages");

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						DubSubPackage.ReadAndAdd(this, j, monitor);
					break;
				case "buildtypes":
					if (!j.Read() || j.TokenType != JsonToken.StartObject)
						throw new JsonReaderException("Expected [ when parsing build types");

					while (j.Read() && j.TokenType != JsonToken.EndObject)
					{
						var n = j.Value as string;
						if (!buildTypes.Contains(n))
							buildTypes.Add(n);

						j.Skip();
					}

					buildTypes.Sort();
					break;
				default:
					return CommonBuildSettings.TryDeserializeBuildSetting(j);
			}

			return true;
		}

		public static DubProject ReadPackageInformation(FilePath packageJsonPath, IProgressMonitor monitor, JsonReader r = null, DubProject superPackage = null)
		{
			DubProject defaultPackage;
			bool free;
			StreamReader s = null;
			bool cleanupAlreadyLoadedPacks = AlreadyLoadedPackages == null;
			if (cleanupAlreadyLoadedPacks)
				AlreadyLoadedPackages = new Dictionary<string, DubProject>();

			if (free = (r == null))
			{
				if (AlreadyLoadedPackages.TryGetValue(packageJsonPath, out defaultPackage))
				{
					if (cleanupAlreadyLoadedPacks)
						AlreadyLoadedPackages = null;
					return defaultPackage;
				}

				s = File.OpenText(packageJsonPath);
				r = new JsonTextReader(s);
			}

			defaultPackage = superPackage != null ? new DubSubPackage() : new DubProject();
			try
			{
				defaultPackage.FileName = packageJsonPath;
				AlreadyLoadedPackages[packageJsonPath] = defaultPackage;
				defaultPackage.BaseDirectory = packageJsonPath.ParentDirectory;

				defaultPackage.BeginLoad();

				defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

				if (superPackage != null)
					superPackage.packagesToAdd.Add(defaultPackage);

				while (r.Read())
				{
					if (r.TokenType == JsonToken.PropertyName)
					{
						var propName = r.Value as string;
						defaultPackage.TryPopulateProperty(propName, r, monitor);
					}
					else if (r.TokenType == JsonToken.EndObject)
						break;
				}

				if (superPackage != null)
					defaultPackage.packageName = superPackage.packageName + ":" + (defaultPackage.packageName ?? string.Empty);

				defaultPackage.Items.Add(new ProjectFile(packageJsonPath, BuildAction.None));

				// https://github.com/aBothe/Mono-D/issues/555
				var dubSelectionJsonPath = packageJsonPath.ParentDirectory.Combine(DubSelectionsJsonFile);
				if (File.Exists(dubSelectionJsonPath))
					defaultPackage.Items.Add(new ProjectFile(dubSelectionJsonPath, BuildAction.None));

				defaultPackage.EndLoad();
			}
			catch (Exception ex)
			{
				monitor.ReportError("Exception while reading dub package " + packageJsonPath, ex);
			}
			finally
			{
				if (free)
				{
					r.Close();
					s.Dispose();
				}

				if (cleanupAlreadyLoadedPacks)
					AlreadyLoadedPackages = null;
			}
			return defaultPackage;
		}

		public static DubSubPackage ReadAndAdd(DubProject superProject,JsonReader r, IProgressMonitor monitor)
		{
			DubSubPackage sub;
			switch (r.TokenType) {
				case JsonToken.StartObject:
					break;
				case JsonToken.String:

					sub = DubFileFormat.ReadPackageInformation (DubFileFormat.GetDubJsonFilePath (superProject, r.Value as string), monitor, null, superProject) as DubSubPackage;
					return sub;
				default:
					throw new JsonReaderException ("Illegal token on subpackage definition beginning");
			}

			sub = new DubSubPackage ();
			sub.FileName = superProject.FileName;

			sub.OriginalBasePath = superProject is DubSubPackage ? (superProject as DubSubPackage).OriginalBasePath : 
				superProject.BaseDirectory;
			sub.VirtualBasePath = sub.OriginalBasePath;

			sub.BeginLoad ();

			sub.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			superProject.packagesToAdd.Add(sub);

			while (r.Read ()) {
				if (r.TokenType == JsonToken.PropertyName)
					sub.TryPopulateProperty (r.Value as string, r, monitor);
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}
				
			sub.packageName = superProject.packageName + ":" + (sub.packageName ?? string.Empty);

			var sourcePaths = sub.GetSourcePaths ().ToArray();
			if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
				sub.VirtualBasePath = new FilePath(sourcePaths [0]);

			DubFileFormat.LoadDubProjectReferences (sub, monitor);

			// TODO: What to do with new configurations that were declared in this sub package? Add them to all other packages as well?
			sub.EndLoad ();

			if (r.TokenType != JsonToken.EndObject)
				throw new JsonReaderException ("Illegal token on subpackage definition end");
			return sub;
		}
	}
}
