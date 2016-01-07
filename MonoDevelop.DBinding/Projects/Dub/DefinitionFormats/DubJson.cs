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
						prj.AddProjectAndSolutionConfiguration(DeserializeFromPackageJson(j));
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
					TryDeserializeBuildSetting(prj.CommonBuildSettings, j);
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
					DubFileManager.Instance.LoadProject (GetDubFilePath(superProject, r.Value as string), superProject.ParentSolution, null, DubFileManager.LoadFlags.None, superProject);
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

		DubProjectConfiguration DeserializeFromPackageJson(JsonReader j)
		{
			var c = new DubProjectConfiguration { Name = "<Undefined>" };

			var srz = new JsonSerializer();
			while (j.Read() && j.TokenType != JsonToken.EndObject)
			{
				if (j.TokenType == JsonToken.PropertyName)
				{
					switch (j.Value as string)
					{
						case "name":
							c.Name = c.Id = j.ReadAsString();
							break;
						case "platforms":
							j.Read();
							c.Platform = string.Join("|", srz.Deserialize<string[]>(j));
							break;
						default:
							if (!TryDeserializeBuildSetting(c.BuildSettings, j))
								j.Skip();
							break;
					}
				}
			}

			return c;
		}

		bool TryDeserializeBuildSetting(DubBuildSettings cfg, JsonReader j)
		{
			if (!(j.Value is string))
				return false;
			var settingIdentifier = (j.Value as string).Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
			if (settingIdentifier.Length < 1)
				return false;

			settingIdentifier[0] = settingIdentifier[0].ToLowerInvariant();
			if (!DubBuildSettings.WantedProps.Contains(settingIdentifier[0]))
			{
				if (settingIdentifier[0] == "subconfigurations")
				{
					j.Read();
					var configurations = (new JsonSerializer()).Deserialize<Dictionary<string, string>>(j);
                    foreach (var kv in configurations)
						cfg.subConfigurations[kv.Key] = kv.Value;
					return true;
				}

				j.Skip();
				return false;
			}

			j.Read();
			string[] flags;

			if (j.TokenType == JsonToken.String)
				flags = new[] { j.Value as string };
			else if (j.TokenType == JsonToken.StartArray)
				flags = (new JsonSerializer()).Deserialize<string[]>(j);
			else
			{
				j.Skip();
				//TODO: Probably throw or notify the user someway else
				flags = null;
				return true;
			}

			DubBuildSetting sett;

			if (settingIdentifier.Length == 4)
			{
				sett = new DubBuildSetting
				{
					Name = settingIdentifier[0],
					OperatingSystem = settingIdentifier[1],
					Architecture = settingIdentifier[2],
					Compiler = settingIdentifier[3],
					Values = flags
				};
			}
			else if (settingIdentifier.Length == 1)
				sett = new DubBuildSetting { Name = settingIdentifier[0], Values = flags };
			else
			{
				string Os = null;
				string Arch = null;
				string Compiler = null;

				for (int i = 1; i < settingIdentifier.Length; i++)
				{
					var pn = settingIdentifier[i].ToLowerInvariant();
					if (Os == null && DubBuildSettings.OsVersions.Contains(pn))
						Os = pn;
					else if (Arch == null && DubBuildSettings.Architectures.Contains(pn))
						Arch = pn;
					else
						Compiler = pn;
				}

				sett = new DubBuildSetting { Name = settingIdentifier[0], OperatingSystem = Os, Architecture = Arch, Compiler = Compiler, Values = flags };
			}

			List<DubBuildSetting> setts;
			if (!cfg.TryGetValue(settingIdentifier[0], out setts))
				cfg.Add(settingIdentifier[0], setts = new List<DubBuildSetting>());

			setts.Add(sett);

			return true;
		}
	}
}
