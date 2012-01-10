using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using System.IO;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core;
using System.CodeDom.Compiler;
using System.Text.RegularExpressions;

namespace MonoDevelop.D.Building
{
	public class ProjectBuilder
	{
		#region Properties
		public DCompilerConfiguration Compiler
		{
			get { return Project.Compiler; }
		}

		public DCompileTarget BuildTargetType;

		LinkTargetConfiguration Commands { get { return Compiler.GetTargetConfiguration(BuildTargetType); } }
		BuildConfiguration Arguments { get { return Commands.GetArguments(BuildConfig.DebugMode); } }

		DProject Project;
		DProjectConfiguration BuildConfig;
		string AbsoluteObjectDirectory;

		IProgressMonitor monitor;
		List<string> BuiltObjects = new List<string>();
		CompilerResults compilerResults;
		BuildResult targetBuildResult;
		List<string> sourceFileIncludePaths = new List<string>();
		#endregion

		protected ProjectBuilder() { }

		public static BuildResult CompileProject(IProgressMonitor ProgressMonitor, DProject Project, ConfigurationSelector BuildConfigurationSelector)
		{
			return new ProjectBuilder().Build(ProgressMonitor, Project, BuildConfigurationSelector);
		}

		/// <summary>
		/// Compiles a D project.
		/// </summary>
		public BuildResult Build(IProgressMonitor ProgressMonitor,DProject Project, ConfigurationSelector BuildConfigurationSelector)
		{
			monitor = ProgressMonitor;
			this.Project = Project;
			BuildConfig = Project.GetConfiguration(BuildConfigurationSelector) as DProjectConfiguration;
			BuildTargetType = Project.CompileTarget;

			BuiltObjects.Clear();
			AbsoluteObjectDirectory = Path.Combine(Project.BaseDirectory, BuildConfig.ObjectDirectory);
			
			if (!Directory.Exists(AbsoluteObjectDirectory))
				Directory.CreateDirectory(AbsoluteObjectDirectory);

			compilerResults = new CompilerResults(new TempFileCollection());

			targetBuildResult = new BuildResult();

			monitor.BeginTask("Build Project", Project.Files.Count + 1);

			sourceFileIncludePaths.Clear();
			sourceFileIncludePaths.AddRange(Compiler.GlobalParseCache.DirectoryPaths);
			sourceFileIncludePaths.AddRange(Project.LocalIncludeCache.DirectoryPaths);

			var modificationsDone = false;

			foreach (var f in Project.Files)
			{
				if (monitor.IsCancelRequested)
					return targetBuildResult;

				// If not compilable, skip it
				if (f.BuildAction != BuildAction.Compile || !File.Exists(f.FilePath))
					continue;

				// a.Check if source file was modified and if object file still exists
				if (Project.EnableIncrementalLinking &&
					Project.LastModificationTimes.ContainsKey(f) &&
					Project.LastModificationTimes[f] == File.GetLastWriteTime(f.FilePath) &&
					File.Exists(f.LastGenOutput))
				{
					// File wasn't edited since last build
					// but add the built object to the objs array
					BuiltObjects.Add(f.LastGenOutput);
					monitor.Step(1);
					continue;
				}

				modificationsDone = true;

				if (f.Name.EndsWith(".rc", StringComparison.OrdinalIgnoreCase))
					CompileResourceScript(f);
				else
					CompileSource(f);
			}

			if (targetBuildResult.FailedBuildCount==0)
			{
				LinkToTarget(!Project.EnableIncrementalLinking || modificationsDone);
			}

			monitor.EndTask();
			
			foreach(CompilerError compilerError in compilerResults.Errors)
				if (compilerError.IsWarning)
					targetBuildResult.AddWarning(compilerError.FileName,
					                           compilerError.Line,
					                           compilerError.Column,
					                           compilerError.ErrorNumber,
					                           compilerError.ErrorText);
				else
					targetBuildResult.AddError(compilerError.FileName,
					                           compilerError.Line,
					                           compilerError.Column,
					                           compilerError.ErrorNumber,
					                           compilerError.ErrorText);

			return targetBuildResult;
		}

