using System;
using System.Linq;

using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.Ide;
using Gtk;
using MonoDevelop.D.Building.CompilerPresets;
using System.IO;
using D_Parser.Misc;

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

			var textRenderer = new CellRendererText ();
			
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
				var iter = compilerStore.AppendValues (cmp.Vendor, virtCopy);
				if (cmp.Vendor == defaultCompilerVendor)
					cmbCompilers.SetActiveIter(iter);
			}
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

		void MakeCurrentConfigDefault ()
		{
			if (configuration != null) {
				defaultCompilerVendor = configuration.Vendor;
				btnMakeDefault.Active = true;
			}
		}

		protected void OnTogglebuttonMakeDefaultPressed (object sender, System.EventArgs e)
		{
			if (configuration != null && configuration.Vendor == defaultCompilerVendor)
				btnMakeDefault.Active = true;
			else
				MakeCurrentConfigDefault ();
		}
		#endregion

		#region Save&Load
		public void Load (DCompilerConfiguration compiler)
		{
			configuration = compiler;

			if (compiler == null) {
				txtBinPath.Text =
					txtCompiler.Text =
					txtConsoleAppLinker.Text =
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
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.Executable);
			
			txtBinPath.Text = compiler.BinPath;
			txtCompiler.Text = compiler.SourceCompilerCommand;
			check_enableLibPrefixing.Active = compiler.EnableGDCLibPrefixing;
			
			//linker targets 			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 						
			txtConsoleAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary); 						
			txtSharedLibLinker.Text = targetConfig.Linker;
			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary); 						
			txtStaticLibLinker.Text = targetConfig.Linker;
			
			releaseArgumentsDialog.Load (compiler, false);		
			debugArgumentsDialog.Load (compiler, true);				

			text_DefaultLibraries.Buffer.Text = string.Join ("\n", compiler.DefaultLibraries);
			text_Includes.Buffer.Text = string.Join ("\n", compiler.IncludePaths);

			btnMakeDefault.Active = 
				configuration.Vendor == defaultCompilerVendor;
			btnMakeDefault.Sensitive = true;
		}

		public bool Validate ()
		{
			return true; //TODO: Establish validation
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
			configuration.SourceCompilerCommand = txtCompiler.Text;
			configuration.EnableGDCLibPrefixing = check_enableLibPrefixing.Active;
			
			var targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 			
			targetConfig.Linker = txtConsoleAppLinker.Text;
			
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

			// Handle removed items
			foreach (var p in configuration.IncludePaths.Except(paths))
				GlobalParseCache.RemoveRoot (p);

			// Handle new items
			foreach (var p in paths.Except(configuration.IncludePaths))
				configuration.IncludePaths.Add (p);

			try {
				// Update parse cache immediately
				configuration.UpdateParseCacheAsync();
			} catch (Exception ex) {
				LoggingService.LogError ("Include path analysis error", ex);
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

		string lastDir;
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

			if (lastDir != null)
				dialog.SetCurrentFolder(lastDir);
			else if (Directory.Exists(txtBinPath.Text))
				dialog.SetCurrentFolder(txtBinPath.Text);

			try {
				if (dialog.Run() == (int)Gtk.ResponseType.Ok)
				{
					lastDir = dialog.Filename;
					text_Includes.Buffer.Text += (text_Includes.Buffer.CharCount == 0 ? "" : "\n") + dialog.Filename;
				}
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
