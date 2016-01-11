using System;
using System.Collections.Generic;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubBuildSettings : Dictionary<string, List<DubBuildSetting>>, ICloneable
	{
		public const string TargetTypeProperty = "targettype";
		public const string TargetNameProperty = "targetname";
		public const string TargetPathProperty = "targetpath";
		public const string SourcePathsProperty = "sourcepaths";
		public const string SourceFilesProperty = "sourcefiles";
		public const string ImportPathsProperty = "importpaths";
		public const string VersionsProperty = "versions";

		public object Clone ()
		{
			var s = new DubBuildSettings ();

			foreach (var kv in this) {
				var newList = new List<DubBuildSetting> ();
				foreach (var setting in kv.Value)
					newList.Add (setting.Clone () as DubBuildSetting);
				s [kv.Key] = newList;
			}

			return s;
		}

		public readonly Dictionary<string, string> subConfigurations = new Dictionary<string,string>();

		public static HashSet<string> OsVersions = new HashSet<string> { 
			"windows","win32","win64","linux","osx",
			"freebsd","openbsd","netbsd","dragonflybsd","bsd",
			"solaris","posix","aix","haiku","skyos","sysv3","sysv4","hurd",
			"android"
		};

		public static HashSet<string> Architectures = new HashSet<string> { 
			"Alpha_HardFloat","Alpha_SoftFloat","Alpha","SH64","SH","HPPA64","HPPA","S390X","","S390",
			"SPARC64","SPARC_HardFloat","SPARC_SoftFloat","SPARC_V8Plus","SPARC",
			"MIPS_HardFloat","MIPS_SoftFloat","MIPS_EABI","MIPS_N64","MIPS_O64","MIPS_N32","MIPS_O32","MIPS64","MIPS32",
			"ia64",
			"PPC64","PPC_HardFloat","PPC_SoftFloat","PPC","AArch64",
			"ARM_HardFloat","ARM_SoftFP","ARM_SoftFloat","ARM_Thumb","arm",
			"x86_64","x86"
		};

		public static readonly HashSet<string> WantedProps = new HashSet<string> {
			TargetTypeProperty,TargetNameProperty,TargetPathProperty,
			SourceFilesProperty,SourcePathsProperty,"excludedsourcefiles",VersionsProperty,ImportPathsProperty,"stringimportpaths"
		};

		public void TryGetTargetTypeProperty(DubProject prj, ConfigurationSelector cfg, ref string targetType)
		{
			List<DubBuildSetting> l;
			if (TryGetValue (DubBuildSettings.TargetTypeProperty, out l))
				foreach (var sett in l)
					if (prj.BuildSettingMatchesConfiguration (sett, cfg))
						targetType = sett.Values [0];
		}

		public void TryGetTargetFileProperties(DubProject prj, ConfigurationSelector configuration,ref string targetType, ref string targetName, ref string targetPath)
		{
			List<DubBuildSetting> l;
			if (TryGetValue (DubBuildSettings.TargetNameProperty, out l))
				foreach (var sett in l)
					if (prj.BuildSettingMatchesConfiguration (sett, configuration))
						targetName = sett.Values [0];

			if (TryGetValue (DubBuildSettings.TargetPathProperty, out l))
				foreach (var sett in l)
					if (prj.BuildSettingMatchesConfiguration (sett, configuration))
						targetPath = sett.Values [0];

			TryGetTargetTypeProperty (prj, configuration, ref targetType);
		}
	}

	public class DubBuildSetting : ICloneable
	{
		public string Name;
		public string OperatingSystem;
		public string Architecture;
		public string Compiler;
		public string[] Values;

		public object Clone()
		{
			return new DubBuildSetting
			{
				Name = Name,
				OperatingSystem = OperatingSystem,
				Architecture = Architecture,
				Compiler = Compiler,
				Values = Values
			};
		}
	}

	public class DubProjectDependency
	{
		public string Name;
		public string Version;
		/// <summary>
		/// May contain inappropriate directory path separators! (Win, Non-Win OS)
		/// </summary>
		public string Path;
	}
}
