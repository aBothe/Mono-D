using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubProjectConfiguration : ProjectConfiguration
	{
		public const string DefaultConfigId = "Default";
		public DubBuildSettings BuildSettings = new DubBuildSettings();

		public Building.DCompileTarget TargetType
		{
			get{
				string targetType = null;
				var prj = base.ParentItem as DubProject;
				prj.CommonBuildSettings.TryGetTargetTypeProperty (prj, Selector, ref targetType);
				BuildSettings.TryGetTargetTypeProperty (prj, Selector, ref targetType);

				if (targetType == null)
					return Building.DCompileTarget.Executable;

				switch (targetType.ToLowerInvariant ()) {
					case "shared":
						return Building.DCompileTarget.SharedLibrary;
					case "static":
						return Building.DCompileTarget.StaticLibrary;
					default:
						return Building.DCompileTarget.Executable;
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
	}
}
