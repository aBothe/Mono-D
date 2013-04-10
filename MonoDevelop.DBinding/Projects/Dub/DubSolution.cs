using MonoDevelop.Core;
using MonoDevelop.Projects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubSolution : Solution
	{
		#region Properties
		List<string> authors = new List<string>();
		Dictionary<string, DubProjectDependency> dependencies = new Dictionary<string, DubProjectDependency>();
		public readonly DubBuildSettings GlobalBuildSettings = new DubBuildSettings();

		public override string Name { get; set; } // override because the name is normally derived from the file name -- package.json is not the project's file name!
		public string Homepage;
		public string Copyright;
		public List<string> Authors { get { return authors; } }
		public Dictionary<string, DubProjectDependency> Dependencies
		{
			get { return dependencies; }
		}

		public readonly DubProject MainProject;
		#endregion

		#region Constructor & Init
		public DubSolution(FilePath packageJson)
		{
			FileName = packageJson;
			BaseDirectory = packageJson.ParentDirectory;
			MainProject = new DubProject(this);

			MainProject.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = "Standard", Id = "Std" });
		}

		public void FinalizeDeserialization()
		{
			// Add configurations for each parsed configuration etc.

			MainProject.UpdateFilelist();
			LoadUserProperties();
		}
		#endregion

		#region Serialize & Deserialize
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
				case "configurations":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Configurations");
					Configurations.Clear();
					MainProject.Configurations.Clear();
					
					while (j.Read() && j.TokenType != JsonToken.EndArray)
						MainProject.AddProjectAndSolutionConfiguration(DubProjectConfiguration.DeserializeFromPackageJson(j));
					break;

				default:
					return TryDeserializeBuildSetting(j,GlobalBuildSettings);
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
			"sourceFiles","sourcePaths","excludedSourceFiles","versions","importPaths","stringImportPaths"
		};

		public static bool TryDeserializeBuildSetting(JsonReader j,DubBuildSettings settings)
		{
			if (!(j.Value is string))
				return false;
			var propName = (j.Value as string).Split(new[]{'-'}, StringSplitOptions.RemoveEmptyEntries);
			if (propName.Length < 1)
				return false;

			// For now, only extract information that affect code completion
			if (!WantedProps.Contains(propName[0]))
			{
				j.Skip();
				return false;
			}

			j.Read();
			var flags = (new JsonSerializer()).Deserialize<string[]>(j);
			DubBuildSetting sett;

			if (propName.Length == 4)
			{
				sett = new DubBuildSetting
				{
					Name = propName[0],
					OperatingSystem = propName[1],
					Architecture = propName[2],
					Compiler = propName[3],
					Flags = flags
				};
			}
			else if (propName.Length == 1)
				sett = new DubBuildSetting { Name = propName[0], Flags = flags };
			else
			{
				string Os=null;
				string Arch=null;
				string Compiler=null;
				
				for (int i = 1; i < propName.Length; i++)
				{
					var pn = propName[i].ToLowerInvariant();
					if (Os == null && OsVersions.Contains(pn))
						Os = pn;
					else if (Arch == null && Architectures.Contains(pn))
						Arch = pn;
					else
						Compiler = pn;
				}

				sett = new DubBuildSetting { Name = propName[0], OperatingSystem = Os, Architecture = Arch, Compiler = Compiler, Flags = flags };
			}

			List<DubBuildSetting> setts;
			if (!settings.TryGetValue(propName[0], out setts))
				settings[propName[0]] = setts = new List<DubBuildSetting>();

			setts.Add(sett);

			//{
			//	case "sourceFiles":
					
			//		break;
			//	case "sourcePaths":
			//		break;
			//	case "excludedSourceFiles":
			//		break;
			//	case "versions":
			//		break;
			//	case "importPaths":
			//		break;
			//	case "stringImportPaths":
			//		break;
			//	default:
			//		return false;
			//}

			return true;
		}

		void HandleProjectConfigurationCreation()
		{
			


		}
		#endregion

		#region Solution items
		public override Project GetProjectContainingFile(FilePath fileName)
		{
			return MainProject.GetProjectFile(fileName) != null ? MainProject : null;
		}

		public override SolutionEntityItem FindSolutionItem(string fileName)
		{
			return base.FindSolutionItem(fileName);
		}

		public override ReadOnlyCollection<T> GetAllItems<T>()
		{
			return new ReadOnlyCollection<T>(new[]{MainProject as T});
		}

		public override ReadOnlyCollection<T> GetAllSolutionItems<T>()
		{
			return new ReadOnlyCollection<T>(new[] { MainProject as T });
		}

		public override ReadOnlyCollection<Solution> GetAllSolutions()
		{
			return new ReadOnlyCollection<Solution>(new[]{this});
		}

		public override bool SupportsFormat(FileFormat format)
		{
			return true;// format.Name.Contains("MSBuild");
		}
		#endregion

		#region Build Configurations
		public override ReadOnlyCollection<string> GetConfigurations()
		{
			return base.GetConfigurations();
		}

		public override SolutionConfiguration GetConfiguration(ConfigurationSelector configuration)
		{
			return base.GetConfiguration(configuration);
		}
		#endregion

		#region Building & Executing
		protected override BuildResult OnBuild(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			return MainProject.Build(monitor, configuration);
		}

		protected override void OnClean(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			MainProject.Clean(monitor, configuration);
		}

		protected override void OnExecute(IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			MainProject.Execute(monitor, context, configuration);
		}

		protected override bool OnGetNeedsBuilding(ConfigurationSelector configuration)
		{
			return true;
		}

		protected override void OnSetNeedsBuilding(bool val, ConfigurationSelector configuration)
		{

		}
		#endregion
	}

	public class DubBuildSettings : Dictionary<string, List<DubBuildSetting>>
	{
		//public Dictionary<string, string> subConfigurations;
	}

	public class DubBuildSetting
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
