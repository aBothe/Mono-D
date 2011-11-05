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
		
		private Gtk.ListStore defaultLibStore = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore includePathStore = new Gtk.ListStore (typeof(string));		
		
		public DCompilerOptions () 
		{
			this.Build ();	
			
			
			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			
			tvDefaultLibs.Model = defaultLibStore;
			tvDefaultLibs.HeadersVisible = false;
			tvDefaultLibs.AppendColumn ("Library", textRenderer, "text", 0);
			
			tvIncludePaths.Model = includePathStore;
			tvIncludePaths.HeadersVisible = false;
			tvIncludePaths.AppendColumn ("Include", textRenderer, "text", 0);			
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
			
			
			foreach (string lib in config.DefaultLibraries)
				defaultLibStore.AppendValues (lib);
			
			/*foreach (string includePath in config.Includes)
				includePathStore.AppendValues (includePath);*/
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
			dialog.IsDebug = false;
			dialog.Load (configuration);
			response = (Gtk.ResponseType) dialog.Run ();
			dialog.Destroy();			
			
			
		}
		protected void btnDebugArguments_Clicked (object sender, System.EventArgs e)
		{
			Gtk.ResponseType response;						
			BuildArgumentOptions dialog = new BuildArgumentOptions();
			dialog.IsDebug = true;
			dialog.Load (configuration);
			response = (Gtk.ResponseType) dialog.Run ();
			dialog.Destroy();
			
		}

		protected void btnBrowseDefaultLib_Clicked (object sender, System.EventArgs e)
		{
			Gtk.ResponseType response;
			AddLibraryDialog dialog = new AddLibraryDialog(AddLibraryDialog.FileFilterType.LibraryFiles);
			response = (Gtk.ResponseType) dialog.Run ();
			if (response == Gtk.ResponseType.Ok)
				txtDefaultLib.Text = dialog.Library;
		}
		
		private void OnDefaultLibAdded(object sender, System.EventArgs e)
		{
			if (txtDefaultLib.Text.Length > 0) {				
				defaultLibStore.AppendValues (txtDefaultLib.Text);
				txtDefaultLib.Text = string.Empty;
			}			
		}		
		
		protected void btnAddDefaultLib_Click (object sender, System.EventArgs e)
		{
			OnDefaultLibAdded(sender, e);
		}		
		
		protected void btnRemoveDefaultLib_Clicked (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			tvDefaultLibs.Selection.GetSelected (out iter);
			defaultLibStore.Remove (ref iter);
		}				

		protected void tvDefaultLibs_CursorChanged (object sender, System.EventArgs e)
		{
			btnRemoveDefaultLib.Sensitive = true;
		}
		
		protected void txtDefaultLib_Changed (object sender, System.EventArgs e)
		{
			if (string.IsNullOrEmpty (txtDefaultLib.Text))
				btnAddDefaultLib.Sensitive = false;
			else
				btnAddDefaultLib.Sensitive = true;
		}

		protected void txtDefaultLib_Activated (object sender, System.EventArgs e)
		{
			OnDefaultLibAdded(sender, e);
		}		
		
		protected void btnBrowseIncludePath_Clicked (object sender, System.EventArgs e)
		{
			Gtk.ResponseType response;
			AddPathDialog dialog = new AddPathDialog (System.IO.Directory.GetCurrentDirectory().ToString());/*configuration.SourcePath*/
			response = (Gtk.ResponseType) dialog.Run ();
			if (response == Gtk.ResponseType.Ok)			
				txtIncludePath.Text = dialog.SelectedPath;
		}
		
		private void OnIncludePathAdded(object sender, System.EventArgs e)
		{
			if (txtIncludePath.Text.Length > 0) {				
				includePathStore.AppendValues (txtIncludePath.Text);
				txtIncludePath.Text = string.Empty;
			}			
		}
		
		protected void btnAddIncludePath_Clicked (object sender, System.EventArgs e)
		{
			OnIncludePathAdded(sender, e);
		}

		protected void btnRemoveIncludePath_Clicked (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			tvIncludePaths.Selection.GetSelected (out iter);
			includePathStore.Remove (ref iter);
		}

		protected void tvIncludePaths_CursorChanged (object sender, System.EventArgs e)
		{
			btnRemoveIncludePath.Sensitive = true;
		}

		protected void txtIncludePath_Changed (object sender, System.EventArgs e)
		{
			if (string.IsNullOrEmpty (txtIncludePath.Text))
				btnAddIncludePath.Sensitive = false;
			else
				btnAddIncludePath.Sensitive = true;
		}

		protected void txtIncludePath_Activated (object sender, System.EventArgs e)
		{
			OnIncludePathAdded(sender, e);
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