		void CompileSource(ProjectFile f)
		{			
			string obj = null;

			if (File.Exists(f.LastGenOutput))
			{
				obj = f.LastGenOutput;

				File.Delete(obj);
			}
			else
				obj = HandleObjectFileNaming(f, DCompiler.ObjectExtension);

			// Create argument string for source file compilation.
			var dmdArgs = FillInMacros(Arguments.CompilerArguments + " " + BuildConfig.ExtraCompilerArguments, new DCompilerMacroProvider
			{
				IncludePathConcatPattern = Commands.IncludePathPattern,
				SourceFile = f.ProjectVirtualPath,
				ObjectFile = obj,
				Includes = sourceFileIncludePaths,
			});

			// b.Execute compiler
			string dmdOutput;
			string stdOutput;
			
			var compilerExecutable=Commands.Compiler;
			if(!Path.IsPathRooted(compilerExecutable)){
				compilerExecutable=Path.Combine(Compiler.BinPath,Commands.Compiler);
				
				if(!File.Exists(compilerExecutable))
					compilerExecutable=Commands.Compiler;
			}
			
			int exitCode = ExecuteCommand(compilerExecutable, dmdArgs, Project.BaseDirectory, monitor, out dmdOutput, out stdOutput);

			HandleCompilerOutput(dmdOutput);
			HandleCompilerOutput(stdOutput);
			HandleReturnCode(compilerExecutable,exitCode);

			monitor.Step(1);

			if (exitCode != 0)
			{
				targetBuildResult.FailedBuildCount++;
			}
			else
			{
				f.LastGenOutput = obj;
				targetBuildResult.BuildCount++;
				Project.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);

				// Especially when compiling large projects, do only add the relative part of the obj file due to command shortness
				if (obj.StartsWith(Project.BaseDirectory))
					BuiltObjects.Add(obj.Substring(Project.BaseDirectory.ToString().Length).TrimStart(Path.DirectorySeparatorChar));
				else
					BuiltObjects.Add(obj);
			}
		}

