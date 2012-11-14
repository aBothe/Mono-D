using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using System.Text;

namespace MonoDevelop.D.Building
{
	public class ProjectBuilder
	{
        #region Properties
		public DCompilerConfiguration Compiler {
			get { return Project.Compiler; }
		}

		public DCompileTarget BuildTargetType { get { return BuildConfig.CompileTarget; } }

		LinkTargetConfiguration LinkTargetCfg { get { return Compiler.GetOrCreateTargetConfiguration (BuildTargetType); } }

		BuildConfiguration BuildArguments { get { return LinkTargetCfg.GetArguments (BuildConfig.DebugMode); } }

		DProject Project;
		DProjectConfiguration BuildConfig;
		string AbsoluteObjectDirectory;
		IArgumentMacroProvider commonMacros;
		IProgressMonitor monitor;
		List<string> BuiltObjects = new List<string> ();

		/// <summary>
		/// In this list, all directories of files that are 'linked' to the project are put in.
		/// Used for multiple-step building only.
		/// </summary>
		List<string> FileLinkDirectories = new List<string>();

		public bool CanDoOneStepBuild {
			get {
				return Project.PreferOneStepBuild && BuildArguments.SupportsOneStepBuild;
			}
		}
        #endregion

		protected ProjectBuilder (IProgressMonitor monitor)
		{
			this.monitor = monitor;
		}

		public static BuildResult CompileProject (IProgressMonitor ProgressMonitor, DProject Project, ConfigurationSelector BuildConfigurationSelector)
		{
			return new ProjectBuilder (ProgressMonitor).Build (Project, BuildConfigurationSelector);
		}

		/// <summary>
		/// Compiles a D project.
		/// </summary>
		public BuildResult Build (DProject Project, ConfigurationSelector BuildConfigurationSelector)
		{
			this.Project = Project;
			BuildConfig = Project.GetConfiguration (BuildConfigurationSelector) as DProjectConfiguration;
			commonMacros = new PrjPathMacroProvider { 
				slnPath = Project.ParentSolution.BaseDirectory
			};
			BuiltObjects.Clear ();

			if (Compiler == null) {
				var targetBuildResult = new BuildResult ();

				targetBuildResult.AddError ("Project compiler \"" + Project.UsedCompilerVendor + "\" not found");
				targetBuildResult.FailedBuildCount++;

				return targetBuildResult;
			}

			if (Path.IsPathRooted (BuildConfig.ObjectDirectory))
				AbsoluteObjectDirectory = BuildConfig.ObjectDirectory;
			else
				AbsoluteObjectDirectory = Path.Combine (Project.BaseDirectory, EnsureCorrectPathSeparators (BuildConfig.ObjectDirectory));

			if (!Directory.Exists (AbsoluteObjectDirectory))
				Directory.CreateDirectory (AbsoluteObjectDirectory);

			if (CanDoOneStepBuild)
				return DoOneStepBuild ();
			else
				return DoStepByStepBuild ();
		}

