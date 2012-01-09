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
using MonoDevelop.D.Completion;

namespace MonoDevelop.D
{
	[DataInclude(typeof(DProjectConfiguration))]
	public class DProject:Project, ICustomDataItem
	{
		/// <summary>
		/// Used to indentify the AST object of a project's D module
		/// </summary>
		public const string DParserPropertyKey="DDom";

		#region Properties
		/// <summary>
		/// Used for incremental compiling&linking
		/// </summary>
		public readonly Dictionary<ProjectFile, DateTime> LastModificationTimes = new Dictionary<ProjectFile, DateTime>();
		public readonly List<string> BuiltObjects = new List<string>();

		public override string ProjectType	{get { return "Native"; }}
		public override string[] SupportedLanguages	{get{return new[]{"D"};}}
		
		/// <summary>
		/// Stores parse information from project-wide includes
		/// </summary>
		public ASTStorage LocalIncludeCache { get; private set; }

		public IEnumerable<IAbstractSyntaxTree> ParseCache
		{
			get {
				return DCodeCompletionSupport.EnumAvailableModules(this);
			}
		}
		
		[ItemProperty("UseDefaultCompiler")]
		public bool UseDefaultCompilerVendor = true;
		
		[ItemProperty("Compiler")]
		public DCompilerVendor UsedCompilerVendor= DCompilerVendor.DMD;

		[ItemProperty("Target")]
		public DCompileTarget CompileTarget = DCompileTarget.Executable;

		[ItemProperty("Libs")]
		[ItemProperty("Lib", Scope = "*")]
		public List<string> ExtraLibraries = new List<string>();

		[ItemProperty("IncrementalLinking")]
		public bool EnableIncrementalLinking = true;

		/// <summary>
		/// Returns the actual compiler configuration used by this project
		/// </summary>
		public DCompilerConfiguration Compiler
		{		
			get 
			{ 
				return DCompiler.Instance.GetCompiler(UseDefaultCompilerVendor?DCompiler.Instance.DefaultCompiler : UsedCompilerVendor); 
			}
			set { UsedCompilerVendor = value.Vendor; }
		}
		#endregion

		#region Parsed project modules
		public IEnumerable<IAbstractSyntaxTree> ParsedModules
		{
			get
			{
				var l = new List<IAbstractSyntaxTree>();
				foreach (ProjectFile pf in Items)
					if (pf != null && pf.ExtendedProperties.Contains(DParserPropertyKey))
						l.Add( pf.ExtendedProperties[DParserPropertyKey] as IAbstractSyntaxTree);
				return l;
			}
		}

		public void UpdateLocalIncludeCache()
		{
			DCompilerConfiguration.UpdateParseCacheAsync(LocalIncludeCache);
		}

		public void ReparseModule(ProjectFile pf)
		{
			if (pf == null || !DLanguageBinding.IsDFile(pf.FilePath.FileName))
				return;

			try
			{
				var ddom = DParser.ParseFile(pf.FilePath.ToAbsolute(BaseDirectory));

				// Update relative module name
				ddom.ModuleName = pf.ProjectVirtualPath.ChangeExtension(null).ToString().Replace(Path.DirectorySeparatorChar, '.');

				pf.ExtendedProperties[DParserPropertyKey] = ddom;
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error while parsing " + pf.FilePath.ToString(), ex);
			}
		}

		public void ReparseModule(string file)
		{
			ReparseModule(GetProjectFile(file));
		}

		/// <summary>
		/// Updates the project's parse cache and reparses all of its D sources
		/// </summary>
		public void UpdateParseCache()
		{
			foreach (ProjectFile pf in Items)
				ReparseModule(pf);					
		}
		#endregion

		#region Init
		void Init()
		{
			LocalIncludeCache = new ASTStorage();

			//if(DCompiler.Instance!=null)
			//	UsedCompilerVendor = DCompiler.Instance.DefaultCompiler;
		}

		public DProject() { Init(); }

