using System;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using System.Linq;
using Gtk;
using System.Collections;
using MonoDevelop.D.Projects;

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

		protected void OnCheckEnableBuildCmdOverrideToggled (object sender, EventArgs e)
		{
			table_CompilingTab.Sensitive = table_LinkingTab.Sensitive = !check_EnableBuildCmdOverride.Active;
			table_CustomBuildTools.Sensitive = check_EnableBuildCmdOverride.Active;
		}
	}
	
	public class ProjectOptionsBinding : MultiConfigItemOptionsPanel
	{
		private ProjectOptions panel = new ProjectOptions();

		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel;
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
