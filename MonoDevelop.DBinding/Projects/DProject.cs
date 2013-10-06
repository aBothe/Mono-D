﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
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
using D_Parser.Resolver;
using MonoDevelop.D.Profiler;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.D.Profiler.Commands;
using MonoDevelop.D.Resolver;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace MonoDevelop.D.Projects
{
	[DataInclude(typeof(DProjectConfiguration))]
	public class DProject : AbstractDProject, ICustomDataItem
	{
		#region Properties
		/// <summary>
		/// Used for incremental compiling and linking
		/// </summary>
		public readonly Dictionary<ProjectFile, DateTime> LastModificationTimes = new Dictionary<ProjectFile, DateTime> ();
		public readonly List<string> BuiltObjects = new List<string> ();
		[ItemProperty("PreferOneStepBuild")]
		public bool PreferOneStepBuild = true;

		public const string ConfigJson = "projectconfig.json";
		public ExtendedProjectConfig ExtendedConfiguration;

		public override IEnumerable<SolutionItem> GetReferencedItems(ConfigurationSelector configuration)
		{
			SolutionItem p;
			foreach (var dep in References.ReferencedProjectIds)
				if ((p = ParentSolution.GetSolutionItem(dep)) != null)
					yield return p;
		}

		public IEnumerable<Project> DependingProjects
		{
			get {
				Project p;
				foreach (var dep in References.ReferencedProjectIds)
					if((p=ParentSolution.GetSolutionItem(dep) as Project) != null)
						yield return p;
			}
		}
		
		[ItemProperty("UseDefaultCompiler")]
		public bool UseDefaultCompilerVendor = true;
		string _compilerVendor;

		[ItemProperty("Compiler")]
		public string UsedCompilerVendor {
			get {
				if (UseDefaultCompilerVendor)
					return DCompilerService.Instance.DefaultCompiler;
				return _compilerVendor;
			}
			set {
				var c = Compiler;
				if (c != null)
					c.FinishedParsing -= compilerCacheUpdated;

				_compilerVendor = value;

				c = Compiler;
				if (c != null) {
					c.FinishedParsing += compilerCacheUpdated;
					if (c.HadInitialParse)
						compilerCacheUpdated (null);
				}
			}
		}

		[ItemProperty("IncrementalLinking")]
		public bool EnableIncrementalLinking = true;

		/// <summary>
		/// Returns the actual compiler configuration used by this project
		/// </summary>
		public DCompilerConfiguration Compiler
		{
			get {
				return DCompilerService.Instance.GetCompiler (UsedCompilerVendor); 
			}
			set { UsedCompilerVendor = value.Vendor; }
		}

		public override IEnumerable<string> GlobalIncludes {
			get {
				return Compiler.IncludePaths;
			}
		}

		readonly DefaultDReferencesCollection referenceCollection;
		public override DProjectReferenceCollection References {
			get {
				return referenceCollection;
			}
		}

		public override string ToString ()
		{
			return string.Format ("[DProject: Name={0}]", Name);
		}
		#endregion

		#region Init
		public DProject (){
			referenceCollection = new DefaultDReferencesCollection (this);
			Init ();
		}

		public DProject (ProjectCreateInformation info, XmlElement projectOptions)
		{
			referenceCollection = new DefaultDReferencesCollection (this);
			Init ();

			string binPath = ".";
			
			if (info != null) {
				Name = info.ProjectName;

				BaseDirectory = info.ProjectBasePath;

				if (info.BinPath != null)
					binPath = info.BinPath;
			}

			var compTarget = DCompileTarget.Executable;

            var outputPrefix = "";

            if (projectOptions != null)
            {
                // Set project's target type to the one which has been defined in the project template
                if (projectOptions.Attributes["Target"] != null)
                    compTarget = (DCompileTarget)Enum.Parse(
                        typeof(DCompileTarget),
                        projectOptions.Attributes["Target"].InnerText);

                // Set project's compiler
                if (projectOptions.Attributes["Compiler"] != null)
                    UsedCompilerVendor = projectOptions.Attributes["Compiler"].InnerText;

                // Non Windows-OS require a 'lib' prefix as library name -- like libphobos2.a
                if (compTarget == DCompileTarget.StaticLibrary || compTarget == DCompileTarget.SharedLibrary && !OS.IsWindows)
                {
                    outputPrefix = "lib";
                }
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
			unittestConfig.UnittestMode = true;

			Configurations.Add (unittestConfig);
            
			// Prepare all configurations
			foreach (DProjectConfiguration c in Configurations) {

				c.ExternalConsole = true;
				c.CompileTarget = compTarget;
				c.OutputDirectory = Path.Combine (this.GetRelativeChildPath (binPath), c.Id);
				c.ObjectDirectory += Path.DirectorySeparatorChar + c.Id;
				c.Output = outputPrefix + Name;

				c.UpdateGlobalVersionIdentifiers();

				if (projectOptions != null) {
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

		void Init()
		{
			var c = Compiler;
			if (c != null) {
				c.FinishedParsing += compilerCacheUpdated;
				if (c.HadInitialParse)
					compilerCacheUpdated (null);
			}
		}
		#endregion

		#region Build Configurations
		protected override void PopulateSupportFileList(FileCopySet list, ConfigurationSelector configuration)
		{
			base.PopulateSupportFileList(list, configuration);
			
			// Automatically copy referenced dll/so's to the target dir
			DProjectConfiguration prjCfg;
			foreach(var prj in DependingProjects)
				if((prjCfg=prj.GetConfiguration(configuration) as DProjectConfiguration) != null &&
				  prjCfg.CompileTarget == DCompileTarget.SharedLibrary)
			{
				list.Add(prj.GetOutputFileName(configuration));
			}
		}
		
		public override SolutionItemConfiguration CreateConfiguration (string name)
		{
			var config = new DProjectConfiguration() { Name=name};
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

		static List<string> alreadyBuiltProjects = new List<string>();
		protected override BuildResult DoBuild (IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			// Handle pending events to ensure that files get saved right before the project is built
			DispatchService.RunPendingEvents ();
			
			// Build projects this project is depending on
			if(alreadyBuiltProjects.Contains(ItemId))
				return new BuildResult() { FailedBuildCount = 1, CompilerOutput="Circular dependency detected!" };
			
			alreadyBuiltProjects.Add(ItemId);
			try{
				BuildResult bs;
				foreach(var prj in DependingProjects)
					if(prj.NeedsBuilding(configuration))
						if((bs=prj.Build(monitor, configuration)).Failed)
							return bs ?? new BuildResult{ FailedBuildCount = 1};
			}finally{
				alreadyBuiltProjects.Remove(ItemId);
			}
			return ProjectBuilder.CompileProject (monitor, this, configuration);
		}

		protected override bool CheckNeedsBuild (ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration (configuration) as DProjectConfiguration;
			
			if (!EnableIncrementalLinking || 
				!File.Exists (GetOutputFileName(configuration)))
				return true;

			foreach (var f in Files) {
				if (f.BuildAction != BuildAction.Compile) //TODO: What if one file changed its properties?
					continue;

				if (!File.Exists (f.LastGenOutput) || 
					!LastModificationTimes.ContainsKey (f) ||
					LastModificationTimes [f] != File.GetLastWriteTime (f.FilePath))
					return true;
			}
			
			//TODO: What if compilation parameters changed? / How to detect this?

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

			return cfg.CompileTarget == DCompileTarget.Executable && context.ExecutionHandler.CanExecute (cmd);
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
			bool executeCustomCommand = 
				ExtendedConfiguration != null && 
				!string.IsNullOrWhiteSpace(ExtendedConfiguration.RunCommand);

			var conf = GetConfiguration (configuration) as DProjectConfiguration;

			if (conf == null)
				return;

			if (conf.CompileTarget != DCompileTarget.Executable || executeCustomCommand) {
				MessageService.ShowMessage ("Compile target is not an executable!");
				return;
			}

			bool pause = conf.PauseConsoleOutput;
			IConsole console;

			if (conf.ExternalConsole)
				console = context.ExternalConsoleFactory.CreateConsole (!pause);
			else
				console = context.ConsoleFactory.CreateConsole (!pause);

			monitor.Log.WriteLine("Running project...");

			

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
			
			if(ProfilerModeHandler.IsProfilerMode && Compiler.HasProfilerSupport)
				IdeApp.CommandService.DispatchCommand( "MonoDevelop.D.Profiler.Commands.ProfilerCommands.AnalyseTaceLog");
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

		/// <summary>
		/// List of GUIDs that identify project items within their solution.
		/// Used to store project dependencies.
		/// </summary>
		[ItemProperty("DependentProjectIds")]
		List<string> tempProjectDependencies = new List<string>();

		[ItemProperty("Includes")]
		[ItemProperty("Path", Scope = "*")]
		List<string> tempIncludes = new List<string> ();

		public void Deserialize (ITypeSerializer handler, DataCollection data)
		{
			handler.Deserialize (this, data);

			Init ();

			referenceCollection.InitRefCollection (tempProjectDependencies, tempIncludes);
		}

		public DataCollection Serialize (ITypeSerializer handler)
		{
			tempIncludes.Clear ();
			tempProjectDependencies.Clear ();

			tempIncludes.AddRange (referenceCollection.RawIncludes);
			tempProjectDependencies.AddRange (referenceCollection.ProjectDependencies);

			var ret = handler.Serialize (this);
			
			return ret;
		}

		protected override void OnSaved(SolutionItemEventArgs args)
		{
			base.OnSaved(args);

			if (ExtendedConfiguration != null)
			{
				try
				{
					var json = JsonConvert.SerializeObject(ExtendedConfiguration, Newtonsoft.Json.Formatting.Indented);
					File.WriteAllText(BaseDirectory.Combine(ConfigJson), json);
				}
				catch 
				{ 
				}
			}
		}

		protected override void OnEndLoad()
		{
			var configJson = BaseDirectory.Combine(ConfigJson);
			if (File.Exists(configJson))
			{
				try
				{
					var contents = File.ReadAllText(configJson);
					ExtendedConfiguration = JsonConvert.DeserializeObject<ExtendedProjectConfig>(contents);
				}
				catch 
				{
				}
			}

			base.OnEndLoad();
		}

		void compilerCacheUpdated(ParsingFinishedEventArgs ea)
		{
			base.InformGlobalParseCacheFilled ();
		}
		#endregion
	}
}