		BuildResult DoOneStepBuild ()
		{
			var br = new BuildResult ();
            
			// Enum files & build resource files
			foreach (var pf in Project.Files) {
				if (pf.BuildAction != BuildAction.Compile || pf.Subtype == Subtype.Directory)
					continue;

				if (pf.FilePath.Extension.EndsWith (".rc", StringComparison.OrdinalIgnoreCase)) {
					if (!CompileResourceScript (br, pf))
						return br;
				} else
					BuiltObjects.Add (GetRelObjFilename (pf.FilePath));
			}

			// Build argument string
			var target = Project.GetOutputFileName (BuildConfig.Selector);

		

			var argumentString = FillInMacros (AdditionalCompilerAttributes +
                BuildArguments.OneStepBuildArguments.Trim () + " " +
                BuildConfig.ExtraCompilerArguments.Trim () + " " +
                BuildConfig.ExtraLinkerArguments.Trim (),
            new OneStepBuildArgumentMacroProvider
            {
                ObjectsStringPattern = LinkTargetCfg.Patterns.ObjectFileLinkPattern,
                IncludesStringPattern = LinkTargetCfg.Patterns.IncludePathPattern,

                SourceFiles = BuiltObjects,
                Includes = FillCommonMacros(Project.IncludePaths),
                Libraries = GetLibraries(BuildConfig,BuildArguments),

                RelativeTargetDirectory = BuildConfig.OutputDirectory,
                ObjectsDirectory = BuildConfig.ObjectDirectory,
                TargetFile = target,
            }, commonMacros);

			// Execute the compiler
			var stdOut = "";
			var stdError = "";

			var linkerExecutable = LinkTargetCfg.Compiler;
			if (!Path.IsPathRooted (linkerExecutable)) {
				linkerExecutable = Path.Combine (Compiler.BinPath, LinkTargetCfg.Linker);

				if (!File.Exists (linkerExecutable))
					linkerExecutable = LinkTargetCfg.Linker;
			}

            monitor.Log.WriteLine("Current dictionary: " + Project.BaseDirectory);

			int exitCode = ExecuteCommand (linkerExecutable, argumentString, Project.BaseDirectory, monitor,
                out stdError,
                out stdOut);

			HandleCompilerOutput (br, stdError);
			HandleCompilerOutput (br, stdOut);
			HandleOptLinkOutput (br, stdOut);
			HandleReturnCode (br, linkerExecutable, exitCode);

			return br;
		}

		BuildResult DoStepByStepBuild ()
		{
			monitor.BeginTask ("Build Project", Project.Files.Count + 1);

            monitor.Log.WriteLine("Current dictionary: "+Project.BaseDirectory);

			var br = new BuildResult ();
			var modificationsDone = false;

			FileLinkDirectories.Clear();
			foreach (var f in Project.Files)
			{
				if (!f.IsLink || !f.IsExternalToProject || f.BuildAction != BuildAction.Compile)
					continue;

				FileLinkDirectories.Add(f.FilePath.ParentDirectory);
			}

			foreach (var f in Project.Files) {
				if (monitor.IsCancelRequested)
					return br;

				// If not compilable, skip it
				if (f.BuildAction != BuildAction.Compile || !File.Exists (f.FilePath))
					continue;

				// a.Check if source file was modified and if object file still exists
				if (Project.EnableIncrementalLinking &&
                    !string.IsNullOrEmpty (f.LastGenOutput) &&
                    f.LastGenOutput.StartsWith (AbsoluteObjectDirectory) &&
                    Project.LastModificationTimes.ContainsKey (f) &&
                    Project.LastModificationTimes [f] == File.GetLastWriteTime (f.FilePath) &&
                    File.Exists (f.LastGenOutput)) {
					// File wasn't edited since last build
					// but add the built object to the objs array
					BuiltObjects.Add (f.LastGenOutput);
					monitor.Step (1);
					continue;
				}

				modificationsDone = true;

				if (f.Name.EndsWith (".rc", StringComparison.OrdinalIgnoreCase))
					CompileResourceScript (br, f);
				else
					CompileSource (br, f);

				monitor.Step (1);
			}

			if (br.FailedBuildCount == 0) 
				LinkToTarget (br, !Project.EnableIncrementalLinking || modificationsDone);

			monitor.EndTask ();

			return br;
		}

