using System;

namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Interface which enables resetting D configuration objects to default.
	/// </summary>
	public interface ICompilerDefaultArgumentProvider
	{
		void ResetCompilerConfiguration(DCompilerConfiguration Config);
		void ResetBuildArguments(DArgumentConfiguration Arguments, bool IsDebug);
	}

	/// <summary>
	/// Provides default build commands and arguments for the dmd compiler.
	/// </summary>
	public class Dmd : ICompilerDefaultArgumentProvider
	{
		public void ResetCompilerConfiguration(DCompilerConfiguration Config)
		{
			Config.CompilerExecutable = "dmd";
			Config.SetAllLinkerPathsTo("dmd");
			Config.CompilerType = DCompilerVendor.DMD;
		}

		public void ResetBuildArguments(DArgumentConfiguration args, bool IsDebug)
		{
			string commonLinkerArgs = "";
			if (IsDebug)
			{
				commonLinkerArgs = "$objs -gc -debug -L/NOLOGO ";

				args.IsDebug = true;
				args.SourceCompilerArguments = "-c \"$src\" -of\"$obj\" $importPaths -gc -debug";
			}
			else
			{
				commonLinkerArgs = "$objs -release -O -L/NOLOGO ";

				args.SourceCompilerArguments = "-c \"$src\" -of\"$obj\" $importPaths -release -O";
			}

			args.ConsolelessLinkerArguments = commonLinkerArgs + "-of\"$target\"";

			//TODO: Complete arg strings which let link to a consoleless executable 
			if (Environment.OSVersion.Platform == PlatformID.MacOSX)
			{ }
			else if (Environment.OSVersion.Platform == PlatformID.Unix)
			{ }
			else
				args.ConsolelessLinkerArguments += " -L/su:windows -L/exet:nt";

			args.ExecutableLinkerArguments = commonLinkerArgs + "-of\"$target\"";
			args.SharedLibraryLinkerArguments = commonLinkerArgs + "-L/IMPLIB:$relativeTargetDir -of\"$target\"";

			// When creating a static library, debug & release builds share equal build parameters
			args.StaticLibraryLinkerArguments = "-lib -of\"$target\" $objs";
		}
	}

	/// <summary>
	/// Provides default build commands and arguments for the gdc compiler.
	/// </summary>
	public class Gdc : Dmd
	{
		public new void ResetCompilerConfiguration(DCompilerConfiguration Config)
		{
			Config.CompilerExecutable = "gdc";
			Config.SetAllLinkerPathsTo("gdc");
			Config.LinkerFor(DCompileTarget.StaticLibrary,"ar");
			Config.CompilerType = DCompilerVendor.GDC;
		}
	}

	/// <summary>
	/// Provides default build commands and arguments for the ldc compiler.
	/// </summary>
	public class Ldc : Dmd
	{
		public new void ResetCompilerConfiguration(DCompilerConfiguration Config)
		{
			Config.CompilerExecutable = "ldc";
			Config.SetAllLinkerPathsTo("ldc");
			Config.LinkerFor(DCompileTarget.StaticLibrary,"ar");
			Config.CompilerType = DCompilerVendor.LDC;
		}
	}

	/*public class DMDCompilerCommandBuilder : DCompilerSupport
	{
		public DMDCompilerCommandBuilder(DProject prj, DProjectConfiguration config)
			: base(prj, config)
		{
			this.compilerCommand = "dmd";
			this.linkerCommand = "dmd";
		}

		public override string BuildCompilerArguments(string srcfile, string outputFile)
		{


			var dmdincludes = new StringBuilder();
			if (config.Includes != null)
				foreach (string inc in config.Includes)
					dmdincludes.AppendFormat(" -I\"{0}\"", inc);

			// b.Build argument string
			//var dmdArgs = "-c \"" + f.FilePath + "\" -op\"" + objDir + "\" " + 
			//(cfg.DebugMode?compilerDebugArgs:compilerReleaseArgs) + " " + 
			//cfg.ExtraCompilerArguments + dmdincludes.ToString();		

			return string.Format("-c \"{0}\" {5}\"{1}\" {2} {3} {4}",
												srcfile,
												outputFile,
												(config.DebugMode ? compilerDebugArgs : compilerReleaseArgs),
												config.ExtraCompilerArguments,
												dmdincludes.ToString(),
												compilerOutputFileSwitch);
		}

		public override string BuildLinkerArguments(List<string> objFiles)
		{
			var objsArg = "";
			foreach (var o in objFiles)
			{
				var o_ = o;

				if (o_.StartsWith(prj.BaseDirectory))
					o_ = o_.Substring(prj.BaseDirectory.ToString().Length).TrimStart('/', '\\');

				objsArg += "\"" + o_ + "\" ";
			}

			var libs = "";
			foreach (var lib in config.Libs)
			{
				libs += lib + " ";
			}

			var nologo = "";
			if (!((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX)))
				nologo = " -L/NOLOGO";
			var linkArgs =
					objsArg.TrimEnd() + nologo + " " +
					libs + " " +
					config.ExtraLinkerArguments + " " +
					(config.DebugMode ? linkerDebugArgs : linkerReleaseArgs) +
					" " + linkerOutputFileSwitch + "\"" + Path.Combine(config.OutputDirectory, config.CompiledOutputName) + "\"";


			switch (config.CompileTarget)
			{
				case DCompileTargetType.SharedLibrary:
					if (config.CompiledOutputName.EndsWith(".dll"))
						linkArgs += " -L/IMPLIB:\"" + Path.GetFileNameWithoutExtension(config.Output) + ".lib\"";
					//TODO: Are there import libs on other platforms?
					break;
				case DCompileTargetType.StaticLibrary:
					linkArgs += "-lib";
					break;
			}
			return linkArgs;
		}

	}
	 
		public class GDCCompilerCommandBuilder : DMDCompilerCommandBuilder
	{
		public GDCCompilerCommandBuilder(DProject prj, DProjectConfiguration config)
			: base(prj, config)
		{
			this.compilerCommand = "gdc";
			this.linkerCommand = "gdc";
			this.compilerDebugArgs = "-g";
			this.compilerOutputFileSwitch = "-o ";
			this.linkerOutputFileSwitch = "-o ";
		}
	}
	 
	 	public class LDCCompilerCommandBuilder : DMDCompilerCommandBuilder
	{
		public LDCCompilerCommandBuilder(DProject prj, DProjectConfiguration config)
			: base(prj, config)
		{
			this.compilerCommand = "ldc";
			this.linkerCommand = "ldc";
			this.compilerDebugArgs = "-gc";
			this.linkerDebugArgs = "-gc";
		}
	}
	 
	 */
}
