
namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Stores compiler commands and arguments for compiling and linking D source files.
	/// </summary>
	public class DCompilerConfiguration
	{
		public DCompilerConfiguration() { }

		/// <summary>
		/// Initializes all commands and arguments (also debug&release args!) with default values depending on given target compiler type
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

		public DCompilerVendor CompilerType;

		public string CompilerExecutable;
		public string LinkerExecutable;

		public DArgumentConfiguration DebugBuildArguments = new DArgumentConfiguration { IsDebug=true};
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
	public class DArgumentConfiguration
	{
		public bool IsDebug;

		public string SourceCompilerArguments;

		public string ExecutableLinkerArguments;
		public string ConsolelessLinkerArguments;

		public string SharedLibraryLinkerArguments;
		public string StaticLibraryLinkerArguments;

		public string GetLinkerArgumentString(DCompileTarget targetType)
		{
			switch (targetType)
			{
				case DCompileTarget.SharedLibrary:
					return SharedLibraryLinkerArguments;
				case DCompileTarget.StaticLibrary:
					return StaticLibraryLinkerArguments;
				case DCompileTarget.ConsolelessExecutable:
					return ConsolelessLinkerArguments;
			}

			return ExecutableLinkerArguments;
		}
	}
}