		bool CompileSource (BuildResult targetBuildResult, ProjectFile f)
		{
			if (File.Exists (f.LastGenOutput))
				File.Delete (f.LastGenOutput);

			var obj = HandleObjectFileNaming (f, DCompilerService.ObjectExtension);

			// Create argument string for source file compilation.
			var dmdArgs = FillInMacros(AdditionalCompilerAttributes + BuildArguments.CompilerArguments + " " + BuildConfig.ExtraCompilerArguments, new DCompilerMacroProvider
            {
                IncludePathConcatPattern = LinkTargetCfg.Patterns.IncludePathPattern,
                SourceFile = f.FilePath.ToRelative(Project.BaseDirectory),
                ObjectFile = obj,
				Includes = FillCommonMacros(Project.IncludePaths).Union(FileLinkDirectories),
            },commonMacros);

			// b.Execute compiler
			string stdError;
			string stdOutput;

			var compilerExecutable = LinkTargetCfg.Compiler;
			if (!Path.IsPathRooted (compilerExecutable)) {
				compilerExecutable = Path.Combine (Compiler.BinPath, LinkTargetCfg.Compiler);

				if (!File.Exists (compilerExecutable))
					compilerExecutable = LinkTargetCfg.Compiler;
			}

			int exitCode = ExecuteCommand (compilerExecutable, dmdArgs, Project.BaseDirectory, monitor, out stdError, out stdOutput);

			HandleCompilerOutput (targetBuildResult, stdError);
			HandleCompilerOutput (targetBuildResult, stdOutput);
			HandleReturnCode (targetBuildResult, compilerExecutable, exitCode);

			if (exitCode != 0) {
				targetBuildResult.FailedBuildCount++;
				return false;
			} else {
				f.LastGenOutput = obj;

				targetBuildResult.BuildCount++;
				Project.LastModificationTimes [f] = File.GetLastWriteTime (f.FilePath);

				BuiltObjects.Add (GetRelObjFilename (obj));
				return true;
			}
		}

		bool CompileResourceScript (BuildResult targetBuildResult, ProjectFile f)
		{
			var res = HandleObjectFileNaming (f, ".res");

			// Build argument string
			var resCmpArgs = FillInMacros (Win32ResourceCompiler.Instance.Arguments,
                new Win32ResourceCompiler.ArgProvider
                {
					RcFile = f.FilePath,
                    ResFile = res
                },commonMacros);

			// Execute compiler
			string output;
			string stdOutput;

			int _exitCode = ExecuteCommand (Win32ResourceCompiler.Instance.Executable,
                resCmpArgs,
                Project.BaseDirectory,
                monitor,
                out output,
                out stdOutput);

			// Error analysis
			if (!string.IsNullOrEmpty (output))
				targetBuildResult.AddError (f.FilePath, 0, 0, "", output);
			if (!string.IsNullOrEmpty (stdOutput))
				targetBuildResult.AddError (f.FilePath, 0, 0, "", stdOutput);

			HandleReturnCode (targetBuildResult, Win32ResourceCompiler.Instance.Executable, _exitCode);

			if (_exitCode != 0) {
				targetBuildResult.FailedBuildCount++;
				return false;
			} else {
				f.LastGenOutput = res;

				targetBuildResult.BuildCount++;
				Project.LastModificationTimes [f] = File.GetLastWriteTime (f.FilePath);

				BuiltObjects.Add (GetRelObjFilename (res));
				return true;
			}
		}

		void LinkToTarget (BuildResult br, bool modificationsDone)
		{
			/// The target file to which all objects will be linked to
			var LinkTargetFile = Project.GetOutputFileName (BuildConfig.Selector);

			if (!modificationsDone &&
                File.Exists (LinkTargetFile)) {
				monitor.ReportSuccess ("Build successful! - No new linkage needed");
				monitor.Step (1);
				return;
			}

			// b.Build linker argument string
			// Build argument preparation
			var linkArgs = FillInMacros (BuildArguments.LinkerArguments + " " + BuildConfig.ExtraLinkerArguments,
                new DLinkerMacroProvider
                {
                    ObjectsStringPattern = LinkTargetCfg.Patterns.ObjectFileLinkPattern,
                    Objects = BuiltObjects.ToArray (),
                    TargetFile = LinkTargetFile,
                    RelativeTargetDirectory = BuildConfig.OutputDirectory.ToRelative (Project.BaseDirectory),
                    Libraries = GetLibraries(BuildConfig, BuildArguments)
                },commonMacros);

			var linkerOutput = "";
			var linkerErrorOutput = "";

			var linkerExecutable = LinkTargetCfg.Linker;
			if (!Path.IsPathRooted (linkerExecutable)) {
				linkerExecutable = Path.Combine (Compiler.BinPath, LinkTargetCfg.Linker);

				if (!File.Exists (linkerExecutable))
					linkerExecutable = LinkTargetCfg.Linker;
			}

			int exitCode = ExecuteCommand (linkerExecutable, linkArgs, Project.BaseDirectory, monitor,
                out linkerErrorOutput,
                out linkerOutput);

			HandleOptLinkOutput (br, linkerOutput);
			HandleReturnCode (br, linkerExecutable, exitCode);
		}

