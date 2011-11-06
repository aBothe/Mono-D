using System;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.OptionPanels
{
	public partial class BuildArgumentOptions : Gtk.Dialog
	{		
		private bool isDebug;
		private DCompilerConfiguration configuration;
		
		
		private string guiCompiler;
		private string consoleCompiler;
		private string sharedlibCompiler;
		private string staticlibCompiler;
		private string guiLinker;
		private string consoleLinker;
		private string sharedlibLinker;
		private string staticlibLinker;
		
		public BuildArgumentOptions ()
		{
			this.Build ();
		}
		
		public bool IsDebug
		{
			get 
			{
				return isDebug;
			} 
			set
			{
				isDebug = value;
			}
		}
		
		public bool CanStore{get;set;}		
		
		protected override void OnShown ()
		{
			base.OnShown ();
			
			this.Title = (isDebug?"Debug":"Release") + " build arguments";			

			//compiler targets
			txtGUICompiler.Text = guiCompiler;
			txtConsoleCompiler.Text = consoleCompiler;
			txtSharedLibCompiler.Text = sharedlibCompiler;
			txtStaticLibCompiler.Text = staticlibCompiler;
			
			
			//linker targets 
			txtGUILinker.Text = guiLinker;
			txtConsoleLinker.Text = consoleLinker;
			txtSharedLibLinker.Text = sharedlibLinker;
			txtStaticLibLinker.Text = staticlibLinker;				
		}
		
		public void Load(DCompilerConfiguration config)
		{
			configuration = config;
			
			LinkTargetConfiguration targetConfig;			
			BuildConfiguration arguments;

			//compiler targets
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			guiCompiler = arguments.CompilerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			consoleCompiler = arguments.CompilerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			sharedlibCompiler = arguments.CompilerArguments;
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			staticlibCompiler = arguments.CompilerArguments;
			
			
			//linker targets 		
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			guiLinker = arguments.LinkerArguments;
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			consoleLinker = arguments.LinkerArguments;			
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			sharedlibLinker = arguments.LinkerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			staticlibLinker = arguments.LinkerArguments;			
		}
		
		public void Store()
		{
			if ((configuration == null))				
				return;
			
			LinkTargetConfiguration targetConfig;			
			BuildConfiguration arguments;

			//compiler targets
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = guiCompiler;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = consoleCompiler;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = sharedlibCompiler;
			
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = staticlibCompiler;
			
			
			//linker targets 
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = guiLinker;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = consoleLinker;			
			
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = sharedlibLinker;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = staticlibLinker;
			
		}

		protected void buttonOk_Clicked (object sender, System.EventArgs e)
		{
			//compiler targets
			guiCompiler = txtGUICompiler.Text;
			consoleCompiler = txtConsoleCompiler.Text;
			sharedlibCompiler = txtSharedLibCompiler.Text;
			staticlibCompiler = txtStaticLibCompiler.Text;			
			
			//linker targets 
			guiLinker = txtGUILinker.Text;
			consoleLinker = txtConsoleLinker.Text;
			sharedlibLinker = txtSharedLibLinker.Text;
			staticlibLinker = txtStaticLibLinker.Text;	
		}
	}
}

