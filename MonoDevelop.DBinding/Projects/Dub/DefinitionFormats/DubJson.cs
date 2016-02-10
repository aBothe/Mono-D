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
		public override bool CanLoad(string file)
		{
			file = Path.GetFileName(file).ToLower();
			return file == DubJsonFile || file == PackageJsonFile;
		}

		protected override void Read(DubProject target, Object input)
		{
			if (input is JsonReader)
				Parse(input as JsonReader, target);
			else if (input is TextReader)
			{
				using (var r = new JsonTextReader(input as TextReader))
				{
					Parse(r, target);
				}
			}
			else
				throw new ArgumentException("input");
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

					DeserializeDubPrjDependencies(j, prj.CommonBuildSettings);
					break;
				case "configurations":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Configurations");

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						IntroduceConfiguration(prj, DeserializeFromPackageJson(j));
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
			switch (r.TokenType)
			{
				case JsonToken.StartObject:
					Load(superProject, superProject.ParentSolution, r, superProject.FileName);
					break;
				case JsonToken.String:
					DubFileManager.Instance.LoadProject(GetDubFilePath(superProject, r.Value as string), superProject.ParentSolution, null, DubFileManager.LoadFlags.None, superProject);
					break;
				default:
					throw new JsonReaderException("Illegal token on subpackage definition beginning");
			}
		}

		void DeserializeDubPrjDependencies(JsonReader j, DubBuildSettings settings)
		{
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

					settings.dependencies[depName] = new DubProjectDependency { Name = depName, Version = depVersion, Path = depPath };
				}
			}
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

			var propName = settingIdentifier[0] = settingIdentifier[0].ToLowerInvariant();
			DubBuildSetting sett = null;

			switch (propName)
			{
				case "dependencies":
					j.Read();
					DeserializeDubPrjDependencies(j, cfg);
					break;
				case "targettype":
				case "targetname":
				case "targetpath":
				case "workingdirectory":
				case "mainsourcefile":
					j.Read();
					if (j.TokenType == JsonToken.String)
					{
						sett = new DubBuildSetting { Name = propName, Values = new[] { j.Value as string } };
					}
					break;
				case "subconfigurations":
					j.Read();
					var configurations = (new JsonSerializer()).Deserialize<Dictionary<string, string>>(j);
					foreach (var kv in configurations)
						cfg.subConfigurations[kv.Key] = kv.Value;
					break;
				case "sourcefiles":
				case "sourcepaths":
				case "excludedsourcefiles":
				case "versions":
				case "debugversions":
				case "importpaths":
				case "stringimportpaths":
					j.Read();
					if (j.TokenType == JsonToken.StartArray)
					{
						sett = new DubBuildSetting { Name = propName, Values = (new JsonSerializer()).Deserialize<string[]>(j) };

						for (int i = 1; i < settingIdentifier.Length; i++)
						{
							var pn = settingIdentifier[i].ToLowerInvariant();
							if (sett.OperatingSystem == null && DubBuildSettings.OsVersions.Contains(pn))
								sett.OperatingSystem = pn;
							else if (sett.Architecture == null && DubBuildSettings.Architectures.Contains(pn))
								sett.Architecture = pn;
							else
								sett.Compiler = pn;
						}
					}
					break;
				default:
					j.Skip();
					return false;
			}

			if (sett != null)
			{
				List<DubBuildSetting> setts;
				if (!cfg.TryGetValue(settingIdentifier[0], out setts))
					cfg.Add(settingIdentifier[0], setts = new List<DubBuildSetting>());

				setts.Add(sett);
			}

			return true;
		}
	}
}
