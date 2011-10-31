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
	public partial class DGenericOptions : Gtk.Bin
	{
		private DProjectConfiguration configuration;

		private Gtk.ListStore libStore = new Gtk.ListStore (typeof(string));
		private Gtk.ListStore includePathStore = new Gtk.ListStore (typeof(string));
		
		public DGenericOptions () 
		{
			this.Build ();
			
			Gtk.CellRendererText textRenderer = new Gtk.CellRendererText ();
			
			libTreeView.Model = libStore;
			libTreeView.HeadersVisible = false;
			libTreeView.AppendColumn ("Library", textRenderer, "text", 0);
			
			includePathTreeView.Model = includePathStore;
			includePathTreeView.HeadersVisible = false;
			includePathTreeView.AppendColumn ("Include", textRenderer, "text", 0);
		}
		
		public void Load (DProjectConfiguration config)
		{
			configuration = config;
			
			cmbCompiler.Active = (int)config.Compiler;
		
			extraCompilerTextView.Buffer.Text = config.ExtraCompilerArguments;
			extraLinkerTextView.Buffer.Text = config.ExtraLinkerArguments;			
			
			foreach (string lib in config.Libs)
				libStore.AppendValues (lib);
			
			foreach (string includePath in config.Includes)
				includePathStore.AppendValues (includePath);
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
		{			
			AddPathDialog dialog = new AddPathDialog (configuration.SourcePath);
			dialog.Run ();
			includePathEntry.Text = dialog.SelectedPath;
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			string line;
			Gtk.TreeIter iter;
			
			configuration.Compiler = (DCompilerVendor)cmbCompiler.Active;
			configuration.ExtraCompilerArguments = extraCompilerTextView.Buffer.Text;
			configuration.ExtraLinkerArguments = extraLinkerTextView.Buffer.Text;
			
			libStore.GetIterFirst (out iter);
			configuration.Libs.Clear();
			while (libStore.IterIsValid (iter)) {
				line = (string)libStore.GetValue (iter, 0);
				configuration.Libs.Add (line);
				libStore.IterNext (ref iter);
			}
			
			includePathStore.GetIterFirst (out iter);
			configuration.Includes.Clear ();
			while (includePathStore.IterIsValid (iter)) {
				line = (string)includePathStore.GetValue (iter, 0);
				configuration.Includes.Add (line);
				includePathStore.IterNext (ref iter);
			}
			
			return true;
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
	
	public class DGenericOptionsBinding : MultiConfigItemOptionsPanel
	{
		private DGenericOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel = new DGenericOptions ();
		}
		
		public override void LoadConfigData ()
		{
			panel.Load((DProjectConfiguration) CurrentConfiguration);
		}

		
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
