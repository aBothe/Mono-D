using System;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.OptionPanels
{
	public partial class DGlobalBuildArgumentOptions : Gtk.Dialog
	{		
		private bool isDebug;
		private DCompilerConfiguration configuration;
		
		public DGlobalBuildArgumentOptions ()
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
		
		public void Load(DCompilerConfiguration config)
		{
			configuration = config;
			
			LinkTargetConfiguration targetConfig;			
			BuildConfiguration arguments;

			//default compiler
			targetConfig =  config.GetTargetConfiguration(DCompileTarget.Executable);				
			arguments = targetConfig.GetArguments(isDebug);					
			txtCompiler.Text = arguments.CompilerArguments;

			
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
	}
}

