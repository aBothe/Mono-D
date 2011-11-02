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
		public DCompilerConfiguration() { }

		/// <summary>
		/// Initializes all commands and arguments (also debug&amp;release args!) with default values depending on given target compiler type
		/// </summary>
		public DCompilerConfiguration(DCompilerVendor type)
		{
			ICompilerDefaultArgumentProvider cmp = null;
			switch (type)
			{
				case DCompilerVendor.DMD:
					cmp = new Dmd();
					break;
				case DCompilerVendor.GDC:
					cmp = new Gdc();
					break;
				case DCompilerVendor.LDC:
					cmp = new Ldc();
					break;
			}

			cmp.ResetCompilerConfiguration(this);

			cmp.ResetBuildArguments(DebugBuildArguments, true);
			cmp.ResetBuildArguments(ReleaseBuildArguments, false);
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

		[ItemProperty("Compiler")]
		public string CompilerExecutable;
		
		[ItemProperty("Linker.Executable")]
		public string Linker_Executable;
		[ItemProperty("Linker.Consoleless")]
		public string Linker_Consoleless;
		[ItemProperty("Linker.SharedLibrary")]
		public string Linker_SharedLib;
		[ItemProperty("Linker.StaticLibrary")]
		public string Linker_StaticLib;
		
		/// <summary>
		/// Gets the linker executable path for a specific link target type
		/// </summary>
		public string LinkerFor(DCompileTarget Target)
		{
			switch (Target)
			{
				case DCompileTarget.SharedLibrary:
					return Linker_SharedLib;
				case DCompileTarget.StaticLibrary:
					return Linker_StaticLib;
				case DCompileTarget.ConsolelessExecutable:
					return Linker_Consoleless;
				default:
					return Linker_Executable;
			}
		}
		
		/// <summary>
		/// Sets the linker executable path for a specific link target type
		/// </summary>
		public void LinkerFor(DCompileTarget Target, string NewLinkerPath)
		{
			switch (Target)
			{
				case DCompileTarget.SharedLibrary:
					Linker_SharedLib=NewLinkerPath;break;
				case DCompileTarget.StaticLibrary:
					Linker_StaticLib=NewLinkerPath;break;
				case DCompileTarget.ConsolelessExecutable:
					Linker_Consoleless=NewLinkerPath;break;
				default:
					Linker_Executable=NewLinkerPath;break;
			}
		}
		
		public void SetAllLinkerPathsTo(string NewLinkerPath)
		{
			Linker_Executable=
				Linker_Consoleless=
				Linker_SharedLib=
				Linker_StaticLib=NewLinkerPath;
		}
		/*
		[ItemProperty("DefaultLibraryPaths")]
		public List<string> DefaultLibPaths=new List<string>();
		*/
		[ItemProperty("DefaultLibs")]
		public List<string> DefaultLibraries=new List<string>();

		[ItemProperty("DebugArguments")]
		public DArgumentConfiguration DebugBuildArguments = new DArgumentConfiguration { IsDebug=true};
		[ItemProperty("ReleaseArguments")]
		public DArgumentConfiguration ReleaseBuildArguments=new DArgumentConfiguration();

		public DArgumentConfiguration GetArgumentCollection(bool DebugArguments = false)
		{
			return DebugArguments ? DebugBuildArguments : ReleaseBuildArguments;
		}
	}

	/// <summary>
	/// Provides raw argument strings that will be used when building projects.
	/// Each DCompilerConfiguration holds both Debug and Release argument sub-configurations.
	/// </summary>
	[DataItem("ArgumentConfiguration")]
	public class DArgumentConfiguration
	{
		[ItemProperty]
		public bool IsDebug;

		[ItemProperty]
		public string SourceCompilerArguments;

		[ItemProperty]
		public string ExecutableLinkerArguments;
		[ItemProperty]
		public string ConsolelessLinkerArguments;

		[ItemProperty]
		public string SharedLibraryLinkerArguments;
		[ItemProperty]
		public string StaticLibraryLinkerArguments;
		
		/// <summary>
		/// Gets/Sets the argument string for a specific link target type
		/// </summary>
		public string this[DCompileTarget target]
		{
			get{
				switch (target)
				{
					case DCompileTarget.SharedLibrary:
						return SharedLibraryLinkerArguments;
					case DCompileTarget.StaticLibrary:
						return StaticLibraryLinkerArguments;
					case DCompileTarget.ConsolelessExecutable:
						return ConsolelessLinkerArguments;
					default:
						return ExecutableLinkerArguments;
				}
			}
			set{
				switch (target)
				{
					case DCompileTarget.SharedLibrary:
						SharedLibraryLinkerArguments=value;break;
					case DCompileTarget.StaticLibrary:
						StaticLibraryLinkerArguments=value;break;
					case DCompileTarget.ConsolelessExecutable:
						ConsolelessLinkerArguments=value;break;
					default:
						ExecutableLinkerArguments=value;break;
				}
			}
		}
	}
}