		void CompileResourceScript(ProjectFile f)
		{
			string res = null;

			if (File.Exists(f.LastGenOutput))
			{
				res = f.LastGenOutput;

				File.Delete(res);
			}
			else
				res = HandleObjectFileNaming(f, ".res");

			// Build argument string
			var resCmpArgs = FillInMacros(Win32ResourceCompiler.Instance.Arguments,
				new Win32ResourceCompiler.ArgProvider
				{
					RcFile = f.ProjectVirtualPath,
					ResFile = res
				});

			// Execute compiler
			string output;
			string stdOutput;
			
			int _exitCode = ExecuteCommand(Win32ResourceCompiler.Instance.Executable,
				resCmpArgs,
				Project.BaseDirectory,
				monitor,
				out output,
				out stdOutput);

			// Error analysis
			if (!string.IsNullOrEmpty(output))
				compilerResults.Errors.Add(new CompilerError { FileName = f.FilePath, ErrorText = output });
			if (!string.IsNullOrEmpty(stdOutput))
				compilerResults.Errors.Add(new CompilerError { FileName = f.FilePath, ErrorText = stdOutput });
			HandleReturnCode(Win32ResourceCompiler.Instance.Executable,_exitCode);

			monitor.Step(1);

			if (_exitCode != 0)
			{
				targetBuildResult.FailedBuildCount++;
			}
			else
			{
				f.LastGenOutput = res;
				targetBuildResult.BuildCount++;
				Project.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);

				// Especially when compiling large projects, do only add the relative part of the r file due to command shortness
				if (res.StartsWith(Project.BaseDirectory))
					BuiltObjects.Add(res.Substring(Project.BaseDirectory.ToString().Length).TrimStart(Path.DirectorySeparatorChar));
				else
					BuiltObjects.Add(res);
			}
		}

		void LinkToTarget(bool modificationsDone)
		{
			/// The target file to which all objects will be linked to
			var LinkTargetFile = BuildConfig.OutputDirectory.Combine(BuildConfig.CompiledOutputName);

			if (!modificationsDone &&
				File.Exists(LinkTargetFile))
			{
				monitor.ReportSuccess("Build successful! - No new linkage needed");
				monitor.Step(1);
				return;
			}

			// b.Build linker argument string
			// Build argument preparation
			var libs = new List<string>(Compiler.DefaultLibraries);
			libs.AddRange(Project.ExtraLibraries);

			var linkArgs = FillInMacros(Arguments.LinkerArguments + " " + BuildConfig.ExtraLinkerArguments,
				new DLinkerMacroProvider
				{
					ObjectsStringPattern = Commands.ObjectFileLinkPattern,
					Objects = BuiltObjects.ToArray(),
					TargetFile = LinkTargetFile,
					RelativeTargetDirectory = BuildConfig.OutputDirectory.ToRelative(Project.BaseDirectory),
					Libraries = libs
				});
			var linkerOutput = "";
			var linkerErrorOutput = "";
			
			var linkerExecutable=Commands.Compiler;
			if(!Path.IsPathRooted(linkerExecutable)){
				linkerExecutable=Path.Combine(Compiler.BinPath,Commands.Linker);
				
				if(!File.Exists(linkerExecutable))
					linkerExecutable=Commands.Linker;
			}
			
			int exitCode = ExecuteCommand(linkerExecutable, linkArgs, Project.BaseDirectory, monitor, 
				out linkerErrorOutput,
				out linkerOutput);

			HandleOptLinkOutput(linkerOutput);

			HandleReturnCode(linkerExecutable,exitCode);

			if (exitCode == 0)
				monitor.ReportSuccess("Build successful!");
		}

		private void HandleOptLinkOutput(string linkerOutput)
		{
			var matches=optlinkRegex.Matches(linkerOutput);

			foreach (Match match in matches)
			{
				var error = new CompilerError();

				// Get associated D source file
				if (match.Groups["obj"].Success)
				{
					var obj = Project.GetAbsoluteChildPath(new FilePath(match.Groups["obj"].Value));

					foreach(var pf in Project.Files)
						if (pf.LastGenOutput == obj)
						{
							error.FileName = pf.FilePath;
							break;
						}
				}

				error.ErrorText="Linker error "+match.Groups["code"].Value+" - "+match.Groups["message"].Value;

				compilerResults.Errors.Add(error);
			}
		}

		string HandleObjectFileNaming(ProjectFile f, string extension)
		{
			var obj= Path.Combine(AbsoluteObjectDirectory, Path.GetFileNameWithoutExtension(f.FilePath)) + extension;

			if (File.Exists(obj) && 
				!BuiltObjects.Contains(
				obj.StartsWith(Project.BaseDirectory) ? obj.Substring(Project.BaseDirectory.ToString().Length + 1) : obj
				))
			{
				File.Delete(obj);
				return obj;
			}

			int i = 2;
			while (File.Exists(obj))
			{
				// Simply add a number between the obj name and its extension
				obj = Path.Combine(AbsoluteObjectDirectory, Path.GetFileNameWithoutExtension(f.FilePath)) + i + extension;
				i++;
			}

			return obj;
		}

		#region Build argument creation

		/// <summary>
		/// Scans through RawArgumentString for macro uses (e.g. -of"$varname") and replace found variable matches with values provided by MacroProvider
		/// </summary>
		public static string FillInMacros(string RawArgumentString, IArgumentMacroProvider MacroProvider)
		{
			var returnArgString = RawArgumentString;

			string tempId = "";
			char c = '\0';
			for (int i = RawArgumentString.Length - 1; i >= 0; i--)
			{
				c = RawArgumentString[i];

				if (char.IsLetterOrDigit(c) || c == '_')
					tempId = c + tempId;
				else if (c == '$' && tempId.Length > 0)
				{
					var replacement = MacroProvider.Replace(tempId);

					//ISSUE: Replace undefined variables with nothing?
					if (replacement == tempId || replacement == null)
						replacement = "";

					returnArgString = returnArgString.Substring(0, i) + replacement + returnArgString.Substring(i + tempId.Length + 1); // "+1" because of the initially skipped '$'

					tempId = "";
				}
				else
					tempId = "";
			}

			return returnArgString;
		}

		#endregion

		/// <summary>
		/// Scans errorString line-wise for filename-line-message patterns (e.g. "myModule(1): Something's wrong here") and add these error locations to the CompilerResults cr.
		/// </summary>
		protected void HandleCompilerOutput(string errorString)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				var error = FindError(next, reader);
				if (error != null)
					compilerResults.Errors.Add(error);
			}

			reader.Close();
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
		private static Regex noColRegex_2 = new Regex(
			@"^\s*((?<file>.*)(\()(?<line>\d*)(\)):\s*(?<message>.*))|(Error:)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		private static Regex gcclinkerRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):((?<column>\d*):)?\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		/// <summary>
		/// Default OptLink regex for recognizing errors and their origins
		/// </summary>
		private static Regex optlinkRegex = new Regex(
			@"\n(?<obj>[a-zA-Z0-9/\\.]+)\((?<module>[a-zA-Z0-9]+)\) (?<offset>[a-zA-Z0-9 ]+)?(\r)?\n Error (?<code>\d*): (?<message>[a-zA-Z0-9_ :]+)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		public static CompilerError FindError(string errorString, TextReader reader)
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
		void HandleReturnCode(string executable,int returnCode)
		{
			compilerResults.NativeCompilerReturnValue = returnCode;

			if (returnCode != 0)
			{
				if (monitor != null)
					monitor.Log.WriteLine("Exit code " + returnCode.ToString());
			}

			if (0 != returnCode && 0 == compilerResults.Errors.Count)
			{
				compilerResults.Errors.Add(new CompilerError(string.Empty, 0, 0, string.Empty,
												  GettextCatalog.GetString("Build failed - check build output for details")));
			}
		}

		

		/// <summary>
		/// Executes a file and reports events related to the execution to the 'monitor' passed in the parameters.
		/// </summary>
		static int ExecuteCommand(
			string command, 
			string args, 
			string baseDirectory, 

			IProgressMonitor monitor, 
			out string errorOutput,
			out string programOutput)
		{
			errorOutput = string.Empty;
			int exitCode = -1;

			var swError = new StringWriter();
			var swOutput = new StringWriter();

			var chainedError = new LogTextWriter();
			chainedError.ChainWriter(monitor.Log);
			chainedError.ChainWriter(swError);

			var chainedOutput = new LogTextWriter();
			chainedOutput.ChainWriter(monitor.Log);
			chainedOutput.ChainWriter(swOutput);

			monitor.Log.WriteLine("{0} {1}", command, args);

			var operationMonitor = new AggregatedOperationMonitor(monitor);

			try
			{
				var p = Runtime.ProcessService.StartProcess(command, args, baseDirectory, chainedOutput, chainedError, null);
				operationMonitor.AddOperation(p); //handles cancellation
				

				p.WaitForOutput();
				errorOutput = swError.ToString();
				programOutput = swOutput.ToString();
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
