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
				virtCopy.AssignFrom (cmp);
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

				text_DefaultLibraries.Buffer.Clear ();
				text_Includes.Buffer.Clear ();

				releaseArgumentsDialog.Load (null, false);
				debugArgumentsDialog.Load (null, true);

				btnMakeDefault.Sensitive = false;
				return;
			}
			//for now, using Executable target compiler command for all targets source compiling
			LinkTargetConfiguration targetConfig;
			targetConfig = config.GetOrCreateTargetConfiguration (DCompileTarget.Executable);
			
			txtBinPath.Text = config.BinPath;
			
			txtCompiler.Text = targetConfig.Compiler;
			
			//linker targets 			
			targetConfig = config.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 						
			txtConsoleAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = config.GetOrCreateTargetConfiguration (DCompileTarget.ConsolelessExecutable); 						
			txtGUIAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = config.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary); 						
			txtSharedLibLinker.Text = targetConfig.Linker;
			
			targetConfig = config.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary); 						
			txtStaticLibLinker.Text = targetConfig.Linker;
			
			releaseArgumentsDialog.Load (config, false);		
			debugArgumentsDialog.Load (config, true);				

			text_DefaultLibraries.Buffer.Text = string.Join ("\n", config.DefaultLibraries);
			text_Includes.Buffer.Text = string.Join ("\n", config.ParseCache.ParsedDirectories);

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
			
			configuration.BinPath = txtBinPath.Text;
			
			//for now, using Executable target compiler command for all targets source compiling
			LinkTargetConfiguration targetConfig;
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 			
			targetConfig.Compiler = txtCompiler.Text;
			
			//linker targets 			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 						
			targetConfig.Linker = txtConsoleAppLinker.Text;			
			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.ConsolelessExecutable); 						
			targetConfig.Linker = txtGUIAppLinker.Text;			
			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary); 						
			targetConfig.Linker = txtSharedLibLinker.Text;
			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary); 						
			targetConfig.Linker = txtStaticLibLinker.Text;
			
			releaseArgumentsDialog.Store ();			
			debugArgumentsDialog.Store ();					
			
			configuration.DefaultLibraries.Clear ();
			configuration.DefaultLibraries.AddRange (text_DefaultLibraries.Buffer.Text.Split (new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries));
			
			#region Store new include paths
			var paths = text_Includes.Buffer.Text.Split (new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);

			// Remove trailing / and \
			for (int i = 0; i < paths.Length; i++)
				paths[i] = paths[i].TrimEnd('\\', '/');

			if (configuration.ParseCache.UpdateRequired (paths)) {
				configuration.ParseCache.ParsedDirectories.Clear ();
				configuration.ParseCache.ParsedDirectories.AddRange (paths);

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
		
		protected void OnButtonAddIncludeClicked (object sender, System.EventArgs e)
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
                    text_Includes.Buffer.Text += (text_Includes.Buffer.CharCount == 0 ? "" : "\n") + string.Join("\n", dialog.Filenames);
			} finally {
				dialog.Destroy ();
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
