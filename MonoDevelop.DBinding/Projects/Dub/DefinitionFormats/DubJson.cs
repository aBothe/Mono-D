using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	class DubJson : DubFileReader
	{
		public const string PackageJsonFile = "package.json";
		public const string DubJsonFile = "dub.json";
		public const string DubSelectionsJsonFile = "dub.selections.json";

		public override bool CanLoad (string file)
		{
			file = System.IO.Path.GetFileName(file).ToLower();
			return file == DubJsonFile || file == PackageJsonFile;
		}

		public override void Load (DubProject target, StreamReader s)
		{
			using (var r = new JsonTextReader (s)) {
				Parse (r, target);
			}
		}

		void Parse(JsonReader r, DubProject prj)
		{
			while (r.Read())
			{
				if (r.TokenType == JsonToken.PropertyName)
				{
					var propName = r.Value as string;
					TryPopulateProperty(prj, propName, r);
				}
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}

			// https://github.com/aBothe/Mono-D/issues/555
			var dubSelectionJsonPath = prj.BaseDirectory.Combine(DubSelectionsJsonFile);
			if (File.Exists(dubSelectionJsonPath))
				prj.Items.Add(new ProjectFile(dubSelectionJsonPath, BuildAction.None));
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

		void TryPopulateProperty(DubProject prj, string propName, JsonReader j)
		{
			switch (propName.ToLowerInvariant())
			{
				case "displayname":
					prj.Name = j.ReadAsString();
					break;
				case "name":
					prj.packageName = j.ReadAsString();
					break;
				case "description":
					prj.Description = j.ReadAsString();
					break;
				case "copyright":
					prj.Copyright = j.ReadAsString();
					break;
				case "homepage":
					prj.Homepage = j.ReadAsString();
					break;
				case "authors":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Authors");
					prj.Authors.Clear();
					while (j.Read() && j.TokenType != JsonToken.EndArray)
						if (j.TokenType == JsonToken.String)
							prj.Authors.Add(j.Value as string);
					break;
				case "dependencies":
					if (!j.Read() || j.TokenType != JsonToken.StartObject)
						throw new JsonReaderException("Expected { when parsing Authors");

					prj.DubReferences.DeserializeDubPrjDependencies(j);
					break;
				case "configurations":
					if (!j.Read () || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException ("Expected [ when parsing Configurations");
					var sln = prj.ParentSolution;
					if (sln != null && sln.Configurations.Count == 1 && sln.Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
						sln.Configurations.Clear();
					if (prj.Configurations.Count == 1 && prj.Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
						prj.Configurations.Clear();

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						prj.AddProjectAndSolutionConfiguration(DubProjectConfiguration.DeserializeFromPackageJson(j));
					break;
				case "subpackages":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing subpackages");

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						ReadSubPackage(prj, j);
					break;
				case "buildtypes":
					if (!j.Read() || j.TokenType != JsonToken.StartObject)
						throw new JsonReaderException("Expected [ when parsing build types");

					while (j.Read() && j.TokenType != JsonToken.EndObject)
					{
						var n = j.Value as string;
						if (!prj.buildTypes.Contains(n))
							prj.buildTypes.Add(n);

						j.Skip();
					}

					prj.buildTypes.Sort();

					break;
				default:
					return prj.CommonBuildSettings.TryDeserializeBuildSetting(j);
			}

			return true;
		}

		void ReadSubPackage(DubProject superProject, JsonReader r)
		{
			switch (r.TokenType) {
				case JsonToken.StartObject:
					break;
				case JsonToken.String:

					sub = DubFileFormat.ReadPackageInformation (DubFileFormat.GetDubJsonFilePath (superProject, r.Value as string), monitor, null, superProject) as DubSubPackage;
					return sub;
				default:
					throw new JsonReaderException ("Illegal token on subpackage definition beginning");
			}

			//DubFileManager.Instance.LoadSubPackage (superProject.FileName, superProject);
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
