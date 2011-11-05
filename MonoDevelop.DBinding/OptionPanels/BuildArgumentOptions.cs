using System;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.OptionPanels
{
	public partial class BuildArgumentOptions : Gtk.Dialog
	{		
		private bool isDebug;
		private DCompilerConfiguration configuration;
		
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
				this.Title = (isDebug?"Debug":"Release") + " build arguments";
			}
		}
		
		public bool CanStore{get;set;}		
		
		public void Load(DCompilerConfiguration config)
		{
			configuration = config;
			
			LinkTargetConfiguration targetConfig;			
			BuildConfiguration arguments;

			//compiler targets
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtGUICompiler.Text = arguments.CompilerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtConsoleCompiler.Text = arguments.CompilerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtSharedLibCompiler.Text = arguments.CompilerArguments;
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtStaticLibCompiler.Text = arguments.CompilerArguments;
			
			
			//linker targets 
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtConsoleLinker.Text = arguments.LinkerArguments;
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtGUILinker.Text = arguments.LinkerArguments;
			
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtSharedLibLinker.Text = arguments.LinkerArguments;

			targetConfig =  config.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtStaticLibLinker.Text = arguments.LinkerArguments;			
		}
		
		public void Store()
		{
			if ((configuration == null) || (!CanStore))				
				return;
			
			LinkTargetConfiguration targetConfig;			
			BuildConfiguration arguments;

			//compiler targets
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = txtGUICompiler.Text;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = txtConsoleCompiler.Text;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = txtSharedLibCompiler.Text;
			
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.CompilerArguments = txtStaticLibCompiler.Text;
			
			
			//linker targets 
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = txtConsoleLinker.Text;
			
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = txtGUILinker.Text;
			
			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.SharedLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = txtSharedLibLinker.Text;

			targetConfig =  configuration.GetTargetConfiguration(DCompileTarget.StaticLibrary);				
			arguments = targetConfig.GetArguments(isDebug);					
			arguments.LinkerArguments = txtStaticLibLinker.Text;
			
		}
	}
}

