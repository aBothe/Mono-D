using MonoDevelop.Core.Serialization;
using System.Collections.Generic;
using D_Parser.Completion;
using System.Collections.ObjectModel;

namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Stores compiler commands and arguments for compiling and linking D source files.
	/// </summary>
	[DataItem("CompilerConfiguration")]
	public class DCompilerConfiguration
	{
		/// <summary>
		/// Initializes all commands and arguments (also debug&amp;release args!) with default values depending on given target compiler type
		/// </summary>
		public static DCompilerConfiguration CreateWithDefaults(DCompilerVendor Compiler)
		{
			var cfg = new DCompilerConfiguration {  CompilerType=Compiler };

			CompilerDefaultArgumentProvider cmp = null;
			switch (Compiler)
			{
				case DCompilerVendor.DMD:
					cmp = new Dmd(cfg);
					break;
				case DCompilerVendor.GDC:
					cmp = new Gdc(cfg);
					break;
				case DCompilerVendor.LDC:
					cmp = new Ldc(cfg);
					break;
			}

			cmp.ResetCompilerConfiguration();
			cmp.ResetBuildArguments();

			return cfg;
		}

		#region Parse Cache
		/// <summary>
		/// See DProject.includePaths for details!
		/// </summary>
		[ItemProperty("IncludePaths")]
		[ItemProperty("Path", Scope = "*")]
		string[] includePaths;

		public ParsePerformanceData[] SetupGlobalParseCache(bool ParseFunctionBodies=true)
		{
			var gc = GetGlobalParseCache();

			if(includePaths!=null)
				foreach (var inc in includePaths)
					gc.Add(inc,ParseFunctionBodies);

			return gc.UpdateCache();
		}

		public void SaveGlobalParseCacheInformation()
		{
			includePaths = GetGlobalParseCache().DirectoryPaths;
		}
		
		public ASTStorage GetGlobalParseCache()
		{
			return DLanguageBinding.GetGlobalParseCache(CompilerType);
		}
		#endregion

		[ItemProperty("Name")]
		public DCompilerVendor CompilerType;

		[ItemProperty]
		LinkTargetConfiguration Cfg_Executable = new LinkTargetConfiguration();
		[ItemProperty]
		LinkTargetConfiguration Cfg_ConsolelessExecutable = new LinkTargetConfiguration();
		[ItemProperty]
		LinkTargetConfiguration Cfg_SharedLib = new LinkTargetConfiguration();
		[ItemProperty]
		LinkTargetConfiguration Cfg_StaticLib = new LinkTargetConfiguration();

		public IEnumerable<LinkTargetConfiguration> TargetConfigurations
		{
			get { return new[] { Cfg_Executable, Cfg_ConsolelessExecutable, Cfg_SharedLib, Cfg_StaticLib }; }
		}

		public LinkTargetConfiguration GetTargetConfiguration(DCompileTarget Target)
		{
			switch (Target)
			{
				case DCompileTarget.ConsolelessExecutable:
					return Cfg_ConsolelessExecutable;
				case DCompileTarget.SharedLibrary:
					return Cfg_SharedLib;
				case DCompileTarget.StaticLibrary:
					return Cfg_StaticLib;
			}

			return Cfg_Executable;
		}

		public void SetAllCompilerBuildArgs(string NewCompilerArguments, bool AffectDebugArguments)
		{
			foreach (var t in TargetConfigurations)
				t.GetArguments(AffectDebugArguments).CompilerArguments=NewCompilerArguments;
		}

		/// <summary>
		/// Overrides all compiler command strings of all LinkTargetConfigurations
		/// </summary>
		public void SetAllCompilerCommands(string NewCompilerPath)
		{
			foreach (var t in TargetConfigurations)
				t.Compiler = NewCompilerPath;
		}

		public void SetAllLinkerCommands(string NewLinkerPath)
		{
			foreach (var t in TargetConfigurations)
				t.Linker= NewLinkerPath;
		}
		
		/*
		 * Do not add default library paths, 
		 * because it would make building the argument strings more complicated 
		 * - there had to be compiler-specific composers 
		 * which created the lib path chain then 
		 * - for each single compiler/linker!
		 */
		/*
		[ItemProperty("DefaultLibraryPaths")]
		public List<string> DefaultLibPaths=new List<string>();
		*/
		[ItemProperty("DefaultLibs")]
		public List<string> DefaultLibraries = new List<string>();
	}

	[DataItem]
	public class LinkTargetConfiguration
	{
		[ItemProperty]
		public string Compiler;
		[ItemProperty]
		public string Linker;

		#region Patterns
		/// <summary>
		/// Describes how each .obj/.o file shall be enumerated in the $objs linking macro
		/// </summary>
		[ItemProperty]
		public string ObjectFileLinkPattern = "\"{0}\"";
		/// <summary>
		/// Describes how each include path shall be enumerated in the $includes compiling macro
		/// </summary>
		[ItemProperty]
		public string IncludePathPattern = "-I\"{0}\"";
		#endregion

		[ItemProperty]
		public BuildConfiguration DebugArguments = new BuildConfiguration();
		[ItemProperty]
		public BuildConfiguration ReleaseArguments = new BuildConfiguration();

		public BuildConfiguration GetArguments(bool IsDebug)
		{
			return IsDebug ? DebugArguments : ReleaseArguments;
		}
	}

	[DataItem]
	public class BuildConfiguration
	{
		[ItemProperty]
		public string CompilerArguments;
		[ItemProperty]
		public string LinkerArguments;
	}
}