		public DProject(ProjectCreateInformation info, XmlElement projectOptions)
		{			
			Init();

			string binPath = ".";
			
			if (info != null)
			{
				Name = info.ProjectName;

				BaseDirectory = info.ProjectBasePath;

				if(info.BinPath!=null)
					binPath = info.BinPath;
			}

			if (projectOptions != null)
			{
				foreach (XmlNode lib in projectOptions.GetElementsByTagName("Lib"))
					if (!string.IsNullOrWhiteSpace(lib.InnerText))
						ExtraLibraries.Add(lib.InnerText);
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
				c.Output = Name;

				if (projectOptions != null)
				{
					// Set project's target type to the one which has been defined in the project template
					if (projectOptions.Attributes["Target"] != null)
					{
						CompileTarget = (DCompileTarget)Enum.Parse(
							typeof(DCompileTarget),
							projectOptions.Attributes["Target"].InnerText);
					}
					
					// Set project's compiler
					if (projectOptions.Attributes["Compiler"] != null)
					{
						UsedCompilerVendor = (DCompilerVendor)Enum.Parse(
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

		#region Build Configurations
		public override SolutionItemConfiguration CreateConfiguration(string name)
		{
			var config = new DProjectConfiguration(this) { Name=name};
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
		public override bool IsCompileable(string fileName)
		{
			return DLanguageBinding.IsDFile(fileName) || fileName.ToLower().EndsWith(".rc");
		}

		public override FilePath GetOutputFileName(ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;

			return cfg.OutputDirectory.Combine(cfg.CompiledOutputName);
		}

		protected override BuildResult DoBuild(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			return ProjectBuilder.CompileProject(monitor,this,configuration);
		}

		protected override bool CheckNeedsBuild(ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;
			
			if (!EnableIncrementalLinking || 
				!File.Exists(cfg.CompiledOutputName))
				return true;

			foreach (var f in Files)
			{
				if (f.BuildAction != BuildAction.Compile)
					continue;

				if(!File.Exists(f.LastGenOutput) || 
					!LastModificationTimes.ContainsKey(f) ||
					LastModificationTimes[f]!=File.GetLastWriteTime(f.FilePath))
					return true;
			}

			return false;
		}

		protected override void DoClean(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			var cfg = GetConfiguration(configuration) as DProjectConfiguration;

			// delete obj/res files
			monitor.BeginTask("Delete intermediate files", Files.Count);
			foreach (var f in Files)
			{
				try
				{
					if (File.Exists(f.LastGenOutput))
						File.Delete(f.LastGenOutput);
				}
				catch (Exception ex)
				{
					monitor.ReportError("Error while removing " + f, ex);
				}
				monitor.Step(1);
			}
			monitor.EndTask();

			// delete target file
			monitor.BeginTask("Delete output file", 1);

			if (File.Exists(cfg.CompiledOutputName))
				File.Delete(cfg.CompiledOutputName);

			monitor.EndTask();

			monitor.ReportSuccess("Cleanup successful!");
		}

		protected override void OnEndLoad ()
		{
			base.OnEndLoad();
			
			/*lock (LocalIncludeCache) {
				LocalIncludeCache.ParsedGlobalDictionaries.Clear();
				DLanguageBinding.DIncludesParser.AddDirectoryRange(IncludePaths, LocalIncludeCache);
			}*/
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

			if (CompileTarget != DCompileTarget.Executable)
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

		#region Loading&Saving

		[ItemProperty("Includes")]
		[ItemProperty("Path", Scope = "*")]
		List<string> tempIncludes = new List<string>();

		public void Deserialize(ITypeSerializer handler, DataCollection data)
		{
			handler.Deserialize(this, data);

			foreach (var p in tempIncludes)
				LocalIncludeCache.Add(p);

			UpdateLocalIncludeCache();
			UpdateParseCache();
		}

		public DataCollection Serialize(ITypeSerializer handler)
		{
			tempIncludes.Clear();
			foreach (var p in LocalIncludeCache.DirectoryPaths)
				tempIncludes.Add(p);

			var ret = handler.Serialize(this);
			
			return ret;
		}
		#endregion
	}
}