        #region File naming
		public static string EnsureCorrectPathSeparators (string file)
		{
			if (OS.IsWindows)
				return file.Replace ('/', '\\');
			else
				return file.Replace ('\\', '/');
		}

		public static string GetRelObjFilename(string baseDirectory,string obj)
		{
			return obj.StartsWith(baseDirectory) ? obj.Substring(baseDirectory.ToString().Length + 1) : obj;
		}

		string GetRelObjFilename (string obj)
		{
			return GetRelObjFilename(Project.BaseDirectory, obj);
		}

		string HandleObjectFileNaming(ProjectFile f, string extension)
		{
			return HandleObjectFileNaming(AbsoluteObjectDirectory, BuiltObjects, f, extension);
		}

		public static string HandleObjectFileNaming (string AbsoluteObjectDirectory, List<string> AlreadyBuiltObjects,ProjectFile f, string extension)
		{
			var obj = Path.Combine (AbsoluteObjectDirectory, f.FilePath.FileNameWithoutExtension) + extension;

			if (!AlreadyBuiltObjects.Contains (GetRelObjFilename (f.Project.BaseDirectory,obj))) {
				if (File.Exists (obj))
					File.Delete (obj);
				return obj;
			}

			// Take the package name + module name otherwise
			obj = Path.Combine (AbsoluteObjectDirectory,
                f.ProjectVirtualPath.ParentDirectory.ToString ().Replace (Path.DirectorySeparatorChar, '.') + "." +
                f.FilePath.FileNameWithoutExtension) + extension;

			if (!AlreadyBuiltObjects.Contains(GetRelObjFilename(f.Project.BaseDirectory, obj)))
			{
				if (File.Exists (obj))
					File.Delete (obj);
				return obj;
			}

			int i = 2;
			while (AlreadyBuiltObjects.Contains(GetRelObjFilename(f.Project.BaseDirectory, obj)) && File.Exists(obj))
			{
				// Simply add a number between the obj name and its extension
				obj = Path.Combine (AbsoluteObjectDirectory,
                        f.ProjectVirtualPath.ParentDirectory.ToString ().Replace (Path.DirectorySeparatorChar, '.') + "." +
                        f.FilePath.FileNameWithoutExtension) + i + extension;
				i++;
			}

			return obj;
		}
        #endregion

        #region Build argument creation

		/// <summary>
		/// Scans through RawArgumentString for macro uses (e.g. -of"$varname") and replace found variable matches with values provided by MacroProvider
		/// </summary>
		public static string FillInMacros (string RawArgumentString, params IArgumentMacroProvider[] MacroProvider)
		{
			var returnArgString = RawArgumentString;

			var macros = new Dictionary<string, string> ();

			foreach(var mp in MacroProvider)
				mp.ManipulateMacros (macros);

			string tempId = "";
			char c = '\0';
			for (int i = RawArgumentString.Length - 1; i >= 0; i--) {
				c = RawArgumentString [i];

				if (char.IsLetterOrDigit (c) || c == '_')
					tempId = c + tempId;
				else if (c == '$' && tempId.Length > 0) {
					string surrogate = "";

					//ISSUE: Replace unknown macros with ""?
					macros.TryGetValue (tempId, out surrogate);

					returnArgString = returnArgString.Substring (0, i) + surrogate + returnArgString.Substring (i + tempId.Length + 1); // "+1" because of the initially skipped '$'

					tempId = "";
				} else
					tempId = "";
			}

			return returnArgString;
		}

