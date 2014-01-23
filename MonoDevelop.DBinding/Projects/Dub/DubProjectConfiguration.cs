using MonoDevelop.Projects;
using Newtonsoft.Json;
namespace MonoDevelop.D.Projects.Dub
{
	public class DubProjectConfiguration : ProjectConfiguration
	{
		public const string DefaultConfigId = "Default";
		public DubBuildSettings BuildSettings = new DubBuildSettings();

		public MonoDevelop.D.Building.DCompileTarget TargetType
		{
			get{ 
				string targetType = null;
				var prj = base.ParentItem as DubProject;
				prj.CommonBuildSettings.TryGetTargetTypeProperty (prj, Selector, ref targetType);
				BuildSettings.TryGetTargetTypeProperty (prj, Selector, ref targetType);

				if (targetType == null)
					return MonoDevelop.D.Building.DCompileTarget.Executable;

				switch (targetType.ToLowerInvariant ()) {
					case "shared":
						return MonoDevelop.D.Building.DCompileTarget.SharedLibrary;
					case "static":
						return MonoDevelop.D.Building.DCompileTarget.StaticLibrary;
					default:
						return MonoDevelop.D.Building.DCompileTarget.Executable;
				}
			}
		}

		public override void CopyFrom (ItemConfiguration conf)
		{
			var cfg = conf as DubProjectConfiguration;
			if (cfg != null) {
				BuildSettings = cfg.BuildSettings;
			}
			base.CopyFrom (conf);
		}

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
