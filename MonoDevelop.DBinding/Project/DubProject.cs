using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MonoDevelop.D.Dub
{
	public class DubProject : DProject
	{
		List<string> authors = new List<string>();
		Dictionary<string, DubProjectDependency> dependencies = new Dictionary<string, DubProjectDependency>();
		public readonly DubBuildSettings GlobalBuildSettings = new DubBuildSettings();

		public string Description;
		public string Homepage;
		public string Copyright;
		public List<string> Authors { get { return authors; } }
		public Dictionary<string, DubProjectDependency> Dependencies
		{
			get { return dependencies; }
		}

		public bool TryPopulateProperty(string propName,JsonReader j)
		{
			switch (propName)
			{
				case "name":
					Name = j.ReadAsString();
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
					dependencies.Clear();
					while (j.Read() && j.TokenType != JsonToken.EndObject)
					{
						if(j.TokenType == JsonToken.PropertyName)
							DeserializeDubPrjDependency(j);
					}
					break;


				default:
					return false;
			}

			return true;
		}

		void DeserializeDubPrjDependency(JsonReader j)
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
			{
				depVersion = j.Value as string;
			}

			dependencies[depName] = new DubProjectDependency { Name = depName, Version=depVersion, Path = depPath };
		}
	}

	public class DubBuildSettings : Dictionary<string, DubBuildSetting>
	{
		
	}

	public struct DubBuildSetting
	{
		public string Name;
		public string OperatingSystem;
		public string Architecture;
		public string Compiler;
		public string[] Flags;
	}

	

	public struct DubProjectDependency
	{
		public string Name;
		public string Version;
		public string Path;
	}
}
