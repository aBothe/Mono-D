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
			file = Path.GetFileName (file).ToLower ();
			return file == DubJsonFile || file == PackageJsonFile;
		}

		protected override void Read(DubProject target, Object input, List<Object> subPackages)
		{
			var json = input as JSONObject;

			if (input is TextReader)
				json = new JSONThingDeserializer().Deserialize (input as TextReader);
			else if(json == null)
				throw new ArgumentException("input");

			foreach (var kv in json.Properties)
				TryPopulateProperty (target, kv.Key, kv.Value, subPackages);
		}

		string ExpectJsonStringValue(JSONThing j, string propName)
		{
			if (!(j is JSONValueLeaf))
				throw new InvalidDataException ("Invalid value type for property "+propName);

			return (j as JSONValueLeaf).Value;
		}

		JSONArray ExpectJsonArray(JSONThing j, string propName)
		{
			if (!(j is JSONArray))
				throw new InvalidDataException ("Expected array as value for property "+propName);

			return j as JSONArray;
		}

		JSONObject ExpectJsonObject(JSONThing j, string propName)
		{
			if (!(j is JSONObject))
				throw new InvalidDataException ("Expected object as value for property "+propName);

			return j as JSONObject;
		}

		void TryPopulateProperty(DubProject prj, string propName, JSONThing j, List<Object> subPackages)
		{
			switch (propName)
			{
				case "displayname":
					prj.Name = ExpectJsonStringValue (j, propName);
					break;
				case "name":
					prj.packageName = ExpectJsonStringValue (j, propName);
					break;
				case "description":
					prj.Description = ExpectJsonStringValue (j, propName);
					break;
				case "copyright":
					prj.Copyright = ExpectJsonStringValue (j, propName);
					break;
				case "homepage":
					prj.Homepage = ExpectJsonStringValue (j, propName);
					break;
				case "authors":
					prj.Authors.Clear();
					foreach(var authorValue in ExpectJsonArray(j, propName).Items)
						prj.Authors.Add(ExpectJsonStringValue(authorValue, propName));
					break;
				case "dependencies":
					DeserializeDubPrjDependencies(ExpectJsonObject(j, propName), prj.CommonBuildSettings);
					break;
				case "configurations":
					foreach(var o in ExpectJsonArray(j, propName).Items)
						IntroduceConfiguration(prj, DeserializeFromPackageJson(ExpectJsonObject(o, propName)));
					break;
				case "subpackages":
					subPackages.AddRange (ExpectJsonArray(j, propName).Items);
					break;
				case "buildtypes":
					foreach (var kv in ExpectJsonObject(j, propName).Properties)
						prj.buildTypes.Add (kv.Key);
					break;
				default:
					TryDeserializeBuildSetting(prj.CommonBuildSettings, propName, j);
					break;
			}
		}

		protected override DubProject ReadSubPackage(DubProject superProject, Object definition)
		{
			if(definition is JSONValueLeaf)
				return DubFileManager.Instance.LoadProject(GetDubFilePath(superProject, (definition as JSONValueLeaf).Value), superProject.ParentSolution, null, DubFileManager.LoadFlags.None, superProject);
			if (definition is JSONObject)
				return Load (superProject, superProject.ParentSolution, definition as JSONObject, superProject.FileName);
			
			throw new InvalidDataException ("definition");
		}

		void DeserializeDubPrjDependencies(JSONObject j, DubBuildSettings settings)
		{
			foreach (var kv in j.Properties) {
				var depName = kv.Key;
				var depVersion = string.Empty;
				var depPath = string.Empty;

				if (kv.Value is JSONValueLeaf)
					depVersion = (kv.Value as JSONValueLeaf).Value;
				else if (kv.Value is JSONObject) {
					foreach (var kvv in (kv.Value as JSONObject).Properties) {
						switch (kvv.Key) {
							case "version":
								depVersion = ExpectJsonStringValue (kvv.Value, "version");
								break;
							case "path":
								depPath = ExpectJsonStringValue (kvv.Value, "path");
								break;
						}
					}
				} else
					throw new InvalidDataException ("Error while deserializing dub project dependency");

				settings.dependencies[depName] =  new DubProjectDependency { Name = depName, Version = depVersion, Path = depPath };
			}
		}

		DubProjectConfiguration DeserializeFromPackageJson(JSONObject j)
		{
			var c = new DubProjectConfiguration { Name = "<Undefined>" };

			if (j.Properties.ContainsKey ("name"))
				c.Name = ExpectJsonStringValue(j.Properties ["name"], "name");

			foreach (var kv in j.Properties) {
				TryDeserializeBuildSetting (c.BuildSettings, kv.Key, kv.Value);
			}

			return c;
		}

		bool TryDeserializeBuildSetting(DubBuildSettings cfg, string propName, JSONThing j)
		{
			var settingIdentifier = propName.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
			if (settingIdentifier.Length < 1)
				return false;

			propName = settingIdentifier[0] = settingIdentifier[0].ToLowerInvariant();
			DubBuildSetting sett;

			switch (propName)
			{
				case "dependencies":
					DeserializeDubPrjDependencies(ExpectJsonObject(j, propName), cfg);
					return true;
				case "targettype":
				case "targetname":
				case "targetpath":
				case "workingdirectory":
				case "mainsourcefile":
					sett = new DubBuildSetting { Name = propName, Values = new[] { ExpectJsonStringValue(j, propName) } };
					break;
				case "subconfigurations":
					foreach (var kv in ExpectJsonObject(j, propName).Properties)
						cfg.subConfigurations[kv.Key] = ExpectJsonStringValue(kv.Value, kv.Key);
					return true;
				case "sourcefiles":
				case "sourcepaths":
				case "excludedsourcefiles":
				case "versions":
				case "debugversions":
				case "importpaths":
				case "stringimportpaths":
					var values = new List<string> ();
					foreach (var i in ExpectJsonArray(j, propName).Items)
						values.Add (ExpectJsonStringValue(i, propName));

					sett = new DubBuildSetting { Name = propName, Values = values.ToArray() };

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
					break;
				default:
					return false;
			}

			List<DubBuildSetting> setts;
			if (!cfg.TryGetValue(settingIdentifier[0], out setts))
				cfg.Add(settingIdentifier[0], setts = new List<DubBuildSetting>());

			setts.Add(sett);

			return true;
		}
	}
}
