using System;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Ide;

namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This is the option panel which occurs in D Project settings.
	/// </summary>
	public partial class ProjectOptions : Gtk.Bin
	{
		private DProject project;
		private DProjectConfiguration configuration;
		private Gtk.ListStore model_Compilers = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore model_Libraries = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore model_IncludePaths = new Gtk.ListStore (typeof(string));
		Gtk.ListStore model_compileTarget = new Gtk.ListStore (typeof(string), typeof(DCompileTarget));
		
		public ProjectOptions ()
		{
			this.Build ();
			
			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			
			libTreeView.Model = model_Libraries;
			libTreeView.HeadersVisible = false;
			libTreeView.AppendColumn ("Library", textRenderer, "text", 0);
			
			includePathTreeView.Model = model_IncludePaths;
			includePathTreeView.HeadersVisible = false;
			includePathTreeView.AppendColumn ("Path", textRenderer, "text", 0);

			cmbCompiler.Clear ();
			Gtk.CellRendererText cellRenderer = new Gtk.CellRendererText ();			
			cmbCompiler.PackStart (cellRenderer, false);
			cmbCompiler.AddAttribute (cellRenderer, "text", 0);

			cmbCompiler.Model = model_Compilers;

			foreach (var cmp in DCompilerService.Instance.Compilers)
				model_Compilers.AppendValues (cmp.Vendor);
			
			combo_ProjectType.Model = model_compileTarget;
			
			model_compileTarget.AppendValues ("Consoleless executable", DCompileTarget.ConsolelessExecutable);
			model_compileTarget.AppendValues ("Executable", DCompileTarget.Executable);
			model_compileTarget.AppendValues ("Shared library", DCompileTarget.SharedLibrary);
			model_compileTarget.AppendValues ("Static library", DCompileTarget.StaticLibrary);
		}
		
		public void Load (DProject proj, DProjectConfiguration config)
		{
			project = proj;
			configuration = config;
			
			cbUseDefaultCompiler.Active = proj.UseDefaultCompilerVendor;
			cbPreferOneStepCompilation.Active = proj.PreferOneStepBuild;
			
			OnUseDefaultCompilerChanged ();
			Gtk.TreeIter iter;
			if (cmbCompiler.Model.GetIterFirst (out iter))
				do {
					if (proj.UsedCompilerVendor == cmbCompiler.Model.GetValue (iter, 0) as string) {
						cmbCompiler.SetActiveIter (iter);
						break;
					} 
				} while (cmbCompiler.Model.IterNext (ref iter));
			
			extraCompilerTextView.Buffer.Text = config.ExtraCompilerArguments;
			extraLinkerTextView.Buffer.Text = config.ExtraLinkerArguments;
			
			text_BinDirectory.Text = config.OutputDirectory;
			text_TargetFile.Text = config.Output;
			text_ObjectsDirectory.Text = config.ObjectDirectory;
			
			if (model_compileTarget.GetIterFirst (out iter))
				do {
					if (proj.CompileTarget == (DCompileTarget)model_compileTarget.GetValue (iter, 1)) {
						combo_ProjectType.SetActiveIter (iter);
						break;
					} 
				} while (model_compileTarget.IterNext (ref iter));
			
			model_Libraries.Clear ();
			foreach (string lib in proj.ExtraLibraries)
				model_Libraries.AppendValues (lib);

			model_IncludePaths.Clear ();
			foreach (var p in project.LocalIncludeCache.ParsedDirectories)
				model_IncludePaths.AppendValues (p);
		}
		
		private void OnIncludePathAdded (object sender, EventArgs e)
		{
			if (includePathEntry.Text.Length > 0) {				
				model_IncludePaths.AppendValues (includePathEntry.Text);
				includePathEntry.Text = string.Empty;
			}
		}
		
		private void OnIncludePathRemoved (object sender, EventArgs e)
		{
			Gtk.TreeIter iter;
			includePathTreeView.Selection.GetSelected (out iter);
			model_IncludePaths.Remove (ref iter);
		}
		
		private void OnLibAdded (object sender, EventArgs e)
		{
			if (libAddEntry.Text.Length > 0) {				
				model_Libraries.AppendValues (libAddEntry.Text);
				libAddEntry.Text = string.Empty;
			}
		}
		
		private void OnLibRemoved (object sender, EventArgs e)
		{
			Gtk.TreeIter iter;
			libTreeView.Selection.GetSelected (out iter);
			model_Libraries.Remove (ref iter);
		}
		
		private void OnBrowseLibButtonClick (object sender, EventArgs e)
		{
			AddLibraryDialog dialog = new AddLibraryDialog (AddLibraryDialog.FileFilterType.LibraryFiles)
			{
				TransientFor = Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};

			dialog.Run ();
			libAddEntry.Text = dialog.SelectedFileName;
		}
		
		private void OnIncludePathBrowseButtonClick (object sender, EventArgs e)
		{
			var dialog = new Gtk.FileChooserDialog ("Select D Source Folder", null, Gtk.FileChooserAction.SelectFolder, "Cancel", Gtk.ResponseType.Cancel, "Ok", Gtk.ResponseType.Ok)
			{
				TransientFor = Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};
			try {
				if (dialog.Run () == (int)Gtk.ResponseType.Ok)
					includePathEntry.Text = dialog.Filename;
			} finally {
				dialog.Destroy ();
			}
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			string line;
			
			// Store used compiler vendor
			project.UseDefaultCompilerVendor = cbUseDefaultCompiler.Active;
			project.PreferOneStepBuild = cbPreferOneStepCompilation.Active;
			
			Gtk.TreeIter iter;
			if (cmbCompiler.GetActiveIter (out iter))
				project.UsedCompilerVendor = cmbCompiler.Model.GetValue (iter, 0) as string;
			
			// Store args
			configuration.ExtraCompilerArguments = extraCompilerTextView.Buffer.Text;
			configuration.ExtraLinkerArguments = extraLinkerTextView.Buffer.Text;
			
			configuration.OutputDirectory = text_BinDirectory.Text;
			configuration.Output = text_TargetFile.Text;
			configuration.ObjectDirectory = text_ObjectsDirectory.Text;
			
			if (combo_ProjectType.GetActiveIter (out iter))
				project.CompileTarget = (DCompileTarget)model_compileTarget.GetValue (iter, 1);
			
			// Store libs
			model_Libraries.GetIterFirst (out iter);
			project.ExtraLibraries.Clear ();
			while (model_Libraries.IterIsValid (iter)) {
				line = (string)model_Libraries.GetValue (iter, 0);
				project.ExtraLibraries.Add (line);
				model_Libraries.IterNext (ref iter);
			}
			
			// Store includes
			model_IncludePaths.GetIterFirst (out iter);
			project.LocalIncludeCache.ParsedDirectories.Clear ();
			while (model_IncludePaths.IterIsValid (iter)) {
				line = (string)model_IncludePaths.GetValue (iter, 0);
				project.LocalIncludeCache.ParsedDirectories.Add (line);
				model_IncludePaths.IterNext (ref iter);
			}

			// Parse local includes
			DCompilerConfiguration.UpdateParseCacheAsync (project.LocalIncludeCache);
			
			return true;
		}
			
		protected virtual void OnUseDefaultCompilerChanged ()
		{
			cmbCompiler.Sensitive = (!cbUseDefaultCompiler.Active);			
		}
				
		protected void cbUseDefaultCompiler_Clicked (object sender, System.EventArgs e)
		{
			OnUseDefaultCompilerChanged ();
		}
		
		protected virtual void OnLibAddEntryChanged (object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty (libAddEntry.Text))
				addLibButton.Sensitive = false;
			else
				addLibButton.Sensitive = true;
		}

		protected virtual void OnLibTreeViewCursorChanged (object sender, System.EventArgs e)
		{
			removeLibButton.Sensitive = true;
		}

		protected virtual void OnRemoveLibButtonClicked (object sender, System.EventArgs e)
		{
			removeLibButton.Sensitive = false;
		}

		protected virtual void OnIncludePathEntryChanged (object sender, System.EventArgs e)
		{
			if (string.IsNullOrEmpty (includePathEntry.Text))
				includePathAddButton.Sensitive = false;
			else
				includePathAddButton.Sensitive = true;
		}

		protected virtual void OnIncludePathTreeViewCursorChanged (object sender, System.EventArgs e)
		{
			includePathRemoveButton.Sensitive = true;
		}

		protected virtual void OnIncludePathRemoveButtonClicked (object sender, System.EventArgs e)
		{
			includePathRemoveButton.Sensitive = false;
		}
		
		protected virtual void OnLibAddEntryActivated (object sender, System.EventArgs e)
		{
			OnLibAdded (this, new EventArgs ());
		}

		protected virtual void OnIncludePathEntryActivated (object sender, System.EventArgs e)
		{
			OnIncludePathAdded (this, new EventArgs ());
		}
	}
	
	public class ProjectOptionsBinding : MultiConfigItemOptionsPanel
	{
		private ProjectOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel = new ProjectOptions ();
		}
		
		public override void LoadConfigData ()
		{
			panel.Load ((DProject)ConfiguredProject, (DProjectConfiguration)CurrentConfiguration);
		}
		
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