		public static List<string> FillInMacros(IEnumerable<string> rawStrings, params IArgumentMacroProvider[] macroProvider)
		{
			var l = new List<string>();
			
			foreach (var i in rawStrings)
				l.Add(FillInMacros(i, macroProvider));
			
			return l;
		}

		internal class PrjPathMacroProvider : IArgumentMacroProvider
		{
			public string slnPath;

			public void ManipulateMacros(Dictionary<string, string> macros)
			{
				macros["solution"] = slnPath;
			}
		}

		List<string> FillCommonMacros(IEnumerable<string> strings)
		{
			return FillInMacros(strings, commonMacros);
		}

		public static IEnumerable<string> GetLibraries(DProjectConfiguration projCfg, BuildConfiguration buildConfig)
		{
			var libraries = (IEnumerable<string>)FillInMacros(projCfg.ReferencedLibraries, 
				new PrjPathMacroProvider { slnPath = projCfg.Project.ParentSolution.BaseDirectory });

			if (buildConfig.EnableGDCLibPrefixing)
				libraries = HandleGdcSpecificLibraryReferencing(libraries, projCfg.Project.BaseDirectory);

			return libraries;
		}

		string AdditionalCompilerAttributes
		{
			get
			{
				return GenAdditionalAttributes(LinkTargetCfg,BuildConfig);
			}
		}

		public static string GenAdditionalAttributes (LinkTargetConfiguration linkTargetCfg, DProjectConfiguration cfg)
		{
			var sb = new StringBuilder ();
			var p = linkTargetCfg.Patterns;

			if (cfg.UnittestMode)
				sb.Append (p.UnittestFlag + " ");

			if (cfg.CustomDebugIdentifiers != null && cfg.CustomVersionIdentifiers.Length != 0)
				foreach (var id in cfg.CustomDebugIdentifiers)
					sb.Append (p.DebugDefinition + "=" + id + " ");

			if (cfg.DebugLevel > 0)
				sb.Append (p.DebugDefinition + "=" + cfg.DebugLevel + " ");

			if (cfg.DebugMode)
				sb.Append (p.DebugDefinition + " ");

			if (cfg.CustomVersionIdentifiers != null && cfg.CustomVersionIdentifiers.Length != 0)
				foreach (var id in cfg.CustomVersionIdentifiers)
					sb.Append (p.VersionDefinition + "=" + id + " ");

			// DDoc handling
			var ddocFiles = new List<string> ();
			var files = cfg.Project.GetItemFiles (true);
			foreach (var f in files)
				if (f.Extension.EndsWith (".ddoc", StringComparison.OrdinalIgnoreCase))
					ddocFiles.Add (f);

			if (ddocFiles.Count != 0) {
				sb.Append(p.EnableDDocFlag+" ");

				foreach(var ddoc in ddocFiles){
					sb.AppendFormat(p.DDocDefinitionFile,ddoc);
					sb.Append(" ");
				}

				sb.AppendFormat(p.DDocExportDirectory,Path.IsPathRooted(cfg.DDocDirectory)?
				                cfg.DDocDirectory : 
				              	(new FilePath(cfg.DDocDirectory).ToAbsolute(cfg.Project.BaseDirectory)).ToString());
				sb.Append(" ");
			}

			return sb.ToString();
		}

		static IEnumerable<string> HandleGdcSpecificLibraryReferencing(IEnumerable<string> libs,string baseDirectory)
		{
			if(libs!=null)
				foreach(var l in libs)
				{
					var lib = Path.IsPathRooted(l) ? l : Path.Combine(baseDirectory,l);

					if(File.Exists(lib))
						yield return l;
					else{
						var l_ = Path.ChangeExtension(l,null);
						if(l_.StartsWith("lib"))
							l_ = l_.Substring(3);
						yield return "-L-l"+l_;
					}
				}
		}

