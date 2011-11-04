using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DCompilerOptions : Gtk.Bin
	{
		private DCompilerConfiguration configuration;
		
		public DCompilerOptions () 
		{
			this.Build ();	
			
		}
	
		public void Load (DCompilerConfiguration config)
		{
			configuration = config;
			//default compiler
			LinkTargetConfiguration targetConfig;
 			targetConfig = config.GetTargetConfiguration(DCompileTarget.Executable); 			
			txtCompiler.Text = targetConfig.Compiler;
			
			//linker targets 			
 			targetConfig = config.GetTargetConfiguration(DCompileTarget.Executable); 						
			txtConsoleAppLinker.Text = targetConfig.Linker;			
			
 			targetConfig = config.GetTargetConfiguration(DCompileTarget.ConsolelessExecutable); 						
			txtGUIAppLinker.Text = targetConfig.Linker;			
			
 			targetConfig = config.GetTargetConfiguration(DCompileTarget.SharedLibrary); 						
			txtSharedLibLinker.Text = targetConfig.Linker;
			
 			targetConfig = config.GetTargetConfiguration(DCompileTarget.StaticLibrary); 						
			txtStaticLibLinker.Text = targetConfig.Linker;
		}


		public bool Validate()
		{
			return true;
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			return true;
		}

		protected void btnReleaseArguments_Clicked (object sender, System.EventArgs e)
		{			
			Gtk.ResponseType response;			
			BuildArgumentOptions dialog = new BuildArgumentOptions();
			try{			
				dialog.IsDebug = false;
				dialog.Load (configuration);
				response = (Gtk.ResponseType) dialog.Run ();
			}finally{
				dialog.Destroy();
			}
			
		}
		protected void btnDebugArguments_Clicked (object sender, System.EventArgs e)
		{
			Gtk.ResponseType response;						
			BuildArgumentOptions dialog = new BuildArgumentOptions();
			try{
				dialog.IsDebug = true;
				dialog.Load (configuration);
				response = (Gtk.ResponseType) dialog.Run ();			
			}finally{
				dialog.Destroy();
			}
			
		}
	}
	
	public class DMDCompilerOptionsBinding : OptionsPanel
	{
		private DCompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DCompilerOptions ();
			LoadConfigData();
			return panel;
		}
		
		public void LoadConfigData ()
		{
			DCompiler.Init();						
			panel.Load(DCompiler.Instance.Dmd);
		}		

		public override bool ValidateChanges()
		{
			return panel.Validate();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
	public class GDCCompilerOptionsBinding : OptionsPanel
	{
		private DCompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DCompilerOptions ();
			LoadConfigData();
			return panel;
		}
		
		public void LoadConfigData ()
		{
			DCompiler.Init();
			panel.Load(DCompiler.Instance.Gdc);
		}		

		public override bool ValidateChanges()
		{
			return panel.Validate();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
	public class LDCCompilerOptionsBinding : OptionsPanel
	{
		private DCompilerOptions panel;

		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DCompilerOptions ();
			LoadConfigData();
			return panel;
		}
			
		public void LoadConfigData ()
		{
			DCompiler.Init();			
			panel.Load(DCompiler.Instance.Ldc);
		}		

		public override bool ValidateChanges()
		{
			return panel.Validate();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}	
	
}
