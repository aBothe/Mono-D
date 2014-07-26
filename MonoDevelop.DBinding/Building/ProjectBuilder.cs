using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.D.Profiler.Commands;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Building
{
	public class ProjectBuilder
	{
        #region Properties
		public DCompilerConfiguration Compiler {
			get { return Project.Compiler; }
		}

		public static DCompileTarget BuildTargetType(DProjectConfiguration BuildConfig) { return BuildConfig.UnittestMode ? DCompileTarget.Executable : BuildConfig.CompileTarget; }

		public static LinkTargetConfiguration LinkTargetCfg(DProjectConfiguration cfg) { return cfg.Project.Compiler.GetOrCreateTargetConfiguration (BuildTargetType(cfg)); }

		static BuildConfiguration BuildArguments(DProjectConfiguration cfg) { return LinkTargetCfg(cfg).GetArguments (cfg.DebugMode); }

		public static ConfigurationSelector BuildingConfigurationSelector { get; private set; }


		DProject Project;
		DProjectConfiguration BuildConfig;

		static string ObjectDirectory(DProjectConfiguration cfg)
		{
			return EnsureCorrectPathSeparators(cfg.ObjectDirectory);
		}

		static string AbsoluteObjectDirectory(DProjectConfiguration cfg){
			if (Path.IsPathRooted (cfg.ObjectDirectory))
				return ObjectDirectory(cfg);
			else
				return Path.Combine (cfg.Project.BaseDirectory, ObjectDirectory(cfg));
		}
		IArgumentMacroProvider commonMacros;
		readonly IProgressMonitor monitor;
		List<string> BuiltObjects = new List<string> ();

		/// <summary>
		/// In this list, all directories of files that are 'linked' to the project are put in.
		/// Used for multiple-step building only.
		/// </summary>
		List<string> FileLinkDirectories = new List<string>();

		public bool CanDoOneStepBuild {
			get {
				return Project.PreferOneStepBuild && BuildArguments(BuildConfig).SupportsOneStepBuild;
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
			if (BuildConfig.Project != Project)
				throw new InvalidOperationException ("Wrong project configuration");
			commonMacros = new PrjPathMacroProvider {
				slnPath = Project.ParentSolution != null ? EnsureCorrectPathSeparators(Project.ParentSolution.BaseDirectory) : ""
			};
			BuiltObjects.Clear ();

			if (Compiler == null) {
				var targetBuildResult = new BuildResult ();

				targetBuildResult.AddError ("Project compiler \"" + Project.UsedCompilerVendor + "\" not found");
				targetBuildResult.FailedBuildCount++;

				return targetBuildResult;
			}

			var absDir = AbsoluteObjectDirectory (BuildConfig);
			if (!Directory.Exists (absDir))
				Directory.CreateDirectory (absDir);

			if (CanDoOneStepBuild)
				return DoOneStepBuild ();
			else
				return DoStepByStepBuild ();
		}

		public static string BuildOneStepBuildString(DProject prj, IEnumerable<string> builtObjects, ConfigurationSelector sel)
		{
			BuildingConfigurationSelector = sel;
			var cfg = prj.GetConfiguration (sel) as DProjectConfiguration;
			var target = prj.GetOutputFileName (sel);

			var rawArgumentString = new StringBuilder();
			var s = GenAdditionalAttributes (prj.Compiler, cfg);
			if(!string.IsNullOrWhiteSpace(s) )
				rawArgumentString.Append(s.Trim()).Append(' ');
			rawArgumentString.Append(BuildArguments(cfg).OneStepBuildArguments.Trim());
			if(!string.IsNullOrEmpty(cfg.ExtraCompilerArguments))
				rawArgumentString.Append(' ').Append(cfg.ExtraCompilerArguments.Trim());
			if (!string.IsNullOrEmpty(cfg.ExtraLinkerArguments))
				rawArgumentString.Append(' ').Append(PrefixedExtraLinkerFlags(cfg));

			var commonMacros = new PrjPathMacroProvider {
				slnPath = prj.ParentSolution != null ? EnsureCorrectPathSeparators(prj.ParentSolution.BaseDirectory) : ""
			};

			return FillInMacros(rawArgumentString.ToString(),
				new OneStepBuildArgumentMacroProvider
				{
					ObjectsStringPattern = prj.Compiler.ArgumentPatterns.ObjectFileLinkPattern,
					IncludesStringPattern = prj.Compiler.ArgumentPatterns.IncludePathPattern,

					SourceFiles = builtObjects,
					Includes = FillInMacros(prj.IncludePaths,commonMacros),
					Libraries = GetLibraries(cfg, prj.Compiler),

					RelativeTargetDirectory = cfg.OutputDirectory.ToRelative(prj.BaseDirectory),
					ObjectsDirectory = ObjectDirectory(cfg),
					TargetFile = target,
				}, commonMacros);

			BuildingConfigurationSelector = null;
		}

		BuildResult DoOneStepBuild ()
		{
			var br = new BuildResult ();

			bool filesModified = false;

			// Enum files & build resource files
			foreach (var pf in Project.Files) {
				if (pf.BuildAction != BuildAction.Compile || pf.Subtype == Subtype.Directory)
					continue;

				DateTime dt;
				if (Project.LastModificationTimes.TryGetValue(pf, out dt))
					filesModified |= File.GetLastWriteTime(pf.FilePath) != dt;
				else
					filesModified = true;
				Project.LastModificationTimes[pf] = File.GetLastWriteTime(pf.FilePath);

				if (pf.FilePath.Extension.EndsWith (".rc", StringComparison.OrdinalIgnoreCase)) {
					if (!CompileResourceScript (br, pf))
						return br;
				} else
					BuiltObjects.Add (MakeRelativeToPrjBase(Project,pf.FilePath));
			}

			// Build argument string
			var target = Project.GetOutputFileName (BuildConfig.Selector);

			if (!Project.NeedsFullRebuild && !filesModified && Project.EnableIncrementalLinking &&
				File.Exists(target))
			{
				monitor.ReportSuccess("Build successful! - No new linkage needed");
				monitor.Step(1);
				return br;
			}

			var argumentString = BuildOneStepBuildString (Project, BuiltObjects, BuildConfig.Selector);


			// Execute the compiler
			var stdOut = "";
			var stdError = "";
			var linkCfg = LinkTargetCfg (BuildConfig);

			var linkerExecutable = Compiler.SourceCompilerCommand;
			if (!Path.IsPathRooted(linkerExecutable) && !string.IsNullOrEmpty(Compiler.BinPath))
			{
				linkerExecutable = Path.Combine(Compiler.BinPath, linkCfg.Linker);

				if (!File.Exists(linkerExecutable))
					linkerExecutable = linkCfg.Linker;
			}

			monitor.Log.WriteLine("Current dictionary: " + Project.BaseDirectory);

			string cmdLineFile;
			HandleOverLongArgumentStrings (monitor,Compiler, true, ref argumentString, out cmdLineFile);

			int exitCode = ExecuteCommand(linkerExecutable, argumentString, Project.BaseDirectory, monitor,
				out stdError,
				out stdOut);

			ErrorExtracting.HandleCompilerOutput(Project,br, stdError);
			ErrorExtracting.HandleCompilerOutput(Project,br, stdOut);
			ErrorExtracting.HandleOptLinkOutput(Project, br, stdOut);
			ErrorExtracting.HandleReturnCode(monitor, br, exitCode);

			if (cmdLineFile != null)
				File.Delete (cmdLineFile);

			if (!br.Failed) {
				Project.CopySupportFiles (monitor, this.BuildConfig.Selector);
				Project.NeedsFullRebuild = false;
			}

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
				if (!Project.NeedsFullRebuild &&
					Project.EnableIncrementalLinking &&
                    !string.IsNullOrEmpty (f.LastGenOutput) && 
                    File.Exists (Path.IsPathRooted(f.LastGenOutput) ? f.LastGenOutput : Project.BaseDirectory.Combine(f.LastGenOutput).ToString()) &&
                    Project.LastModificationTimes.ContainsKey (f) &&
                    Project.LastModificationTimes [f] == File.GetLastWriteTime (f.FilePath)) {
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
				LinkToTarget (br, Project.NeedsFullRebuild || !Project.EnableIncrementalLinking || modificationsDone);

			if (!br.Failed) {
				Project.CopySupportFiles (monitor, this.BuildConfig.Selector);
				Project.NeedsFullRebuild = false;
			}

			monitor.EndTask ();

			return br;
		}

		bool CompileSource (BuildResult targetBuildResult, ProjectFile f)
		{
			if (File.Exists (f.LastGenOutput))
				File.Delete (f.LastGenOutput);

			var obj = GetRelativeObjectFileName (ObjectDirectory(BuildConfig),f, DCompilerService.ObjectExtension);
			var buildArgs = BuildArguments (BuildConfig);

			// Create argument string for source file compilation.
			var dmdArgs = FillInMacros((AdditionalCompilerAttributes ?? string.Empty) + " " +
				buildArgs.CompilerArguments.Trim() + " " + (BuildConfig.ExtraCompilerArguments ?? ""),
			new DCompilerMacroProvider
            {
                IncludePathConcatPattern = Compiler.ArgumentPatterns.IncludePathPattern,
                SourceFile = f.FilePath.ToRelative(Project.BaseDirectory),
                ObjectFile = obj,
				Includes = FillCommonMacros(Project.IncludePaths).Union(FileLinkDirectories),
				},commonMacros).Trim();

			// b.Execute compiler
			string stdError;
			string stdOutput;

			var compilerExecutable = Compiler.SourceCompilerCommand;
			if (!Path.IsPathRooted (compilerExecutable) && !string.IsNullOrEmpty(Compiler.BinPath)) {
				compilerExecutable = Path.Combine (Compiler.BinPath, Compiler.SourceCompilerCommand);

				if (!File.Exists (compilerExecutable))
					compilerExecutable = Compiler.SourceCompilerCommand;
			}

			string cmdArgFile;
			HandleOverLongArgumentStrings (monitor,Compiler, false, ref dmdArgs, out cmdArgFile);

			int exitCode = ExecuteCommand (compilerExecutable, dmdArgs, Project.BaseDirectory, monitor, out stdError, out stdOutput);

			ErrorExtracting.HandleCompilerOutput(Project,targetBuildResult, stdError);
			ErrorExtracting.HandleCompilerOutput(Project,targetBuildResult, stdOutput);
			ErrorExtracting.HandleReturnCode (monitor,targetBuildResult, exitCode);

			if (exitCode != 0) {
				targetBuildResult.FailedBuildCount++;
				return false;
			} else {
				if (cmdArgFile != null)
					File.Delete (cmdArgFile);

				f.LastGenOutput = obj;

				targetBuildResult.BuildCount++;
				Project.LastModificationTimes [f] = File.GetLastWriteTime (f.FilePath);

				BuiltObjects.Add (obj);
				return true;
			}
		}

		bool CompileResourceScript (BuildResult targetBuildResult, ProjectFile f)
		{
			var res = GetRelativeObjectFileName (ObjectDirectory(BuildConfig), f, ".res");

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

			ErrorExtracting.HandleReturnCode (monitor,targetBuildResult, _exitCode);

			if (_exitCode != 0) {
				targetBuildResult.FailedBuildCount++;
				return false;
			} else {
				f.LastGenOutput = res;

				targetBuildResult.BuildCount++;
				Project.LastModificationTimes [f] = File.GetLastWriteTime (f.FilePath);

				BuiltObjects.Add (MakeRelativeToPrjBase (Project,res));
				return true;
			}
		}

		void LinkToTarget (BuildResult br, bool modificationsDone)
		{
			// The target file to which all objects will be linked to
			var LinkTargetFile = Project.GetOutputFileName (BuildConfig.Selector);

			if (!modificationsDone &&
                File.Exists (LinkTargetFile)) {
				monitor.ReportSuccess ("Build successful! - No new linkage needed");
				monitor.Step (1);
				return;
			}

			// b.Build linker argument string
			// Build argument preparation
			var linkArgs = FillInMacros (BuildArguments(BuildConfig).LinkerArguments.Trim() + 
			                             (string.IsNullOrEmpty(BuildConfig.ExtraLinkerArguments) ? string.Empty : (" " + BuildConfig.ExtraLinkerArguments.Trim())),
                new DLinkerMacroProvider
                {
                    ObjectsStringPattern = Compiler.ArgumentPatterns.ObjectFileLinkPattern,
                    Objects = BuiltObjects.ToArray (),
                    TargetFile = LinkTargetFile,
                    RelativeTargetDirectory = BuildConfig.OutputDirectory.ToRelative (Project.BaseDirectory),
                    Libraries = GetLibraries(BuildConfig, Compiler)
                },commonMacros);

			var linkerOutput = "";
			var linkerErrorOutput = "";
			var linkCfg = LinkTargetCfg (BuildConfig);

			var linkerExecutable = linkCfg.Linker;
			if (!Path.IsPathRooted (linkerExecutable) && !string.IsNullOrEmpty(Compiler.BinPath)) {
				linkerExecutable = Path.Combine (Compiler.BinPath, linkCfg.Linker);

				if (!File.Exists (linkerExecutable))
					linkerExecutable = linkCfg.Linker;
			}

			string cmdLineFile;
			HandleOverLongArgumentStrings (monitor,Compiler, true, ref linkArgs, out cmdLineFile);

			int exitCode = ExecuteCommand (linkerExecutable, linkArgs, Project.BaseDirectory, monitor,
                out linkerErrorOutput,
                out linkerOutput);

			ErrorExtracting.HandleOptLinkOutput (Project,br, linkerOutput);
			ErrorExtracting.HandleReturnCode(monitor,br, exitCode);

			if (cmdLineFile != null && !br.Failed)
				File.Delete (cmdLineFile);
		}

        #region File naming
		public static string EnsureCorrectPathSeparators (string file)
		{
			if (OS.IsWindows)
				return file.Replace ('/', '\\');
			else
				return file.Replace ('\\', '/');
		}

		public static string MakeRelativeToPrjBase(Project prj,string obj)
		{
			var baseDirectory = prj.BaseDirectory.ToString();
			return obj.StartsWith(baseDirectory) ? obj.Substring(baseDirectory.Length + 1) : obj;
		}

		public static string GetRelativeObjectFileName (string objDirectory,ProjectFile f, string extension)
		{
			return Path.Combine(EnsureCorrectPathSeparators(objDirectory),
			                    f.ProjectVirtualPath.ChangeExtension(extension)
			                    	.ToString()
			                    	.Replace(Path.DirectorySeparatorChar,'.'));
		}
        #endregion

        #region Build argument creation

		static string PrefixedExtraLinkerFlags(DProjectConfiguration BuildConfig)
		{
				var linkerRedirectPrefix = BuildConfig.Project.Compiler.ArgumentPatterns.LinkerRedirectPrefix;
				if (string.IsNullOrWhiteSpace(BuildConfig.ExtraLinkerArguments))
					return string.Empty;

				var sb = new StringBuilder(BuildConfig.ExtraLinkerArguments);
				int lastArgStart = -1;
				bool isInString = false;
				for (int i = 0; i < sb.Length; i++)
				{
					switch(sb[i])
					{
						case '\t':
						case ' ':
							if(isInString)
								continue;
							lastArgStart = -1;
							break;
						case '"':
							isInString = !isInString;
							goto default;
						default:
							if (lastArgStart == -1)
							{
								lastArgStart = i;
								sb.Insert(i, linkerRedirectPrefix);
								i += linkerRedirectPrefix.Length;
							}
							break;
					}
				}
				return sb.ToString();
		}

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

		public static IEnumerable<string> GetLibraries(DProjectConfiguration projCfg, DCompilerConfiguration compiler)
		{
			IEnumerable<string> libraries = FillInMacros (projCfg.GetReferencedLibraries (projCfg.Selector), new PrjPathMacroProvider {
				slnPath = projCfg.Project.ParentSolution != null ? projCfg.Project.ParentSolution.BaseDirectory.ToString () : ""
			});

			if (compiler.EnableGDCLibPrefixing)
				libraries = HandleGdcSpecificLibraryReferencing(libraries, projCfg.Project.BaseDirectory);

			return libraries;
		}

		string AdditionalCompilerAttributes
		{
			get
			{
				return GenAdditionalAttributes(Compiler,BuildConfig);
			}
		}

		public static string GenAdditionalAttributes (DCompilerConfiguration compiler, DProjectConfiguration cfg)
		{
			var sb = new StringBuilder ();
			var p = compiler.ArgumentPatterns;

			string s = cfg.Platform;
			if (s != null && s.Contains('|'))
				s = s.Substring(0, s.IndexOf('|'));
			if (cfg.Platform != null && compiler.ArgumentPatterns.PlatformDependentOptions.TryGetValue(s, out s))
				sb.Append(s).Append(' ');

			if (cfg.UnittestMode)
				sb.Append (p.UnittestFlag).Append(' ');
				
			if(ProfilerModeHandler.IsProfilerMode && compiler.HasProfilerSupport)
				sb.Append (p.ProfileFlag).Append(" -v ");

			if (cfg.CustomDebugIdentifiers != null && cfg.CustomVersionIdentifiers.Length != 0)
				foreach (var id in cfg.CustomDebugIdentifiers)
					sb.Append (p.DebugDefinition + "=" + id + " ");

			if (cfg.DebugLevel > 0)
				sb.Append (p.DebugDefinition).Append('=').Append(cfg.DebugLevel).Append(' ');

			if (cfg.DebugMode)
				sb.Append (p.DebugDefinition).Append(' ');

			if (cfg.CustomVersionIdentifiers != null && cfg.CustomVersionIdentifiers.Length != 0)
				foreach (var id in cfg.CustomVersionIdentifiers)
					sb.Append (p.VersionDefinition).Append('=').Append(id).Append(' ');

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
			}

			return sb.ToString().Trim();
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

		static void HandleOverLongArgumentStrings(IProgressMonitor mon,DCompilerConfiguration cmp, bool isLinking,ref string argstring, out string tempFile)
		{
			tempFile = null;

			if (argstring.Length < 1024)
				return;

			if (isLinking && !cmp.ArgumentPatterns.CommandFileCanBeUsedForLinking)
				return;

			var cmdFile = cmp.ArgumentPatterns.CommandFile;

			if (string.IsNullOrWhiteSpace (cmdFile))
				return;

			tempFile = Path.GetTempFileName ();
			File.WriteAllText (tempFile, argstring);

			mon.Log.WriteLine("Contents of \"{0}\":", tempFile);
			mon.Log.WriteLine(argstring);
			mon.Log.Flush();

			argstring = string.Format (cmdFile, tempFile);
		}

        #endregion

		#region Command execution lowlevel
		/// <summary>
		/// Executes a file and reports events related to the execution to the 'monitor' passed in the parameters.
		/// </summary>
		public static int ExecuteCommand (
            string command,
            string args,
            string baseDirectory,

            IProgressMonitor monitor,
            out string errorOutput,
            out string programOutput)
		{
			bool useMonitor = monitor != null;
			errorOutput = string.Empty;
			int exitCode = -1;

			var swError = new StringWriter ();
			var swOutput = new StringWriter ();

			var chainedError = new LogTextWriter ();
			chainedError.ChainWriter (swError);

			var chainedOutput = new LogTextWriter ();
			chainedOutput.ChainWriter (swOutput);

			if (useMonitor) {
				chainedError.ChainWriter (monitor.Log);
				chainedOutput.ChainWriter (monitor.Log);
				monitor.Log.WriteLine ("{0} {1}", command, args);
			}

			// Workaround for not handling %PATH% env var expansion properly
			if (!command.Contains(Path.DirectorySeparatorChar))
				command = TryExpandPathEnvVariable(command, baseDirectory);

			var operationMonitor = useMonitor ? new AggregatedOperationMonitor (monitor) : null;
			var p = Runtime.ProcessService.StartProcess (command, args, baseDirectory, chainedOutput, chainedError, null);
			if(useMonitor)
				operationMonitor.AddOperation (p); //handles cancellation

			p.WaitForOutput ();
			swError.Flush ();
			swOutput.Flush ();
			errorOutput = swError.ToString ();
			programOutput = swOutput.ToString ();
			exitCode = p.ExitCode;
			p.Dispose ();

			if (useMonitor) {
				if (monitor.IsCancelRequested) {
					monitor.Log.WriteLine (GettextCatalog.GetString ("Build cancelled"));
					monitor.ReportError (GettextCatalog.GetString ("Build cancelled"), null);
					if (exitCode == 0)
						exitCode = -1;
				}

				operationMonitor.Dispose ();
			}

			chainedError.Close ();
			swOutput.Close ();
			swError.Close ();

			return exitCode;
		}

		public static string TryExpandPathEnvVariable(string command, string baseDirectory = null)
		{
			string origCommand;
			string tmp;

			if (OS.IsWindows)
			{
				// TODO: Perhaps take .cmd and other executable file extensions into it?
				origCommand = command;
				command = Path.ChangeExtension(command, ".exe");
			}
			else
				origCommand = null;

			if (baseDirectory != null && File.Exists(tmp = Path.Combine(baseDirectory, command)))
				return tmp;
			else
			{
				var PATH = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ??
				           Environment.GetEnvironmentVariable("PATH");
				if (PATH != null) {
					string[] PATHS;
					if (OS.IsWindows)
						PATHS = PATH.Split (';');
					else
						PATHS = PATH.Split (':');

					foreach (var path in PATHS)
						if (File.Exists (tmp = Path.Combine (path, command)))
							return tmp;
				}
			}

			return origCommand ?? command;
		}
		#endregion
	}
}
