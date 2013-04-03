using System;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using System.Linq;
using Gtk;
using System.Collections;

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
		Gtk.ListStore model_compileTarget = new Gtk.ListStore (typeof(string), typeof(DCompileTarget));

		public ProjectOptions ()
		{
			this.Build ();

			// Init compiler selection dropdown
			cmbCompiler.Clear ();
			Gtk.CellRendererText cellRenderer = new Gtk.CellRendererText ();			
			cmbCompiler.PackStart (cellRenderer, false);
			cmbCompiler.AddAttribute (cellRenderer, "text", 0);

			cmbCompiler.Model = model_Compilers;
			
			foreach (var cmp in DCompilerService.Instance.Compilers)
				model_Compilers.AppendValues (cmp.Vendor);
			
			combo_ProjectType.Model = model_compileTarget;
			
			// Init compile target checkbox
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
			text_DDocDir.Text = config.DDocDirectory;
			
			if(config.CustomDebugIdentifiers==null)
				text_debugConstants.Text = "";
			else
				text_debugConstants.Text = string.Join(";",config.CustomDebugIdentifiers);
			if(config.CustomVersionIdentifiers == null)
				text_versionConstants.Text = "";
			else
				text_versionConstants.Text = string.Join(";", config.CustomVersionIdentifiers);
			spin_debugLevel.Value = (double)config.DebugLevel;

			// Disable debug-specific fields on non-debug configurations
			text_debugConstants.Sensitive = spin_debugLevel.Sensitive = config.DebugMode;
			
			if (model_compileTarget.GetIterFirst (out iter))
				do {
					if (config.CompileTarget == (DCompileTarget)model_compileTarget.GetValue (iter, 1)) {
						combo_ProjectType.SetActiveIter (iter);
						break;
					} 
				} while (model_compileTarget.IterNext (ref iter));
			
			text_Libraries.Buffer.Text = string.Join ("\n", config.ExtraLibraries);
			text_Includes.Buffer.Text = string.Join ("\n", proj.LocalIncludeCache.ParsedDirectories);

			// Remove old children list
			var depsChildren = ((ArrayList)vbox_ProjectDeps.AllChildren);
			for (int k = depsChildren.Count - 1; k >= 0; k--)
				vbox_ProjectDeps.Remove((Widget)depsChildren[k]);

			// Init new project dep list
			int i = 0;
			foreach(var prj in proj.ParentSolution.GetAllProjects())
			{
				if (prj == proj)
					continue;

				var cb = new Gtk.CheckButton(prj.Name){
					CanFocus=true,
					DrawIndicator=true,
					UseUnderline=false,
					Active = proj.ProjectDependencies.Contains(prj.ItemId)
				};

				cb.Data.Add("prj", prj);

				vbox_ProjectDeps.Add(cb);
				
				var bc=(Box.BoxChild)vbox_ProjectDeps[cb];
				bc.Expand=false;
				bc.Fill=false;
				bc.Position=i++;
			}
			vbox_ProjectDeps.ShowAll();
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
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
			configuration.DDocDirectory = text_DDocDir.Text;
			
			if (combo_ProjectType.GetActiveIter (out iter))
				configuration.CompileTarget = (DCompileTarget)model_compileTarget.GetValue (iter, 1);

			configuration.CustomDebugIdentifiers = text_debugConstants.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			configuration.CustomVersionIdentifiers = text_versionConstants.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			configuration.UpdateGlobalVersionIdentifiers(project);
			
			// Store libs
			configuration.ExtraLibraries.Clear ();
			configuration.ExtraLibraries.AddRange (text_Libraries.Buffer.Text.Split (new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries));
			
			// Store includes
			#region Store new include paths
			var paths = text_Includes.Buffer.Text.Split (new[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);

			// Remove trailing / and \
			for (int i = 0; i < paths.Length; i++)
				paths[i] = paths[i].TrimEnd('\\','/');

			if (project.LocalIncludeCache.UpdateRequired (paths)) {
				project.LocalIncludeCache.ParsedDirectories.Clear ();
				project.LocalIncludeCache.ParsedDirectories.AddRange (paths);

				try {
					// Update parse cache immediately
					DCompilerConfiguration.UpdateParseCacheAsync (project.LocalIncludeCache);
				} catch (Exception ex) {
					LoggingService.LogError ("Include path analysis error", ex);
				}
			}
			#endregion

			// Store project deps
			project.ProjectDependencies.Clear();
			foreach (var i in vbox_ProjectDeps)
			{
				var cb = i as CheckButton;

				if (cb == null || !cb.Active)
					continue;

				var prj = cb.Data["prj"] as Project;
				if(prj!=null)
					project.ProjectDependencies.Add(prj.ItemId);
			}
			
			return true;
		}
		
		protected void OnButtonAddIncludeClicked (object sender, System.EventArgs e)
		{
			var dialog = new Gtk.FileChooserDialog (
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
                if (dialog.Run() == (int)Gtk.ResponseType.Ok)
                {
                    text_Includes.Buffer.Text += (text_Includes.Buffer.CharCount==0?"":"\n") + string.Join("\n", dialog.Filenames);
                }
			} finally {
				dialog.Destroy ();
			}
		}
			
		protected virtual void OnUseDefaultCompilerChanged ()
		{
			cmbCompiler.Sensitive = (!cbUseDefaultCompiler.Active);	
		}
				
		protected void cbUseDefaultCompiler_Clicked (object sender, System.EventArgs e)
		{
			OnUseDefaultCompilerChanged ();
		}

		protected void OnCheckEnableBuildCmdOverrideToggled (object sender, EventArgs e)
		{
			table_CompilingTab.Sensitive = table_LinkingTab.Sensitive = !check_EnableBuildCmdOverride.Active;
			table_CustomBuildTools.Sensitive = check_EnableBuildCmdOverride.Active;
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
			if(ConfiguredProject is DProject && CurrentConfiguration is DProjectConfiguration)
				panel.Load (ConfiguredProject as DProject, CurrentConfiguration as DProjectConfiguration);
		}
		
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
