using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Xml;
using System.IO;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.D.Parser;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Completion;
using MonoDevelop.D.Building;

namespace MonoDevelop.D
{
	[DataInclude(typeof(DProjectConfiguration))]
	public class DProject:Project
	{
		public const string DParserPropertyKey="dom";

		#region Properties
		/// <summary>
		/// Used for incremental compiling&linking
		/// </summary>
		public readonly Dictionary<ProjectFile, DateTime> LastModificationTimes = new Dictionary<ProjectFile, DateTime>();
		public readonly List<string> BuiltObjects = new List<string>();

		public override string ProjectType	{get { return "Native"; }}
		public override string[] SupportedLanguages	{get{return new[]{"D"};}}
		
		public ASTStorage ParseCache { get; private set; }		
		
		[ItemProperty("Compiler")]
		DCompilerVendor compiler = DCompilerVendor.DMD;

		[ItemProperty("Target")]
		DCompileTarget target = DCompileTarget.Executable;

		public DCompileTarget CompileTarget {
			get { return target; }
			set { target = value; }
		}

		/// <summary>
		/// Returns the actual compiler configuration used by this project
		/// </summary>
		public DCompilerConfiguration Compiler
		{
			get { return DCompiler.GetCompiler(compiler); }
		}

		#region Parsing cache
		public IEnumerable<IAbstractSyntaxTree> ParsedModules
		{
			get
			{
				foreach (ProjectFile pf in Items)
					if (pf != null && pf.ExtendedProperties.Contains(DParserPropertyKey))
						yield return pf.ExtendedProperties[DParserPropertyKey] as IAbstractSyntaxTree;
			}
		}

		/// <summary>
		/// Updates the project's parse cache and reparses all of its D sources
		/// </summary>
		public void UpdateParseCache()
		{
			foreach (ProjectFile pf in Items)
				if (pf != null && DLanguageBinding.IsDFile(pf.FilePath.FileName))
				{
					try{
					pf.ExtendedProperties[DParserPropertyKey]=DParser.ParseFile(pf.FilePath.ToAbsolute(BaseDirectory));
					}catch(Exception ex)
					{
						LoggingService.LogError("Error while parsing "+pf.FilePath.ToString(),ex);
					}
				}
		}
		#endregion

		#endregion

		#region Init
		void Init()
		{
			ParseCache = new ASTStorage();	
		}

		public DProject() { Init(); }

		public DProject(ProjectCreateInformation info, XmlElement projectOptions)
		{			
			Init();

			string binPath = ".";
			
			if (info != null)
			{
				Name = info.ProjectName;
				binPath = info.BinPath;
			}

			// Create a debug configuration
			var cfg = CreateConfiguration("Debug") as DProjectConfiguration;

			cfg.DebugMode = true;

			Configurations.Add(cfg);

			// Create a release configuration
			cfg = CreateConfiguration("Release") as DProjectConfiguration;

			cfg.DebugMode = false;
			cfg.ExtraCompilerArguments = "-o";

			Configurations.Add(cfg);
			
			// Prepare all configurations
			foreach (DProjectConfiguration c in Configurations)
			{
				c.OutputDirectory = Path.Combine(binPath, c.Id);
				c.SourcePath = info.ProjectBasePath;
				c.Output = Name;

				if (projectOptions != null)
				{
					// Set project's target type to the one which has been defined in the project template
					if (projectOptions.Attributes["Target"] != null)
					{
						c.CompileTarget = (DCompileTarget)Enum.Parse(
							typeof(DCompileTarget),
							projectOptions.Attributes["Target"].InnerText);
					}
					
					// Set project's compiler
					if (projectOptions.Attributes["Compiler"] != null)
					{
						c.Compiler = (DCompilerVendor)Enum.Parse(
							typeof(DCompilerVendor), 
							projectOptions.Attributes["Compiler"].InnerText);
					}

					// Set extra compiler&linker args
					if (projectOptions.Attributes["CompilerArgs"].InnerText != null)
					{
						c.ExtraCompilerArguments = projectOptions.Attributes["CompilerArgs"].InnerText;
					}
					if (projectOptions.Attributes["LinkerArgs"].InnerText != null)
					{
						c.ExtraLinkerArguments = projectOptions.Attributes["LinkerArgs"].InnerText;
					}

					if (projectOptions.GetAttribute("ExternalConsole") == "True")
					{
						c.ExternalConsole = true;
						c.PauseConsoleOutput = true;
					}
					if (projectOptions.Attributes["PauseConsoleOutput"] != null)
					{
						c.PauseConsoleOutput = bool.Parse(
							projectOptions.Attributes["PauseConsoleOutput"].InnerText);
					}			
				}
			}


						
		}
		#endregion

