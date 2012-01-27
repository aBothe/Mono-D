using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.Ide;


namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DCompilerOptions : Gtk.Bin
	{
		private Gtk.ListStore compilerStore = new Gtk.ListStore (typeof(string), typeof(DCompilerConfiguration));
		private DCompilerConfiguration configuration;
		private Gtk.ListStore defaultLibStore = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore includePathStore = new Gtk.ListStore (typeof(string));
		private BuildArgumentOptions releaseArgumentsDialog = null;
		private BuildArgumentOptions debugArgumentsDialog = null;
		
		private List<DCompilerConfiguration> pendingPresetDeletes;
		
		public DCompilerOptions ()
		{
			this.Build ();
			
			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			
			cmbCompilers.Clear ();			

			cmbCompilers.PackStart (textRenderer, false);
			cmbCompilers.AddAttribute (textRenderer, "text", 0);

			cmbCompilers.Model = compilerStore;

			tvDefaultLibs.Model = defaultLibStore;
			tvDefaultLibs.HeadersVisible = false;
			tvDefaultLibs.AppendColumn ("Library", textRenderer, "text", 0);
			
			tvIncludePaths.Model = includePathStore;
			tvIncludePaths.HeadersVisible = false;
			tvIncludePaths.AppendColumn ("Include", textRenderer, "text", 0);	
			
			releaseArgumentsDialog = new BuildArgumentOptions ();
			debugArgumentsDialog = new BuildArgumentOptions ();
			
			pendingPresetDeletes = new List<DCompilerConfiguration>();
		}
	
		public void ReloadCompilerList ()
		{
			compilerStore.Clear ();
			
			foreach (var cmp in DCompilerService.Instance.Compilers) {
				var virtCopy = new DCompilerConfiguration ();
				virtCopy.CopyFrom (cmp);
				compilerStore.AppendValues (cmp.Vendor, virtCopy);
			}
			
			Gtk.TreeIter iter;
			compilerStore.GetIterFirst (out iter);
			cmbCompilers.SetActiveIter (iter);
		}
		
		protected void OnCmbCompilersChanged (object sender, System.EventArgs e)
		{
			if (configuration != null) {
				ApplyToVirtConfiguration ();
			}
			
			Gtk.TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) {
				configuration = cmbCompilers.Model.GetValue (iter, 1) as DCompilerConfiguration;
				
				Load (configuration);
			}
			
			//only allow NON default compiler preset to be renamed and removed
			//ToDo: figure out how best to determine this
			//btnRenameCompiler.Sensitive = ?
			//btnRemoveCompiler.Sensitive = ?
		}
		
		public void Load (DCompilerConfiguration config)
		{
			configuration = config;
			//for now, using Executable target compiler command for all targets source compiling
			LinkTargetConfiguration targetConfig;
			targetConfig = config.GetTargetConfiguration (DCompileTarget.Executable);
			
			txtBinPath.Text = config.BinPath;
			
			txtCompiler.Text = targetConfig.Compiler;
			
			//linker targets 			
			targetConfig = config.GetTargetConfiguration (DCompileTarget.Executable); 						
			txtConsoleAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = config.GetTargetConfiguration (DCompileTarget.ConsolelessExecutable); 						
			txtGUIAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = config.GetTargetConfiguration (DCompileTarget.SharedLibrary); 						
			txtSharedLibLinker.Text = targetConfig.Linker;
			
			targetConfig = config.GetTargetConfiguration (DCompileTarget.StaticLibrary); 						
			txtStaticLibLinker.Text = targetConfig.Linker;
			
			releaseArgumentsDialog.Load (config, false);		
			debugArgumentsDialog.Load (config, true);				

			defaultLibStore.Clear ();
			foreach (string lib in config.DefaultLibraries)
				defaultLibStore.AppendValues (lib);

			includePathStore.Clear ();
			foreach (var p in config.GlobalParseCache.DirectoryPaths)
				includePathStore.AppendValues (p);
		}

		public bool Validate ()
		{
			return true;
		}

		public bool Store()
		{
			ApplyToVirtConfiguration();

			Gtk.TreeIter iter;
			compilerStore.GetIterFirst(out iter);

			do
			{
				var virtCmp=compilerStore.GetValue(iter,1) as DCompilerConfiguration;
				var cmp = DCompilerService.Instance.GetCompiler(virtCmp.Vendor);

				//check for renames
				var newVendorName = compilerStore.GetValue(iter, 0) as string;
				if (virtCmp.Vendor != newVendorName)
					virtCmp.Vendor = newVendorName;					
				
				if (cmp != null)
					cmp.CopyFrom(virtCmp);
				else
					DCompilerService.Instance.Compilers.Add(virtCmp);
			}
			while (compilerStore.IterNext(ref iter));
			
			//process pending config deletions
			foreach(var delConfig in pendingPresetDeletes)
			{
				foreach(var config in DCompilerService.Instance.Compilers)
				{				
					if (delConfig.Vendor == config.Vendor)
					{
						DCompilerService.Instance.Compilers.Remove(config);
						break;
					}
				}
			}
			pendingPresetDeletes.Clear();
			
			
			return true;
		}
		
		public bool ApplyToVirtConfiguration ()
		{
			if (configuration == null)
				return false;
			
			Gtk.TreeIter iter;
			string line;
			
			configuration.BinPath = txtBinPath.Text;
			
			//for now, using Executable target compiler command for all targets source compiling
			LinkTargetConfiguration targetConfig;
			targetConfig = configuration.GetTargetConfiguration (DCompileTarget.Executable); 			
			targetConfig.Compiler = txtCompiler.Text;
			
			//linker targets 			
			targetConfig = configuration.GetTargetConfiguration (DCompileTarget.Executable); 						
			targetConfig.Linker = txtConsoleAppLinker.Text;			
			
			targetConfig = configuration.GetTargetConfiguration (DCompileTarget.ConsolelessExecutable); 						
			targetConfig.Linker = txtGUIAppLinker.Text;			
			
			targetConfig = configuration.GetTargetConfiguration (DCompileTarget.SharedLibrary); 						
			targetConfig.Linker = txtSharedLibLinker.Text;
			
			targetConfig = configuration.GetTargetConfiguration (DCompileTarget.StaticLibrary); 						
			targetConfig.Linker = txtStaticLibLinker.Text;
			
			releaseArgumentsDialog.Store ();			
			debugArgumentsDialog.Store ();					
			
			defaultLibStore.GetIterFirst (out iter);
			configuration.DefaultLibraries.Clear ();
			while (defaultLibStore.IterIsValid (iter)) {
				line = (string)defaultLibStore.GetValue (iter, 0);
				configuration.DefaultLibraries.Add (line);
				defaultLibStore.IterNext (ref iter);
			}

			#region Store new include paths
			var paths = new List<string> ();

			includePathStore.GetIterFirst (out iter);
			while (includePathStore.IterIsValid (iter)) {
				line = (string)includePathStore.GetValue (iter, 0);
				
				paths.Add (line);

				includePathStore.IterNext (ref iter);
			}

			// If current dir count != the new dir count
			bool cacheUpdateRequired = paths.Count != configuration.GlobalParseCache.ParsedGlobalDictionaries.Count;

			// If there's a new directory in it
			if (!cacheUpdateRequired)
				foreach (var path in paths)
					if (!configuration.GlobalParseCache.ContainsDictionary (path)) {
						cacheUpdateRequired = true;
						break;
					}

			if (cacheUpdateRequired) {
				configuration.GlobalParseCache.ParsedGlobalDictionaries.Clear ();

				foreach (var path in paths)
					configuration.GlobalParseCache.Add (path);

				try {
					// Update parse cache immediately
					DCompilerConfiguration.UpdateParseCacheAsync (configuration.GlobalParseCache);
				} catch (Exception ex) {
					LoggingService.LogError ("Include path analysis error", ex);
				}
			}
			#endregion

			return true;
		}
		
		private void ShowArgumentsDialog (bool isDebug)
		{
			BuildArgumentOptions dialog = null;
			if (isDebug)
				dialog = debugArgumentsDialog;
			else
				dialog = releaseArgumentsDialog;

			MessageService.RunCustomDialog (dialog, IdeApp.Workbench.RootWindow);
		}
		
		protected void btnReleaseArguments_Clicked (object sender, System.EventArgs e)
		{			
			ShowArgumentsDialog (false);						
		}

		protected void btnDebugArguments_Clicked (object sender, System.EventArgs e)
		{
			ShowArgumentsDialog (true);			
		}

		protected void btnBrowseDefaultLib_Clicked (object sender, System.EventArgs e)
		{
			var dialog = new AddLibraryDialog (AddLibraryDialog.FileFilterType.LibraryFiles)
			{
				TransientFor = Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};

			if (dialog.Run () == (int)Gtk.ResponseType.Ok)
				txtDefaultLib.Text = dialog.Library;
		}
		
		private void OnDefaultLibAdded (object sender, System.EventArgs e)
		{
			if (txtDefaultLib.Text.Length > 0) {				
				defaultLibStore.AppendValues (txtDefaultLib.Text);
				txtDefaultLib.Text = string.Empty;
			}			
		}
		
		protected void btnAddDefaultLib_Click (object sender, System.EventArgs e)
		{
			OnDefaultLibAdded (sender, e);
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
			OnDefaultLibAdded (sender, e);
		}
		
		protected void btnBrowseIncludePath_Clicked (object sender, System.EventArgs e)
		{
			Gtk.FileChooserDialog dialog = new Gtk.FileChooserDialog (
				"Select D Source Folder",
				Ide.IdeApp.Workbench.RootWindow,
				Gtk.FileChooserAction.SelectFolder,
				"Cancel",
				Gtk.ResponseType.Cancel,
				"Ok",
				Gtk.ResponseType.Ok) 
			{ 
				TransientFor=Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};

			try {
				if (dialog.Run () == (int)Gtk.ResponseType.Ok)
					txtIncludePath.Text = dialog.Filename;
			} finally {
				dialog.Destroy ();
			}
		}
		
		private void OnIncludePathAdded (object sender, System.EventArgs e)
		{
			if (txtIncludePath.Text.Length > 0) {				
				includePathStore.AppendValues (txtIncludePath.Text);
				txtIncludePath.Text = string.Empty;
			}			
		}
		
		protected void btnAddIncludePath_Clicked (object sender, System.EventArgs e)
		{
			OnIncludePathAdded (sender, e);
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
			OnIncludePathAdded (sender, e);
		}

		protected void btnDefaults_Clicked (object sender, System.EventArgs e)
		{			
			//need new object, because the user can still hit canel at the config screen
			//so we don't want to update the real object yet
			DCompilerConfiguration realConfig = configuration;			
			try {
				var tempConfig = new DCompilerConfiguration{Vendor = configuration.Vendor};		
				if (DCompilerConfiguration.ResetToDefaults (tempConfig))	
					Load (tempConfig);
				
			} finally {
				configuration = realConfig;	
				releaseArgumentsDialog.Configuration = realConfig;
				debugArgumentsDialog.Configuration = realConfig;
			}				
		}

		protected void OnButtonBinPathBrowserClicked (object sender, System.EventArgs e)
		{
			var dialog = new Gtk.FileChooserDialog ("Select Compiler's bin path", null, Gtk.FileChooserAction.SelectFolder, "Cancel", Gtk.ResponseType.Cancel, "Ok", Gtk.ResponseType.Ok)
			{
				TransientFor = Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};

			try {
				if (dialog.Run () == (int)Gtk.ResponseType.Ok)
					txtBinPath.Text = dialog.Filename;
			} finally {
				dialog.Destroy ();
			}
		}

		private void CreateNewPreset(string name)
		{
			//ToDo: prevent adding a preset name that already exists
			
			DCompilerConfiguration newpreset = new DCompilerConfiguration ();
			newpreset.CopyFrom (configuration);		
			newpreset.Vendor = name;			
			Gtk.TreeIter iter;			
			iter = compilerStore.AppendValues(newpreset.Vendor, newpreset);
			cmbCompilers.SetActiveIter (iter);			
			foreach(var delConfig in pendingPresetDeletes)
			{
				if (delConfig.Vendor == configuration.Vendor)
				{
					pendingPresetDeletes.Remove(delConfig);
					break;
				}
			}			
		}
		
		protected void OnBtnAddCompilerClicked (object sender, System.EventArgs e)
		{			
			EditCompilerName dialog = new EditCompilerName();
			try {
				if ((dialog.Run () == (int)Gtk.ResponseType.Ok) && (dialog.PresetName != ""))
					CreateNewPreset(dialog.PresetName);
			} finally {
				dialog.Destroy ();
			}			
		}
		
		protected void OnBtnRenameCompilerClicked (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) 
			{					
				EditCompilerName dialog = new EditCompilerName();
				try {
					dialog.PresetName = cmbCompilers.Model.GetValue (iter, 0) as string;		
					if ((dialog.Run () == (int)Gtk.ResponseType.Ok) && (dialog.PresetName != ""))
					{						
						var config = cmbCompilers.Model.GetValue(iter, 1) as DCompilerConfiguration;
						cmbCompilers.Model.SetValue(iter, 0, dialog.PresetName);						
					}
				} finally {
					dialog.Destroy ();
				}		
			}
		}
		
		protected void OnBtnRemoveCompilerClicked (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) 
			{
				if (!pendingPresetDeletes.Contains(configuration))
					pendingPresetDeletes.Add(configuration);				
				compilerStore.Remove(ref iter);
				compilerStore.GetIterFirst (out iter);
				cmbCompilers.SetActiveIter (iter);	
			}
		}		
		
	}
	
	public class DCompilerOptionsBinding : OptionsPanel
	{
		private DCompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DCompilerOptions ();
			LoadConfigData ();
			return panel;
		}
		
		public void LoadConfigData ()
		{
			panel.ReloadCompilerList ();
		}

		public override bool ValidateChanges ()
		{
			return panel.Validate ();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store();
		}
	}	
}
