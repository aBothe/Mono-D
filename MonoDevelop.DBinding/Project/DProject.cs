using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Parser;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core.Serialization;
using MonoDevelop.D.Building;
using MonoDevelop.D.Completion;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using D_Parser.Misc;
using MonoDevelop.D.Parser;

namespace MonoDevelop.D
{
	[DataInclude(typeof(DProjectConfiguration))]
	public class DProject:Project, ICustomDataItem
	{
		#region Properties
		/// <summary>
		/// Used for incremental compiling&linking
		/// </summary>
		public readonly Dictionary<ProjectFile, DateTime> LastModificationTimes = new Dictionary<ProjectFile, DateTime> ();
		public readonly List<string> BuiltObjects = new List<string> ();
		[ItemProperty("PreferOneStepBuild")]
		public bool PreferOneStepBuild = true;

		public override string ProjectType	{ get { return "Native"; } }

		public override string[] SupportedLanguages	{ get { return new[]{"D",""}; } }
		
		/// <summary>
		/// Stores parse information from project-wide includes
		/// </summary>
		public readonly ParseCache LocalIncludeCache = new ParseCache ();

		/// <summary>
		/// Stores parse information from files inside the project's base directory
		/// </summary>
		public readonly ParseCache LocalFileCache = new ParseCache ();

		public ParseCacheList ParseCache {
			get {
				return DCodeCompletionSupport.EnumAvailableModules (this);
			}
		}

		protected override void OnDefaultConfigurationChanged (ConfigurationEventArgs args)
		{
			base.OnDefaultConfigurationChanged (args);
		}
		
		[ItemProperty("UseDefaultCompiler")]
		public bool UseDefaultCompilerVendor = true;
		[ItemProperty("Compiler")]
		string _compilerVendor;

		public string UsedCompilerVendor {
			get {
				if (UseDefaultCompilerVendor)
					return DCompilerService.Instance.DefaultCompiler;
				return _compilerVendor;
			}
			set {
				_compilerVendor = value;
			}
		}

		[ItemProperty("Target")]
		public DCompileTarget CompileTarget = DCompileTarget.Executable;
		[ItemProperty("IncrementalLinking")]
		public bool EnableIncrementalLinking = true;

		/// <summary>
		/// Returns the actual compiler configuration used by this project
		/// </summary>
		public DCompilerConfiguration Compiler {
			get {
				return string.IsNullOrEmpty (UsedCompilerVendor) ? 
					DCompilerService.Instance.GetDefaultCompiler () : 
					DCompilerService.Instance.GetCompiler (UsedCompilerVendor); 
			}
			set { UsedCompilerVendor = value.Vendor; }
		}
		#endregion

		#region Parsed project modules
		public void UpdateLocalIncludeCache ()
		{
			DCompilerConfiguration.UpdateParseCacheAsync (LocalIncludeCache);
		}

		public void ReparseModule (ProjectFile pf)
		{
			if (pf == null || !DLanguageBinding.IsDFile (pf.FilePath.FileName))
				return;

			try {
				var ddom = DParser.ParseFile (pf.FilePath.ToAbsolute (BaseDirectory));

				// Update relative module name
				ddom.ModuleName = ParsedDModule.BuildModuleName (pf);

				LocalFileCache.AddOrUpdate (ddom);
			} catch (Exception ex) {
				LoggingService.LogError ("Error while parsing " + pf.FilePath.ToString (), ex);
			}
		}

		public void ReparseModule (string file)
		{
			ReparseModule (GetProjectFile (file));
		}

		/// <summary>
		/// Updates the project's parse cache and reparses all of its D sources
		/// </summary>
		public void UpdateParseCache ()
		{
			LocalFileCache.Parse (new[] { BaseDirectory.ToString () });
		}

		protected override void OnFileRemovedFromProject (ProjectFileEventArgs e)
		{
			UpdateParseCache ();

			base.OnFileRemovedFromProject (e);
		}

		protected override void OnFileRenamedInProject (ProjectFileRenamedEventArgs e)
		{
			UpdateParseCache ();

			base.OnFileRenamedInProject (e);
		}
		#endregion

		#region Init
		void Init ()
		{
		}

		public DProject ()
		{
			Init ();
		}

