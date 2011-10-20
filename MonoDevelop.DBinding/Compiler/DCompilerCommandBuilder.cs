using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.CodeDom.Compiler;
using MonoDevelop.Projects;
using MonoDevelop.Core;

namespace MonoDevelop.D
{
	public abstract class DCompilerCommandBuilder
	{	
		protected string compilerCommand = "";
		protected string linkerCommand = "";
		
		protected string compilerArguments = "";
		protected string linkerArguments = "";
		
		
		// Arguments that are inserted additionally (by default!)
		protected string compilerDebugArgs = "-g -debug";
		protected string compilerReleaseArgs = "-O -release -inline";
		
		protected string compilerOutputFileSwitch = "-of";		
		
		protected string linkerDebugArgs = "-g -debug";
		protected string linkerReleaseArgs = "-O -release -inline";		
		
		protected string linkerOutputFileSwitch = "-of";
		
		protected DProject prj; 		
		protected DProjectConfiguration config;
			// List of created object files
		
		protected bool modificationsdone;
		
		public DCompilerCommandBuilder(DProject prj, DProjectConfiguration config)
		{
			this.prj= prj;
			this.config = config;					
		}
		public string CompilerCommand
		{get{return compilerCommand;}}
		public string LinkerCommand
		{get{return linkerCommand;}}
			
		
		public abstract string BuildCompilerArguments(string srcfile, string outputFile);
		public abstract string BuildLinkerArguments(List<string> objFiles);
		
		
		#region Compiler Error Parsing
		private static Regex withColRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):(?<column>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);	
		private static Regex noColRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);		
		private static Regex linkerRegex = new Regex(
			@"^\s*(?<file>[^:]*):(?<line>\d*):\s*(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		
		//additional regex parsers
		private static Regex noColRegex_2 = new Regex (
			@"^\s*((?<file>.*)(\()(?<line>\d*)(\)):\s*(?<message>.*))|(Error:)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);			
		
		private static Regex gcclinkerRegex = new Regex (
		    @"^\s*(?<file>.*):(?<line>\d*):((?<column>\d*):)?\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		
		
		//TODO, let decendents handle this
		public virtual CompilerError FindError(string errorString, TextReader reader)
		{
			var error = new CompilerError();
			string warning = GettextCatalog.GetString("warning");
			string note = GettextCatalog.GetString("note");

			var match = withColRegex.Match(errorString);

			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				error.Column = int.Parse(match.Groups["column"].Value);
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				return error;
			}

			match = noColRegex.Match(errorString);

			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				// Skip messages that begin with ( and end with ), since they're generic.
				//Attempt to capture multi-line versions too.
				if (error.ErrorText.StartsWith("("))
				{
					string error_continued = error.ErrorText;
					do
					{
						if (error_continued.EndsWith(")"))
							return null;
					} while ((error_continued = reader.ReadLine()) != null);
				}

				return error;
			}
			
			match = noColRegex_2.Match(errorString);
			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				return error;
			}
			
			match = gcclinkerRegex.Match(errorString);
			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;
					
				
				return error;
			}
			
			
			return null;				
		}
		#endregion		
		
	}	
	
	
	public class DMDCompilerCommandBuilder:DCompilerCommandBuilder
	{
		public DMDCompilerCommandBuilder(DProject prj, DProjectConfiguration config):base(prj, config)
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
						
			return  string.Format("-c \"{0}\" {5}\"{1}\" {2} {3} {4}",
												srcfile,
												outputFile,
												(config.DebugMode?compilerDebugArgs:compilerReleaseArgs),
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
					o_ = o_.Substring(prj.BaseDirectory.ToString().Length).TrimStart('/','\\');

				objsArg += "\"" + o_ + "\" ";
			}			
			
			var libs = "";
			foreach(var lib in config.Libs)
			{
				libs += lib + " "; 
			}
				
			var nologo = "";
			if (!((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX)))				
				nologo = " -L/NOLOGO";
			var linkArgs =
					objsArg.TrimEnd()+ nologo + " " +
					libs + " " +
					config.ExtraLinkerArguments + " " + 
					(config.DebugMode?linkerDebugArgs:linkerReleaseArgs) +
					" " + linkerOutputFileSwitch + "\""+Path.Combine(config.OutputDirectory,config.CompiledOutputName)+"\"";					
			
			
			switch (config.CompileTarget)
			{
				case DCompileTargetType.SharedLibrary:
					if (config.CompiledOutputName.EndsWith(".dll"))
						linkArgs += " -L/IMPLIB:\""+Path.GetFileNameWithoutExtension(config.Output)+".lib\"";
					//TODO: Are there import libs on other platforms?
					break;
				case DCompileTargetType.StaticLibrary:
					linkArgs += "-lib";
					break;
			}
			return linkArgs;
		}
			
	}
	
	public class GDCCompilerCommandBuilder:DMDCompilerCommandBuilder
	{
		public GDCCompilerCommandBuilder(DProject prj, DProjectConfiguration config):base(prj, config)
		{
			this.compilerCommand = "gdc";	
			this.linkerCommand = "gdc";
			this.compilerDebugArgs = "-g";			
			this.compilerOutputFileSwitch = "-o ";
			this.linkerOutputFileSwitch = "-o ";			
		}		
	}
	
	public class LDCCompilerCommandBuilder:DMDCompilerCommandBuilder
	{
		public LDCCompilerCommandBuilder(DProject prj, DProjectConfiguration config):base(prj, config)
		{
			this.compilerCommand = "ldc";	
			this.linkerCommand = "ldc";
			this.compilerDebugArgs = "-gc";
			this.linkerDebugArgs = "-gc";			
		}
	}
}