        #endregion

        #region Compiler Error Parsing
		/// <summary>
		/// Default OptLink regex for recognizing errors and their origins
		/// </summary>
		static Regex optlinkRegex = new Regex (
            @"\n(?<obj>[a-zA-Z0-9/\\.]+)\((?<module>[a-zA-Z0-9]+)\) (?<offset>[a-zA-Z0-9 ]+)?(\r)?\n Error (?<code>\d*): (?<message>[a-zA-Z0-9_ :]+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		private void HandleOptLinkOutput (BuildResult br, string linkerOutput)
		{
			var matches = optlinkRegex.Matches (linkerOutput);

			foreach (Match match in matches) {
				var error = new BuildError ();

				// Get associated D source file
				if (match.Groups ["obj"].Success) {
					var obj = Project.GetAbsoluteChildPath (new FilePath (match.Groups ["obj"].Value)).ChangeExtension (".d");

					foreach (var pf in Project.Files)
						if (pf.FilePath == obj) {
							error.FileName = pf.FilePath;
							break;
						}
				}

				error.ErrorText = "Linker error " + match.Groups ["code"].Value + " - " + match.Groups ["message"].Value;

				br.Append (error);
			}
		}

		/// <summary>
		/// Scans errorString line-wise for filename-line-message patterns (e.g. "myModule(1): Something's wrong here") and add these error locations to the CompilerResults cr.
		/// </summary>
		protected void HandleCompilerOutput (BuildResult br, string errorString)
		{
			var reader = new StringReader (errorString);
			string next;

			while ((next = reader.ReadLine()) != null) {
				var error = ErrorExtracting.FindError (next, reader);
				if (error != null) {
					if (!Path.IsPathRooted (error.FileName))
						error.FileName = Project.GetAbsoluteChildPath (error.FileName);

					br.Append (error);
				}
			}

			reader.Close ();
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
		void HandleReturnCode (BuildResult br, string executable, int returnCode)
		{
			if (returnCode != 0) {
				if (monitor != null)
					monitor.Log.WriteLine ("Exit code " + returnCode.ToString ());

				br.AddError (string.Empty, 0, 0, string.Empty,
                    GettextCatalog.GetString ("Build failed - check build output for details"));
			}
		}
        #endregion



		/// <summary>
		/// Executes a file and reports events related to the execution to the 'monitor' passed in the parameters.
		/// </summary>
		static int ExecuteCommand (
            string command,
            string args,
            string baseDirectory,

            IProgressMonitor monitor,
            out string errorOutput,
            out string programOutput)
		{
			errorOutput = string.Empty;
			int exitCode = -1;

			var swError = new StringWriter ();
			var swOutput = new StringWriter ();

			var chainedError = new LogTextWriter ();
			chainedError.ChainWriter (monitor.Log);
			chainedError.ChainWriter (swError);

			var chainedOutput = new LogTextWriter ();
			chainedOutput.ChainWriter (monitor.Log);
			chainedOutput.ChainWriter (swOutput);

			monitor.Log.WriteLine ("{0} {1}", command, args);

			var operationMonitor = new AggregatedOperationMonitor (monitor);
			var p = Runtime.ProcessService.StartProcess (command, args, baseDirectory, chainedOutput, chainedError, null);
			operationMonitor.AddOperation (p); //handles cancellation


			p.WaitForOutput ();
			errorOutput = swError.ToString ();
			programOutput = swOutput.ToString ();
			exitCode = p.ExitCode;
			p.Dispose ();

			if (monitor.IsCancelRequested) {
				monitor.Log.WriteLine (GettextCatalog.GetString ("Build cancelled"));
				monitor.ReportError (GettextCatalog.GetString ("Build cancelled"), null);
				if (exitCode == 0)
					exitCode = -1;
			}
			{
				chainedError.Close ();
				swError.Close ();
				operationMonitor.Dispose ();
			}

			return exitCode;
		}
	}
}
