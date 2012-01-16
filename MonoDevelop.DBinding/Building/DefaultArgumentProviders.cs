using System;

namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Interface which enables resetting D configuration objects to default.
	/// </summary>
	public abstract class CompilerDefaultArgumentProvider
	{
		protected DCompilerConfiguration Configuration;

		public CompilerDefaultArgumentProvider(DCompilerConfiguration Configuration)
		{
			this.Configuration = Configuration;
		}

		/// <summary>
		/// Resets generic compiler properties except build arguments.
		/// </summary>
		public abstract void ResetCompilerConfiguration();

		public void ResetBuildArguments()
		{
			foreach (var t in new[]{ 
				DCompileTarget.Executable, 
				DCompileTarget.ConsolelessExecutable, 
				DCompileTarget.SharedLibrary, 
				DCompileTarget.StaticLibrary })
			{
				ResetBuildArguments(t, true);
				ResetBuildArguments(t, false);
			}
		}

		public abstract void ResetBuildArguments(DCompileTarget LinkTarget, bool IsDebug);
	}

	/// <summary>
	/// Provides default build commands and arguments for the dmd compiler.
	/// </summary>
	public class Dmd:CompilerDefaultArgumentProvider
	{
		public Dmd(DCompilerConfiguration cfg):base(cfg){}

		public override void ResetCompilerConfiguration()
		{
			Configuration.Vendor = DCompilerVendor.DMD;
			Configuration.DefaultLibraries.Clear();

			var dmd = "dmd";

			if (OS.IsWindows)
				dmd += ".exe";

			Configuration.SetAllCompilerCommands(dmd);
			Configuration.SetAllLinkerCommands(dmd);
			
			// Add phobos.lib/.a by default
			Configuration.DefaultLibraries.Clear();
			if(OS.IsWindows)
				Configuration.DefaultLibraries.Add("phobos"+DCompiler.StaticLibraryExtension);
		}

		public override void ResetBuildArguments(DCompileTarget LinkTarget, bool IsDebug)
		{
			// Only arguments become reset, not the commands
			var args = Configuration.GetTargetConfiguration(LinkTarget).GetArguments(IsDebug);
			var debugAppendix=IsDebug?"-gc -debug":"-O -release";
			var noLogoArg = (OS.IsWindows)?"-L/NOLOGO ":"";
			
			args.CompilerArguments = "-c \"$src\" -of\"$obj\" $includes " + debugAppendix;
			args.LinkerArguments = noLogoArg + debugAppendix + " -of\"$target\" $objs $libs";

			switch (LinkTarget)
			{
				case DCompileTarget.ConsolelessExecutable:
					//TODO: Complete arg strings which let link to a consoleless executable 
					if (Environment.OSVersion.Platform == PlatformID.MacOSX)
					{ }
					else if (Environment.OSVersion.Platform == PlatformID.Unix)
					{ }
					else
						args.LinkerArguments += " -L/su:windows -L/exet:nt";
					break;

				case DCompileTarget.SharedLibrary:
					args.LinkerArguments += " -L/IMPLIB:$relativeTargetDir ";
					break;

				case DCompileTarget.StaticLibrary:
					args.LinkerArguments = "-lib -of\"$target\" $objs";
					break;
			}
		}
	}

	/// <summary>
	/// Provides default build commands and arguments for the gdc compiler.
	/// </summary>
	public class Gdc : CompilerDefaultArgumentProvider
	{
		public Gdc(DCompilerConfiguration cfg) : base(cfg) { }

		public override void ResetCompilerConfiguration()
		{
			Configuration.Vendor = DCompilerVendor.GDC;
			Configuration.DefaultLibraries.Clear();

			var gdc = "gdc";

			if (OS.IsWindows)
				gdc += ".exe";

			Configuration.SetAllCompilerCommands(gdc);
			Configuration.SetAllLinkerCommands(gdc);

			Configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary).Linker = "ar" + (OS.IsWindows?".exe":"");
			
			Configuration.DefaultLibraries.Clear();
			if (OS.IsWindows)
				Configuration.DefaultLibraries.Add("phobos"+DCompiler.StaticLibraryExtension);
		}

		public override void ResetBuildArguments(DCompileTarget LinkTarget, bool IsDebug)
		{
			var args = Configuration.GetTargetConfiguration(LinkTarget).GetArguments(IsDebug);
			var debugAppendix = IsDebug ? "-g" : "-O3";

			args.CompilerArguments = "-c \"$src\" -o \"$obj\" $includes " + debugAppendix;
			args.LinkerArguments = "-o \"$target\" " + debugAppendix + " $objs $libs";

			switch (LinkTarget)
			{
				case DCompileTarget.ConsolelessExecutable:
					if (Environment.OSVersion.Platform == PlatformID.MacOSX)
					{ }
					else if (Environment.OSVersion.Platform == PlatformID.Unix)
					{ }
					else
						args.LinkerArguments += " -L/su:windows -L/exet:nt";
					break;

				case DCompileTarget.SharedLibrary:
					args.CompilerArguments = "-fPIC " + args.CompilerArguments;
					args.LinkerArguments += " -shared";
					break;

				case DCompileTarget.StaticLibrary:
					args.LinkerArguments = "rcs \"$target\" $objs";
					break;
			}
		}
	}

	/// <summary>
	/// Provides default build commands and arguments for the ldc compiler.
	/// </summary>
	public class Ldc :CompilerDefaultArgumentProvider
	{
		public Ldc(DCompilerConfiguration cfg):base(cfg){}

		public override void ResetCompilerConfiguration()
		{
			Configuration.Vendor = DCompilerVendor.LDC;
			Configuration.DefaultLibraries.Clear();

			var ldc = "ldc";

			if (OS.IsWindows)
				ldc += ".exe";

			Configuration.SetAllCompilerCommands(ldc);
			Configuration.SetAllLinkerCommands(ldc);

			Configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary).Linker = "ar" + (OS.IsWindows ? ".exe" : "");
			
			Configuration.DefaultLibraries.Clear();
			if (OS.IsWindows)
				Configuration.DefaultLibraries.Add("phobos"+DCompiler.StaticLibraryExtension);
		}

		public override void ResetBuildArguments(DCompileTarget LinkTarget, bool IsDebug)
		{
			var args = Configuration.GetTargetConfiguration(LinkTarget).GetArguments(IsDebug);
			var debugAppendix=IsDebug?"-g": "-O3 -release";

			args.CompilerArguments = "-c \"$src\" -of \"$obj\" $includes "+debugAppendix;
			args.LinkerArguments = "-of \"$target\" "+debugAppendix +" $objs $libs";

			switch (LinkTarget)
			{
				case DCompileTarget.ConsolelessExecutable:
					if (Environment.OSVersion.Platform == PlatformID.MacOSX)
					{ }
					else if (Environment.OSVersion.Platform == PlatformID.Unix)
					{ }
					else
						args.LinkerArguments += " -L/su:windows -L/exet:nt";
					break;

				case DCompileTarget.SharedLibrary:
					args.CompilerArguments="-relocation-model=pic " + args.CompilerArguments; 
					args.LinkerArguments += " -L-shared";
					break;

				case DCompileTarget.StaticLibrary:
					args.LinkerArguments =	"rcs \"$target\" $objs";
					break;
			}
		}
	}
}
