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
	/// This is the option panel which occurs in D Project settings.
	/// </summary>
	public partial class ProjectOptions : Gtk.Bin
	{
		private DProject project;
		private DProjectConfiguration configuration;

		private Gtk.ListStore compilerStore = new Gtk.ListStore(typeof(string), typeof(DCompilerVendor));
		private Gtk.ListStore libStore = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore includePathStore = new Gtk.ListStore (typeof(string));
		
		public ProjectOptions () 
		{
			this.Build ();
			
			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			
			libTreeView.Model = libStore;
			libTreeView.HeadersVisible = false;
			libTreeView.AppendColumn ("Library", textRenderer, "text", 0);
			
			includePathTreeView.Model = includePathStore;
			includePathTreeView.HeadersVisible = false;
			includePathTreeView.AppendColumn ("Include", textRenderer, "text", 0);
			
			cmbCompiler.Clear();			
			Gtk.CellRendererText cellRenderer = new Gtk.CellRendererText();			
			cmbCompiler.PackStart(cellRenderer, false);
			cmbCompiler.AddAttribute(cellRenderer, "text", 0);

			cmbCompiler.Model = compilerStore;
            compilerStore.AppendValues("DMD", DCompilerVendor.DMD);			
            compilerStore.AppendValues("GDC", DCompilerVendor.GDC);			
            compilerStore.AppendValues("LDC", DCompilerVendor.LDC);	
			
		}
		
		public void Load (DProject proj, DProjectConfiguration config)
		{
			project = proj;
			configuration = config;
			
			cbUseDefaultCompiler.Active = proj.UseDefaultCompilerVendor;
			OnUseDefaultCompilerChanged();
			Gtk.TreeIter iter;
			if (cmbCompiler.Model.GetIterFirst (out iter)) {
				do {
					if (proj.UsedCompilerVendor == (DCompilerVendor)cmbCompiler.Model.GetValue(iter, 1)) {
						cmbCompiler.SetActiveIter(iter);
						break;
					} 
				} while (cmbCompiler.Model.IterNext (ref iter));
			}			
				
			extraCompilerTextView.Buffer.Text = config.ExtraCompilerArguments;
			extraLinkerTextView.Buffer.Text = config.ExtraLinkerArguments;			
			
			libStore.Clear();
			foreach (string lib in proj.ExtraLibraries)
				libStore.AppendValues (lib);

			includePathStore.Clear();
			includePathStore.AppendValues(project.LocalIncludeCache.DirectoryPaths);
		}
		
		private void OnIncludePathAdded (object sender, EventArgs e)
		{
			if (includePathEntry.Text.Length > 0) {				
				includePathStore.AppendValues (includePathEntry.Text);
				includePathEntry.Text = string.Empty;
			}
		}
		
		private void OnIncludePathRemoved (object sender, EventArgs e)
		{
			Gtk.TreeIter iter;
			includePathTreeView.Selection.GetSelected (out iter);
			includePathStore.Remove (ref iter);
		}
		
		private void OnLibAdded (object sender, EventArgs e)
		{
			if (libAddEntry.Text.Length > 0) {				
				libStore.AppendValues (libAddEntry.Text);
				libAddEntry.Text = string.Empty;
			}
		}
		
		private void OnLibRemoved (object sender, EventArgs e)
		{
			Gtk.TreeIter iter;
			libTreeView.Selection.GetSelected (out iter);
			libStore.Remove (ref iter);
		}
		
		private void OnBrowseButtonClick (object sender, EventArgs e)
		{
			AddLibraryDialog dialog = new AddLibraryDialog ();
			dialog.Run ();
			libAddEntry.Text = dialog.Library;
		}
		
		private void OnIncludePathBrowseButtonClick (object sender, EventArgs e)
		{			/*
			AddPathDialog dialog = new AddPathDialog (configuration.SourcePath);
			dialog.Run ();
			includePathEntry.Text = dialog.SelectedPath;*/
			
			Gtk.FileChooserDialog dialog = new Gtk.FileChooserDialog("Select D Source Folder", null, Gtk.FileChooserAction.SelectFolder, "Cancel", Gtk.ResponseType.Cancel, "Ok", Gtk.ResponseType.Ok);
			try{
				dialog.WindowPosition = Gtk.WindowPosition.Center;				
				if (dialog.Run() == (int)Gtk.ResponseType.Ok)
					includePathEntry.Text = dialog.Filename;
			}finally{
				dialog.Destroy();
			}
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			string line;
			
			// Store used compiler vendor
			project.UseDefaultCompilerVendor = cbUseDefaultCompiler.Active;
			Gtk.TreeIter iter;
			if (cmbCompiler.GetActiveIter(out iter))
				project.UsedCompilerVendor = (DCompilerVendor)cmbCompiler.Model.GetValue (iter,1);
			
			// Store args
			configuration.ExtraCompilerArguments = extraCompilerTextView.Buffer.Text;
			configuration.ExtraLinkerArguments = extraLinkerTextView.Buffer.Text;
			
			// Store libs
			libStore.GetIterFirst (out iter);
			project.ExtraLibraries.Clear();
			while (libStore.IterIsValid (iter)) {
				line = (string)libStore.GetValue (iter, 0);
				project.ExtraLibraries.Add(line);
				libStore.IterNext (ref iter);
			}
			
			// Store includes
			includePathStore.GetIterFirst (out iter);
			project.LocalIncludeCache.ParsedGlobalDictionaries.Clear();
			while (includePathStore.IterIsValid (iter)) {
				line = (string)includePathStore.GetValue (iter, 0);
				project.LocalIncludeCache.Add(line);
				includePathStore.IterNext (ref iter);
			}
			// Update internal includes list
			project.SaveLocalIncludeCacheInformation();
			// Parse local includes
			project.LocalIncludeCache.UpdateCache();
			
			return true;
		}
			
		protected virtual void OnUseDefaultCompilerChanged()
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
			panel.Load((DProject)ConfiguredProject, (DProjectConfiguration) CurrentConfiguration);
		}

		
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
