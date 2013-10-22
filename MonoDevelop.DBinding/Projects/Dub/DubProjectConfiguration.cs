using MonoDevelop.Projects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubProjectConfiguration : ProjectConfiguration
	{
		public const string DefaultConfigId = "Default";
		public readonly DubBuildSettings BuildSettings = new DubBuildSettings();

		public DubProjectConfiguration()
		{
			Platform = "AnyCPU";
			ExternalConsole = true;
			PauseConsoleOutput = true;
			DebugMode = true;
		}

		public static DubProjectConfiguration DeserializeFromPackageJson(JsonReader j)
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
							c.Platform = string.Join("|",srz.Deserialize<string[]>(j));
							break;
						default:
							if (!c.BuildSettings.TryDeserializeBuildSetting(j))
								j.Skip();
							break;
					}
				}
			}

			return c;
		}
	}
}
