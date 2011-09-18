using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.IO;
using MonoDevelop.Core.ProgressMonitoring;
using System.CodeDom.Compiler;
using System.Text.RegularExpressions;

namespace MonoDevelop.D
{
	public class DMDCompiler
	{
		protected string compilerCommand = "dmd";
		protected string linkerCommand;
		protected string linkerCommand_windows = "dmd";
		protected string linkerCommand_linux = "gcc";		
		

		// Arguments that are inserted additionally (by default!)
		protected string compilerDebugArgs = "-g -debug";
		protected string compilerReleaseArgs = "-O -release -inline";
		
		protected string linkerDebugArgs;
		protected string linkerReleaseArgs;		
		protected string linkerDebugArgs_windows = "-g -debug";
		protected string linkerReleaseArgs_windows = "-O -release -inline";
		
		protected string linkerDebugArgs_linux = "-g -debug"; //not sure about theses for gcc
		protected string linkerReleaseArgs_linux = "-O -release -inline"; //not about these for gcc
			
		public DMDCompiler()
		{						
			if ((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX))
			{
				linkerCommand = linkerCommand_linux;				
				linkerDebugArgs = linkerDebugArgs_linux;
				linkerReleaseArgs = linkerDebugArgs_linux;
			} else {
				linkerCommand = linkerCommand_windows;
				linkerDebugArgs = linkerDebugArgs_windows;
				linkerReleaseArgs = linkerReleaseArgs_windows;
			}						
		}

		public string CompilerCommand
		{
			get { return compilerCommand; }
		}

		public BuildResult Compile(
			DProject prj, 
			ProjectFileCollection files, 
			DProjectConfiguration cfg, 
			IProgressMonitor monitor)
		{
			// Determine default object extension
			var objExt = ".obj";
			if (Environment.OSVersion.Platform == PlatformID.MacOSX ||
				Environment.OSVersion.Platform == PlatformID.Unix)
				objExt = ".o";

			var relObjDir = "objs";
			var objDir = Path.Combine(prj.BaseDirectory, relObjDir);

			if(!Directory.Exists(objDir))
				Directory.CreateDirectory(objDir);

			/*
			 * 1) Compile all D sources
			 *	a. Check if modified
			 *	b. If so, compile
			 * 2) Link them
			 *	a. Check if there were modifications made
			 *	b. If so, link
			 */

			// List of created object files
			var objs = new List<string>();
			var compilerResults = new CompilerResults(new TempFileCollection());
			var buildResult = new BuildResult(compilerResults, "");
			bool succesfullyBuilt = true;
			bool modificationsDone = false;

			monitor.BeginTask("Build Project", files.Count + 1);

			// 1)
			foreach (var f in files)
			{
				if (monitor.IsCancelRequested)
					return buildResult;

				// If not compilable, skip it
				if (f.BuildAction != BuildAction.Compile || !File.Exists(f.FilePath))
					continue;

				// Create object file path
				var obj = Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath)) + objExt;
				
				// a.Check if source file was modified and if object file still exists
				if (prj.LastModificationTimes.ContainsKey(f) &&
					prj.LastModificationTimes[f] == File.GetLastWriteTime(f.FilePath) &&
					File.Exists(f.LastGenOutput))
				{
					// File wasn't edited since last build
					// but add the built object to the objs array
					objs.Add(f.LastGenOutput);
					monitor.Step(1);
					continue;
				}
				// If source was modified and if obj file is existing, delete it
				else
				{
					modificationsDone = true;
					if (File.Exists(obj))
						File.Delete(obj);
				}

