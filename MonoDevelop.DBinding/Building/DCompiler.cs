using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;

namespace MonoDevelop.D.Building
{
	public enum DCompileTarget
	{
		/// <summary>
		/// A normal console application.
		/// </summary>
		Executable,

		/// <summary>
		/// Applications which explicitly draw themselves a custom GUI and do not need a console.
		/// Usually 'Desktop' applications.
		/// </summary>
		ConsolelessExecutable,

		SharedLibrary,
		StaticLibrary
	}

	public enum DCompilerVendor
	{
		DMD,
		GDC,
		LDC
	}

	/// <summary>
	/// Central class which enables build support for D projects in MonoDevelop.
	/// </summary>
	[DataItem("DCompiler")]
	public class DCompiler
	{
		#region Init/Loading & Saving
		public static void Init()
		{
			Instance = PropertyService.Get<DCompiler>(GlobalPropertyName);

			if (Instance == null)
			{
				Instance = new DCompiler
				{
					Dmd = new DCompilerConfiguration(DCompilerVendor.DMD),
					Gdc = new DCompilerConfiguration(DCompilerVendor.GDC),
					Ldc = new DCompilerConfiguration(DCompilerVendor.LDC)
				};

				Instance.Save();
			}
		}

		public void Save()
		{
			PropertyService.Set(GlobalPropertyName, this);
			PropertyService.SaveProperties();
		}

		const string GlobalPropertyName = "DBinding.DCompiler";
		#endregion

		[ItemProperty]
		public DCompilerVendor DefaultCompiler = DCompilerVendor.DMD;

		public static DCompiler Instance;

		/// <summary>
		/// Static object which stores all global information about the dmd installation which probably exists on the programmer's machine.
		/// </summary>
		[ItemProperty]
		public DCompilerConfiguration Dmd;
		[ItemProperty]
		public DCompilerConfiguration Gdc;
		[ItemProperty]
		public DCompilerConfiguration Ldc;

		/// <summary>
		/// Returns the default compiler configuration
		/// </summary>
		public static DCompilerConfiguration GetDefaultCompiler()
		{
			return GetCompiler(Instance.DefaultCompiler);
		}

		public static DCompilerConfiguration GetCompiler(DCompilerVendor type)
		{
			switch (type)
			{
				case DCompilerVendor.GDC:
					return Instance.Gdc;
				case DCompilerVendor.LDC:
					return Instance.Ldc;
			}

			return Instance.Dmd;
		}
		
		/// <summary>
		/// Compiles a D project.
		/// </summary>
		public static BuildResult Compile(
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

			var Compiler = GetCompiler(cfg.Compiler);
			var Arguments=Compiler.GetArgumentCollection(cfg.DebugMode);

			/// The target file to which all objects will be linked to
			var LinkTarget = cfg.OutputDirectory.Combine(cfg.CompiledOutputName);
				
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

				// Create argument string for source file compilation.
				var dmdArgs = FillInMacros(Arguments.SourceCompilerArguments,  new DCompilerMacroProvider 
				{ 
					SourceFile = f.FilePath, 
					ObjectFile = obj 
				});			
				
				// b.Execute compiler
				string dmdOutput;
				int exitCode = ExecuteCommand(Compiler.CompilerExecutable, dmdArgs, prj.BaseDirectory, monitor, out dmdOutput);

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

					// Especially when compiling large projects, do only add the relative part of the obj file due to command shortness
					if (obj.StartsWith(prj.BaseDirectory))
						objs.Add(obj.Substring(prj.BaseDirectory.ToString().Length).TrimStart(Path.DirectorySeparatorChar));
					else
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
					if (File.Exists(LinkTarget))
					{
						monitor.Step(1);
						return new BuildResult(compilerResults, "");
					}
				}

				// b.Build linker argument string
				var linkArgs = FillInMacros(Arguments.GetLinkerArgumentString(prj.CompileTarget), new DLinkerMacroProvider { 
					Objects=objs.ToArray(),
					TargetFile=LinkTarget,
					RelativeTargetDirectory=cfg.OutputDirectory.ToRelative(prj.BaseDirectory)
				});
				var linkerOutput = "";
				int exitCode = ExecuteCommand(Compiler.LinkerExecutable,linkArgs,prj.BaseDirectory,monitor,out linkerOutput);

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

		#region Build argument creation

		/// <summary>
		/// Scans through RawArgumentString for macro uses (e.g. -of"$varname") and replace found variable matches with values provided by MacroProvider
		/// </summary>
		public static string FillInMacros(string RawArgumentString, IArgumentMacroProvider MacroProvider)
		{
			var returnArgString = RawArgumentString;

			string tempId = "";
			char c='\0';
			for (int i = RawArgumentString.Length - 1; i >= 0; i--)
			{
				c = RawArgumentString[i];

				if (char.IsLetterOrDigit(c) || c == '_')
					tempId = c+tempId;
				else if (c == '$' && tempId.Length>0)
				{
					var replacement = MacroProvider.Replace(tempId);

					//ISSUE: Replace undefined variables with nothing?
					if (replacement == tempId || replacement == null)
						replacement = "";

					returnArgString = returnArgString.Substring(0, i) + replacement + returnArgString.Substring(i + tempId.Length +1); // "+1" because of the initially skipped '$'

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
		protected static void ParseCompilerOutput(string errorString, CompilerResults cr)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				var error = FindError(next, reader);
				if (error != null)
					cr.Errors.Add(error);
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
		static void CheckReturnCode(int returnCode, CompilerResults cr)
		{
			cr.NativeCompilerReturnValue = returnCode;
			if (0 != returnCode && 0 == cr.Errors.Count)
			{
				cr.Errors.Add(new CompilerError(string.Empty, 0, 0, string.Empty,
												  GettextCatalog.GetString("Build failed - check build output for details")));
			}
		}

		/// <summary>
		/// Executes a file and reports events related to the execution to the 'monitor' passed in the parameters.
		/// </summary>
		static int ExecuteCommand(string command, string args, string baseDirectory, IProgressMonitor monitor, out string errorOutput)
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
