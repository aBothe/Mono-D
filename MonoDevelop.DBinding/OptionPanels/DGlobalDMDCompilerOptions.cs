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
	public partial class DGlobalDMDCompilerOptions : Gtk.Bin
	{
		private DProjectConfiguration configuration;
		
		public DGlobalDMDCompilerOptions () 
		{
			this.Build ();	
			
		}
	
		public void Load (DProjectConfiguration config)
		{
			configuration = config;
			
			
			//DCompiler.Init();
			//DCompiler.Instance
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
			DGlobalBuildArgumentOptions dialog = new DGlobalBuildArgumentOptions();
			try{			
				dialog.IsDebug = false;
				dialog.ShowAll();
				response = (Gtk.ResponseType) dialog.Run ();
			}finally{
				dialog.Destroy();
			}
			
		}
		protected void btnDebugArguments_Clicked (object sender, System.EventArgs e)
		{
			Gtk.ResponseType response;						
			DGlobalBuildArgumentOptions dialog = new DGlobalBuildArgumentOptions();
			try{
				dialog.IsDebug = true;
				dialog.ShowAll();
				response = (Gtk.ResponseType) dialog.Run ();			
			}finally{
				dialog.Destroy();
			}
			
		}
	}
	
	public class DGlobalDMDCompilerOptionsBinding : OptionsPanel
	{
		private DGlobalDMDCompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel = new DGlobalDMDCompilerOptions ();
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
