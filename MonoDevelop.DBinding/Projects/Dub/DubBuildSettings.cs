using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubBuildSettings : Dictionary<string, List<DubBuildSetting>>, ICloneable
	{
		public const string TargetTypeProperty = "targettype";
		public const string TargetNameProperty = "targetname";
		public const string TargetPathProperty = "targetpath";
		public const string SourcePathsProperty = "sourcepaths";
		public const string ImportPathsProperty = "importpaths";

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

		//public Dictionary<string, string> subConfigurations;

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

		static HashSet<string> WantedProps = new HashSet<string> {
			"targettype","targetname","targetpath",
			"sourcefiles",SourcePathsProperty,"excludedsourcefiles","versions",ImportPathsProperty,"stringimportpaths"
		};

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

			if (TryGetValue (DubBuildSettings.TargetTypeProperty, out l))
				foreach (var sett in l)
					if (prj.BuildSettingMatchesConfiguration (sett, configuration))
						targetType = sett.Values [0];
		}

		public bool TryDeserializeBuildSetting(JsonReader j)
		{
			if (!(j.Value is string))
				return false;
			var settingIdentifier = (j.Value as string).Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
			if (settingIdentifier.Length < 1)
				return false;

			settingIdentifier[0] = settingIdentifier [0].ToLowerInvariant ();
			// For now, only extract information that affect code completion
			if (!WantedProps.Contains(settingIdentifier[0]))
			{
				j.Skip();
				return false;
			}

			j.Read();
			string[] flags;

			if (j.TokenType == JsonToken.String)
				flags = new[]{ j.Value as string };
			else if (j.TokenType == JsonToken.StartArray)
				flags = (new JsonSerializer ()).Deserialize<string[]> (j);
			else {
				j.Skip ();
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
					if (Os == null && OsVersions.Contains(pn))
						Os = pn;
					else if (Arch == null && Architectures.Contains(pn))
						Arch = pn;
					else
						Compiler = pn;
				}

				sett = new DubBuildSetting { Name = settingIdentifier[0], OperatingSystem = Os, Architecture = Arch, Compiler = Compiler, Values = flags };
			}

			List<DubBuildSetting> setts;
			if (!TryGetValue(settingIdentifier[0], out setts))
				Add(settingIdentifier[0], setts = new List<DubBuildSetting>());

			setts.Add(sett);

			return true;
		}
	}

	public class DubBuildSetting : ICloneable
	{
		public string Name;
		public string OperatingSystem;
		public string Architecture;
		public string Compiler;
		public string[] Values;

		public object Clone ()
		{
			return new DubBuildSetting{ Name = Name, OperatingSystem = OperatingSystem, Architecture = Architecture, Compiler = Compiler, Values = Values };
		}
	}
	
	public class DubProjectDependency
	{
		public string Name;
		public string Version;
		public string Path;
	}
}