		public DProject (ProjectCreateInformation info, XmlElement projectOptions)
		{			
			Init ();

			string binPath = ".";
			
			if (info != null) {
				Name = info.ProjectName;

				BaseDirectory = info.ProjectBasePath;

				if (info.BinPath != null)
					binPath = info.BinPath;
			}
			
			var libs = new List<string> ();
			if (projectOptions != null) {
				foreach (XmlNode lib in projectOptions.GetElementsByTagName("Lib"))
					if (!string.IsNullOrWhiteSpace (lib.InnerText))
						libs.Add (lib.InnerText);
			}

			// Create a debug configuration
			var cfg = CreateConfiguration ("Debug") as DProjectConfiguration;
			
			cfg.ExtraLibraries.AddRange (libs);
			cfg.DebugMode = true;

			Configurations.Add (cfg);

			// Create a release configuration
			cfg = CreateConfiguration ("Release") as DProjectConfiguration;
			
			cfg.ExtraLibraries.AddRange (libs);
			cfg.DebugMode = false;

			Configurations.Add (cfg);

			// Create unittest configuration
			var unittestConfig = CreateConfiguration ("Unittest") as DProjectConfiguration;
			
			unittestConfig.ExtraLibraries.AddRange (libs);
			unittestConfig.DebugMode = true;
			unittestConfig.ExtraCompilerArguments += "-unittest ";

			Configurations.Add (unittestConfig);


			// Prepare all configurations
			foreach (DProjectConfiguration c in Configurations) {
				c.OutputDirectory = Path.Combine (this.GetRelativeChildPath (binPath), c.Id);
				c.ObjectDirectory += Path.DirectorySeparatorChar + c.Id;
				c.Output = Name;

				if (projectOptions != null) {
					// Set project's target type to the one which has been defined in the project template
					if (projectOptions.Attributes ["Target"] != null) {
						CompileTarget = (DCompileTarget)Enum.Parse (
							typeof(DCompileTarget),
							projectOptions.Attributes ["Target"].InnerText);
					}
					
					// Set project's compiler
					if (projectOptions.Attributes ["Compiler"] != null) {
						UsedCompilerVendor = projectOptions.Attributes ["Compiler"].InnerText;
					}

					// Set extra compiler&linker args
					if (projectOptions.Attributes ["CompilerArgs"].InnerText != null) {
						c.ExtraCompilerArguments += projectOptions.Attributes ["CompilerArgs"].InnerText;
					}
					if (projectOptions.Attributes ["LinkerArgs"].InnerText != null) {
						c.ExtraLinkerArguments += projectOptions.Attributes ["LinkerArgs"].InnerText;
					}

					if (projectOptions.GetAttribute ("ExternalConsole") == "True") {
						c.ExternalConsole = true;
						c.PauseConsoleOutput = true;
					}
					if (projectOptions.Attributes ["PauseConsoleOutput"] != null) {
						c.PauseConsoleOutput = bool.Parse (
							projectOptions.Attributes ["PauseConsoleOutput"].InnerText);
					}			
				}
			}
						
		}
		#endregion

		#region Build Configurations
		public override SolutionItemConfiguration CreateConfiguration (string name)
		{
			var config = new DProjectConfiguration (this) { Name=name};
			//config.Changed += new EventHandler(config_Changed);				
			
			return config;			
		}

		/*private void config_Changed(object sender, EventArgs e)
		{
			lock (LocalIncludeCache) {
				LocalIncludeCache.ParsedGlobalDictionaries.Clear();			
				DLanguageBinding.DIncludesParser.AddDirectoryRange(IncludePaths, LocalIncludeCache);
			}			
		}*/
		#endregion

		#region Building
		public override bool IsCompileable (string fileName)
		{
			return DLanguageBinding.IsDFile (fileName) || fileName.ToLower ().EndsWith (".rc");
		}

        /// <summary>
        /// Returns the absolute file name + path to the link target
        /// </summary>
		public override FilePath GetOutputFileName (ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration (configuration) as DProjectConfiguration;

            return cfg.OutputDirectory.IsAbsolute ? cfg.OutputDirectory.Combine(cfg.CompiledOutputName) : cfg.OutputDirectory.Combine(cfg.CompiledOutputName).ToAbsolute(BaseDirectory);
		}

		protected override BuildResult DoBuild (IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			// Handle pending events to ensure that files get saved right before the project is built
			DispatchService.RunPendingEvents ();

			return ProjectBuilder.CompileProject (monitor, this, configuration);
		}

		protected override bool CheckNeedsBuild (ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration (configuration) as DProjectConfiguration;
			
			if (!EnableIncrementalLinking || 
				!File.Exists (cfg.CompiledOutputName))
				return true;

			foreach (var f in Files) {
				if (f.BuildAction != BuildAction.Compile)
					continue;

				if (!File.Exists (f.LastGenOutput) || 
					!LastModificationTimes.ContainsKey (f) ||
					LastModificationTimes [f] != File.GetLastWriteTime (f.FilePath))
					return true;
			}

			return false;
		}