		public override SolutionItemConfiguration CreateConfiguration(string name)
		{
			DProjectConfiguration config = new DProjectConfiguration() { Name=name};
			config.Changed += new EventHandler(config_Changed);				
			
			return config;			
		}

		private void config_Changed(object sender, EventArgs e)
		{
			List<string> includepaths = new List<string>();	
			foreach (DProjectConfiguration c in Configurations)					
				foreach(string dir in c.Includes)
						includepaths.Add(dir);					
			
			lock (ParseCache) {
				ParseCache.ParsedGlobalDictionaries.Clear();			
				DLanguageBinding.DIncludesParser.AddDirectoryRange(includepaths, ParseCache);
			}			
		}


		#region Building
		public override bool IsCompileable(string fileName)
		{
			return DLanguageBinding.IsDFile(fileName);
		}

		public override FilePath GetOutputFileName(ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;
			return cfg.OutputDirectory.Combine(cfg.CompiledOutputName);
		}

		protected override BuildResult DoBuild(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;
			cfg.SourcePath = BaseDirectory;

			return DCompiler.Compile(this,Files,cfg,monitor);
		}

		protected override void DoClean(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			
		}

		protected override void OnEndLoad ()
		{
			base.OnEndLoad();
			
			List<string> includepaths = new List<string>();	
			foreach (DProjectConfiguration c in Configurations)
				foreach(string dir in c.Includes)
						includepaths.Add(dir);
			
			lock (ParseCache) {
				ParseCache.ParsedGlobalDictionaries.Clear();
				DLanguageBinding.DIncludesParser.AddDirectoryRange(includepaths, ParseCache);
			}
		}
		#endregion

		#region Execution
		protected override bool OnGetCanExecute(ExecutionContext context, ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;
			if (cfg == null)
				return false;
			var cmd = CreateExecutionCommand(cfg);

			return CompileTarget == DCompileTarget.Executable && context.ExecutionHandler.CanExecute(cmd);
		}

		protected virtual ExecutionCommand CreateExecutionCommand(DProjectConfiguration conf)
		{
			var app = Path.Combine(conf.OutputDirectory, conf.Output);
			var cmd = new NativeExecutionCommand(app);
			cmd.Arguments = conf.CommandLineParameters;
			cmd.WorkingDirectory = Path.GetFullPath(conf.OutputDirectory);
			cmd.EnvironmentVariables = conf.EnvironmentVariables;
			return cmd;
		}

		protected override void DoExecute(IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			var conf = GetConfiguration(configuration) as DProjectConfiguration;

			if (conf == null)
				return;

			bool pause = conf.PauseConsoleOutput;
			IConsole console;

			if (conf.CompileTarget != DCompileTarget.Executable)
			{
				MessageService.ShowMessage("Compile target is not an executable!");
				return;
			}

			monitor.Log.WriteLine("Running project...");

			if (conf.ExternalConsole)
				console = context.ExternalConsoleFactory.CreateConsole(!pause);
			else
				console = context.ConsoleFactory.CreateConsole(!pause);

			var operationMonitor = new AggregatedOperationMonitor(monitor);

			try
			{
				var cmd = CreateExecutionCommand(conf);
				if (!context.ExecutionHandler.CanExecute(cmd))
				{
					monitor.ReportError("Cannot execute \"" + conf.Output + "\". The selected execution mode is not supported for D projects.", null);
					return;
				}

				var op = context.ExecutionHandler.Execute(cmd, console);

				operationMonitor.AddOperation(op);
				op.WaitForCompleted();

				monitor.Log.WriteLine("The operation exited with code: {0}", op.ExitCode);
			}
			catch (Exception ex)
			{
				monitor.ReportError("Cannot execute \"" + conf.Output + "\"", ex);
			}
			finally
			{
				operationMonitor.Dispose();
				console.Dispose();
			}
		}
		#endregion
	}
}
