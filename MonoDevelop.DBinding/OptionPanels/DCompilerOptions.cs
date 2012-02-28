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
using Gtk;
using MonoDevelop.D.Building.CompilerPresets;

namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DCompilerOptions : Gtk.Bin
	{
		#region Properties & Init
		private Gtk.ListStore compilerStore = new Gtk.ListStore (typeof(string), typeof(DCompilerConfiguration));
		private DCompilerConfiguration configuration;
		string defaultCompilerVendor;
		private Gtk.ListStore defaultLibStore = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore includePathStore = new Gtk.ListStore (typeof(string));
		private BuildArgumentOptions releaseArgumentsDialog = null;
		private BuildArgumentOptions debugArgumentsDialog = null;

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
		}
		#endregion

		#region Preset management
		public void ReloadCompilerList ()
		{
			compilerStore.Clear ();

			defaultCompilerVendor = DCompilerService.Instance.DefaultCompiler;

			foreach (var cmp in DCompilerService.Instance.Compilers) {
				var virtCopy = new DCompilerConfiguration ();
				virtCopy.CopyFrom (cmp);
				compilerStore.AppendValues (cmp.Vendor, virtCopy);
			}

			Gtk.TreeIter iter;
			if (compilerStore.GetIterFirst (out iter))
				cmbCompilers.SetActiveIter (iter);
		}

		string ComboBox_CompilersLabel {
			get {
				return cmbCompilers.ActiveText;
			}
		}
		
		protected void OnCmbCompilersChanged (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) {
				var newConfig = cmbCompilers.Model.GetValue (iter, 1) as DCompilerConfiguration;

				if (configuration == newConfig)
					return;
				else
					ApplyToVirtConfiguration ();

				Load (newConfig);
			} else if (!compilerStore.GetIterFirst (out iter)) {
				ApplyToVirtConfiguration ();
				Load (null);
			}
		}
		
		private void CreateNewPreset (string name)
		{
			if (!CanUseNewName (name))
				return;

			ApplyToVirtConfiguration ();

			configuration = new DCompilerConfiguration { 
				Vendor=name
			};

			ApplyToVirtConfiguration ();

			Gtk.TreeIter iter;
			iter = compilerStore.AppendValues (configuration.Vendor, configuration);
			cmbCompilers.SetActiveIter (iter);
		}

		bool CanUseNewName (string newName)
		{
			if (!System.Text.RegularExpressions.Regex.IsMatch (newName, "[\\w-]+")) {
				MessageService.ShowError ("Compiler configuration", "Compiler name can only contain letters/digits/'-'");

				return false;
			}

			Gtk.TreeIter iter;
			compilerStore.GetIterFirst (out iter);

			do {
				var virtCmp = compilerStore.GetValue (iter, 1) as DCompilerConfiguration;
				
				if (virtCmp.Vendor == newName) {
					MessageService.ShowError ("Compiler configuration", "Compiler name already taken");
					return false;
				}

			} while (compilerStore.IterNext(ref iter));

			return true;
		}

		void RenameCurrentPreset (string newName)
		{
			if (configuration == null) {
				CreateNewPreset (newName);
				return;
			}

			if (configuration.Vendor == newName || !CanUseNewName (newName))
				return;

			// If default compiler affected, update the default compiler's name, too
			if (defaultCompilerVendor == configuration.Vendor)
				defaultCompilerVendor = newName;

			// Apply new name to the cfg object
			configuration.Vendor = newName;

			// + to the compiler store model
			compilerStore.Foreach ((TreeModel tree, TreePath path, TreeIter iter) =>
			{
				if (compilerStore.GetValue (iter, 1) == configuration) {
					compilerStore.SetValue (iter, 0, configuration.Vendor);
					return true;
				}

				return false;
			});
		}

		void MakeCurrentConfigDefault ()
		{
			if (configuration != null) {
				defaultCompilerVendor = configuration.Vendor;
				btnMakeDefault.Active = true;
			}
		}

		protected void OnBtnAddCompilerClicked (object sender, System.EventArgs e)
		{
			CreateNewPreset (ComboBox_CompilersLabel);
		}

		protected void OnBtnRemoveCompilerClicked (object sender, System.EventArgs e)
		{
			Gtk.TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) {
				Gtk.TreeIter iter2 = iter;
				compilerStore.Remove (ref iter2);

				if (compilerStore.IterNext (ref iter) || compilerStore.GetIterFirst (out iter))
					cmbCompilers.SetActiveIter (iter);
				else
					Load (null);
			}
		}

		protected void OnTogglebuttonMakeDefaultPressed (object sender, System.EventArgs e)
		{
			if (configuration != null && configuration.Vendor == defaultCompilerVendor)
				btnMakeDefault.Active = true;
			else
				MakeCurrentConfigDefault ();
		}

		protected void OnBtnApplyRenamingPressed (object sender, System.EventArgs e)
		{
			RenameCurrentPreset (ComboBox_CompilersLabel);
		}
		#endregion

		#region Save&Load
		public void Load (DCompilerConfiguration config)
		{
			configuration = config;

			if (config == null) {
				txtBinPath.Text =
					txtCompiler.Text =
					txtConsoleAppLinker.Text =
					txtGUIAppLinker.Text =
					txtSharedLibLinker.Text =
					txtStaticLibLinker.Text = null;

				defaultLibStore.Clear ();
				includePathStore.Clear ();

				releaseArgumentsDialog.Load (null, false);
				debugArgumentsDialog.Load (null, true);

				btnMakeDefault.Sensitive = false;
				return;
			}
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
			foreach (var p in config.ParseCache.ParsedDirectories)
				includePathStore.AppendValues (p);

			btnMakeDefault.Active = 
				configuration.Vendor == defaultCompilerVendor;
			btnMakeDefault.Sensitive = true;
		}

		public bool Validate ()
		{
			return true;
		}

		public bool Store ()
		{
			ApplyToVirtConfiguration ();

			DCompilerService.Instance.Compilers.Clear ();

			Gtk.TreeIter iter;
			compilerStore.GetIterFirst (out iter);
			do {
				var virtCmp = compilerStore.GetValue (iter, 1) as DCompilerConfiguration;
				
				DCompilerService.Instance.Compilers.Add (virtCmp);
			} while (compilerStore.IterNext(ref iter));

			DCompilerService.Instance.DefaultCompiler = defaultCompilerVendor;

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
			bool cacheUpdateRequired = paths.Count != configuration.ParseCache.ParsedDirectories.Count;

			// If there's a new directory in it
			if (!cacheUpdateRequired)
				foreach (var path in paths)
					if (!configuration.ParseCache.ParsedDirectories.Contains (path)) {
						cacheUpdateRequired = true;
						break;
					}

			if (cacheUpdateRequired) {
				configuration.ParseCache.Clear ();
				configuration.ParseCache.ParsedDirectories.AddRange(paths);

				try {
					// Update parse cache immediately
					DCompilerConfiguration.UpdateParseCacheAsync (configuration.ParseCache);
				} catch (Exception ex) {
					LoggingService.LogError ("Include path analysis error", ex);
				}
			}
			#endregion

			return true;
		}
		#endregion

		#region Setting edititing helper methods
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
				txtDefaultLib.Text = dialog.SelectedFileName;
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

		protected void OnBtnDefaultsClicked (object sender, System.EventArgs e)
		{
			if (configuration == null)
				return;

			if (!PresetLoader.HasPresetsAvailable (configuration)) {
				MessageService.ShowMessage ("No defaults available for " + configuration.Vendor);
				return;
			}

			if (MessageService.AskQuestion ("Reset current compiler preset?", AlertButton.Yes, AlertButton.No) == AlertButton.Yes && 
				PresetLoader.TryLoadPresets (configuration))
				Load (configuration);
		}
		#endregion
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
			panel.Store ();
		}
	}	
}