				// Prevent duplicates e.g. when having the samely-named source files in different sub-packages
				int i=2;
				while(File.Exists(obj))
				{
					// Simply add a number between the obj name and its extension
					obj= Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath))+i + objExt;
					i++;
				}
				
				var dmdincludes = new StringBuilder();
				if (cfg.Includes != null)
					foreach (string inc in cfg.Includes)
						dmdincludes.AppendFormat(" -I\"{0}\"", inc);	
				
				// b.Build argument string
				var dmdArgs = "-c \"" + f.FilePath + "\" -of\"" + obj + "\" " + 
					(cfg.DebugMode?compilerDebugArgs:compilerReleaseArgs) + 
					cfg.ExtraCompilerArguments + dmdincludes.ToString();
				
			
				
				// b.Execute compiler
				string dmdOutput;
				int exitCode = ExecuteCommand(compilerCommand, dmdArgs, prj.BaseDirectory, monitor, out dmdOutput);

				ParseCompilerOutput(dmdOutput, compilerResults);
				CheckReturnCode(exitCode, compilerResults);

				monitor.Step(1);

				if (exitCode != 0)
				{
					buildResult.FailedBuildCount++;
					succesfullyBuilt = false;
					break;
				}
				else
				{
					f.LastGenOutput = obj;
					buildResult.BuildCount++;
					prj.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);
					objs.Add(obj);
				}
			}

			// 2)
			if (succesfullyBuilt)
			{
				// a.
				if (!modificationsDone)
				{
					// Only return if build target is still existing
					if (File.Exists(cfg.CompiledOutputName))
					{
						monitor.Step(1);
						return new BuildResult(compilerResults, "");
					}
				}

				// b.Build linker argument string
				var objsArg = "";
				foreach (var o in objs)
				{
					var o_ = o;

					if (o_.StartsWith(prj.BaseDirectory))
						o_ = o_.Substring(prj.BaseDirectory.ToString().Length).TrimStart('/','\\');

					objsArg += "\"" + o_ + "\" ";
				}
				
				var linkArgs = ""; 				
				if ((Environment.OSVersion.Platform == PlatformID.Unix) || (Environment.OSVersion.Platform == PlatformID.MacOSX))
				{
					StringBuilder formattedlibs = new StringBuilder();
					if (cfg.Libs != null)
						foreach (var lib in cfg.Libs)
							formattedlibs.AppendFormat (" -\"{0}\"", lib);					
					
					linkArgs =
						string.Format ("-o {0} -B {1} {2} {3} " +
						(cfg.DebugMode?linkerDebugArgs:linkerReleaseArgs),
						Path.Combine(cfg.OutputDirectory,cfg.CompiledOutputName), cfg.OutputDirectory, objsArg, formattedlibs.ToString());										
				}else{
					linkArgs =
						objsArg.TrimEnd()+ " -L/NOLOGO "+
						(cfg.DebugMode?linkerDebugArgs:linkerReleaseArgs) +
						" -of\""+Path.Combine(cfg.OutputDirectory,cfg.CompiledOutputName)+"\"";					
				}
				
				switch (cfg.CompileTarget)
				{
					case DCompileTargetType.SharedLibrary:
						if (cfg.CompiledOutputName.EndsWith(".dll"))
							linkArgs += " -L/IMPLIB:\""+Path.GetFileNameWithoutExtension(cfg.Output)+".lib\"";
						//TODO: Are there import libs on other platforms?
						break;
					case DCompileTargetType.StaticLibrary:
						linkArgs += "-lib";
						break;
				}

				var linkerOutput = "";
				int exitCode = ExecuteCommand(linkerCommand,linkArgs,prj.BaseDirectory,monitor,out linkerOutput);

				compilerResults.NativeCompilerReturnValue = exitCode;

				CheckReturnCode(exitCode, compilerResults);

				if (exitCode == 0)
				{
					monitor.ReportSuccess("Build successful!");
					monitor.Step(1);
				}
			}

			return new BuildResult(compilerResults,"");
		}


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
		
		
		CompilerError CreateErrorFromErrorString(string errorString, TextReader reader)
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

		protected void ParseCompilerOutput(string errorString, CompilerResults cr)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				CompilerError error = CreateErrorFromErrorString(next, reader);
				if (error != null)
					cr.Errors.Add(error);
			}

			reader.Close();
		}

		/// <summary>
		/// Checks a compilation return code, 
		/// and adds an error result if the compiler results
		/// show no errors.
		/// </summary>
		/// <param name="returnCode">
		/// A <see cref="System.Int32"/>: A process return code
		/// </param>
		/// <param name="cr">
		/// A <see cref="CompilerResults"/>: The return code from a compilation run
		/// </param>
		static void CheckReturnCode(int returnCode, CompilerResults cr)
		{
			cr.NativeCompilerReturnValue = returnCode;
			if (0 != returnCode && 0 == cr.Errors.Count)
			{
				cr.Errors.Add(new CompilerError(string.Empty, 0, 0, string.Empty,
												  GettextCatalog.GetString("Build failed - check build output for details")));
			}
		}
		#endregion

		int ExecuteCommand(string command, string args, string baseDirectory, IProgressMonitor monitor, out string errorOutput)
		{
			errorOutput = string.Empty;
			int exitCode = -1;

			var swError = new StringWriter();
			var chainedError = new LogTextWriter();
			chainedError.ChainWriter(monitor.Log);
			chainedError.ChainWriter(swError);

			monitor.Log.WriteLine("{0} {1}", command, args);

			var operationMonitor = new AggregatedOperationMonitor(monitor);

			try
			{
				var p = Runtime.ProcessService.StartProcess(command, args, baseDirectory, monitor.Log, chainedError, null);
				operationMonitor.AddOperation(p); //handles cancellation

				p.WaitForOutput();
				errorOutput = swError.ToString();
				exitCode = p.ExitCode;
				p.Dispose();

				if (monitor.IsCancelRequested)
				{
					monitor.Log.WriteLine(GettextCatalog.GetString("Build cancelled"));
					monitor.ReportError(GettextCatalog.GetString("Build cancelled"), null);
					if (exitCode == 0)
						exitCode = -1;
				}
			}
			finally
			{
				chainedError.Close();
				swError.Close();
				operationMonitor.Dispose();
			}

			return exitCode;
		}
	}
}
