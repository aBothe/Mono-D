using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using MonoDevelop.Projects;
using System.Text.RegularExpressions;
using MonoDevelop.D.Building;
using MonoDevelop.Core;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	class DubJson : DubFileReader
	{
		public const string PackageJsonFile = "package.json";
		public const string DubJsonFile = "dub.json";
		public const string DubSelectionsJsonFile = "dub.selections.json";

		public override bool CanLoad (string file)
		{
			file = Path.GetFileName(file).ToLower();
			return file == DubJsonFile || file == PackageJsonFile;
		}

		protected override void Read (DubProject target, Object input)
		{
			if (input is JsonReader)
				Parse (input as JsonReader, target);
			else if (input is TextReader) {
				using (var r = new JsonTextReader (input as TextReader)) {
					Parse (r, target);
				}
			} else
				throw new ArgumentException ("input");
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

					DeserializeDubPrjDependencies(j, prj);
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
					prj.CommonBuildSettings.TryDeserializeBuildSetting(j);
					break;
			}
		}

		void ReadSubPackage(DubProject superProject, JsonReader r)
		{
			switch (r.TokenType) {
				case JsonToken.StartObject:
					Load(superProject, superProject.ParentSolution, r, superProject.FileName);
					break;
				case JsonToken.String:
					DubFileManager.Instance.LoadProject (GetDubJsonFilePath (superProject, r.Value as string), superProject.ParentSolution, null, DubFileManager.LoadFlags.None, superProject);
					break;
				default:
					throw new JsonReaderException ("Illegal token on subpackage definition beginning");
			}
		}

		void DeserializeDubPrjDependencies(JsonReader j, DubProject prj)
		{
			prj.DubReferences.dependencies.Clear();
			bool tryFillRemainingPaths = false;

			while (j.Read() && j.TokenType != JsonToken.EndObject)
			{
				if (j.TokenType == JsonToken.PropertyName)
				{
					var depName = j.Value as string;
					string depVersion = null;
					string depPath = null;

					if (!j.Read())
						throw new JsonReaderException("Found EOF when parsing project dependency");

					if (j.TokenType == JsonToken.StartObject)
					{
						while (j.Read() && j.TokenType != JsonToken.EndObject)
						{
							if (j.TokenType == JsonToken.PropertyName)
							{
								switch (j.Value as string)
								{
									case "version":
										depVersion = j.ReadAsString();
										break;
									case "path":
										depPath = j.ReadAsString();
										break;
								}
							}
						}
					}
					else if (j.TokenType == JsonToken.String)
						depVersion = j.Value as string;

					tryFillRemainingPaths |= string.IsNullOrEmpty(depPath);
					prj.DubReferences.dependencies[depName] = new DubProjectDependency { Name = depName, Version = depVersion, Path = depPath };
				}
			}

			if (tryFillRemainingPaths)
				FillDubReferencesPaths(prj);
			else
				prj.DubReferences.FireUpdate();			
		}
	}
}
