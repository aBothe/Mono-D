using System;
using MonoDevelop.D.Building;
using MonoDevelop.Core;
using MonoDevelop.D.Projects;
using D_Parser.Misc;
using System.Linq;

namespace MonoDevelop.D
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class ProjectIncludesWidget : Gtk.Bin
	{
		public readonly DProject Project;
		public readonly DProjectConfiguration CurrentConfig;
		public ProjectIncludesWidget (DProject prj, DProjectConfiguration cfg)
		{
			this.Build ();

			Project = prj;
			CurrentConfig = cfg;
		}

		public void Load()
		{
			text_Includes.Buffer.Text = string.Join ("\n", Project.LocalIncludes);
		}

		public void Store()
		{
			var paths = text_Includes.Buffer.Text.Split (new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
			
			// Remove trailing / and \
			for (int i = 0; i < paths.Length; i++)
				paths[i] = paths[i].TrimEnd('\\','/');
			
			// Handle removed items
			foreach (var p in Project.LocalIncludes.Except(paths))
				GlobalParseCache.RemoveRoot (p);

			// Handle new items
			foreach (var p in paths.Except(Project.LocalIncludes))
				Project.LocalIncludes.Add (p);
			
			try {
				// Update parse cache immediately
				Project.UpdateLocalIncludeCache();
			} catch (Exception ex) {
				LoggingService.LogError ("Include path analysis error", ex);
			}
		}

		protected void OnButtonAddIncludeClicked(object sender, System.EventArgs e)
		{
			var dialog = new Gtk.FileChooserDialog(
				"Select D Source Folder",
				Ide.IdeApp.Workbench.RootWindow,
				Gtk.FileChooserAction.SelectFolder,
				"Cancel",
				Gtk.ResponseType.Cancel,
				"Ok",
				Gtk.ResponseType.Ok)
			{
				TransientFor = Toplevel as Gtk.Window,
				WindowPosition = Gtk.WindowPosition.Center
			};

			try
			{
				if (dialog.Run() == (int)Gtk.ResponseType.Ok)
				{
					text_Includes.Buffer.Text += (text_Includes.Buffer.CharCount == 0 ? "" : "\n") + string.Join("\n", dialog.Filenames);
				}
			}
			finally
			{
				dialog.Destroy();
			}
		}
	}
}