		protected override void DoClean (IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration (configuration) as DProjectConfiguration;

			// delete obj/res files
			monitor.BeginTask ("Delete intermediate files", Files.Count);
			foreach (var f in Files) {
				try {
					if (File.Exists (f.LastGenOutput))
						File.Delete (f.LastGenOutput);
				} catch (Exception ex) {
					monitor.ReportError ("Error while removing " + f, ex);
				} finally {
					f.LastGenOutput = string.Empty;
					monitor.Step (1);
				}
			}
			monitor.EndTask ();

			// delete target file
			monitor.BeginTask ("Delete output file", 1);

			if (File.Exists (cfg.CompiledOutputName))
				File.Delete (cfg.CompiledOutputName);

			monitor.EndTask ();

			monitor.ReportSuccess ("Cleanup successful!");
		}
		#endregion

		#region Execution
		protected override bool OnGetCanExecute (ExecutionContext context, ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration (configuration) as DProjectConfiguration;
			if (cfg == null)
				return false;
			var cmd = CreateExecutionCommand (cfg);

			return CompileTarget == DCompileTarget.Executable && context.ExecutionHandler.CanExecute (cmd);
		}

		protected virtual ExecutionCommand CreateExecutionCommand (DProjectConfiguration conf)
		{
			var app = GetOutputFileName(conf.Selector);
			var cmd = new NativeExecutionCommand (app);
			cmd.Arguments = conf.CommandLineParameters;
			cmd.WorkingDirectory = conf.OutputDirectory.ToAbsolute(BaseDirectory);
			cmd.EnvironmentVariables = conf.EnvironmentVariables;
			return cmd;
		}

		protected override void DoExecute (IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			var conf = GetConfiguration (configuration) as DProjectConfiguration;

			if (conf == null)
				return;

			bool pause = conf.PauseConsoleOutput;
			IConsole console;

			if (CompileTarget != DCompileTarget.Executable) {
				MessageService.ShowMessage ("Compile target is not an executable!");
				return;
			}

			monitor.Log.WriteLine ("Running project...");

			if (conf.ExternalConsole)
				console = context.ExternalConsoleFactory.CreateConsole (!pause);
			else
				console = context.ConsoleFactory.CreateConsole (!pause);

			var operationMonitor = new AggregatedOperationMonitor (monitor);

			try {
				var cmd = CreateExecutionCommand (conf);
				if (!context.ExecutionHandler.CanExecute (cmd)) {
					monitor.ReportError ("Cannot execute \"" + conf.Output + "\". The selected execution mode is not supported for D projects.", null);
					return;
				}

				var op = context.ExecutionHandler.Execute (cmd, console);

				operationMonitor.AddOperation (op);
				op.WaitForCompleted ();

				monitor.Log.WriteLine ("The operation exited with code: {0}", op.ExitCode);
			} catch (Exception ex) {
				monitor.ReportError ("Cannot execute \"" + conf.Output + "\"", ex);
			} finally {
				operationMonitor.Dispose ();
				console.Dispose ();
			}
		}
		#endregion

		#region Loading&Saving

		/*protected override void OnModified(SolutionItemModifiedEventArgs args)
		{
			foreach (var arg in args)
				if (arg.SolutionItem is DProject)
				{
					var dprj = arg.SolutionItem as DProject;

					// Update the directory referenced by the local cache if base directory changed
					if (arg.Hint == "BaseDirectory" && dprj != null && dprj.LocalFileCache.BaseDirectory != dprj.BaseDirectory)
					{
						dprj.LocalFileCache.BaseDirectory = dprj.BaseDirectory;
						//UpdateParseCache();
					}
				}

			base.OnModified(args);
		}*/

		protected override void OnEndLoad ()
		{
			UpdateLocalIncludeCache ();
			UpdateParseCache ();

			base.OnEndLoad ();
		}

		[ItemProperty("Includes")]
		[ItemProperty("Path", Scope = "*")]
		List<string> tempIncludes = new List<string> ();

		public void Deserialize (ITypeSerializer handler, DataCollection data)
		{
			handler.Deserialize (this, data);

			foreach (var p in tempIncludes)
				LocalIncludeCache.ParsedDirectories.Add (ProjectBuilder.EnsureCorrectPathSeparators (p));
		}

		public DataCollection Serialize (ITypeSerializer handler)
		{
			tempIncludes.Clear ();
			foreach (var p in LocalIncludeCache.ParsedDirectories)
				tempIncludes.Add (p);

			var ret = handler.Serialize (this);
			
			return ret;
		}
		#endregion
	}
}
