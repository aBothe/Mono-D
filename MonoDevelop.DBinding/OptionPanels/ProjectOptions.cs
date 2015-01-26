using System;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Projects;
using System.Collections.Generic;

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
		private Gtk.ListStore model_Platforms = new Gtk.ListStore(typeof(string));
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
			combo_Platform.Model = model_Platforms;
			
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
			cbIsUnittestConfig.Active = config.UnittestMode;
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

			check_LinkThirdPartyLibs.Active = configuration.LinkinThirdPartyLibraries;
			
			text_BinDirectory.Text = proj.GetRelativeChildPath(config.OutputDirectory);
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

			model_Platforms.Clear();
			var blackListed = new List<string>();
			foreach (var cfg in proj.Configurations)
				if (cfg.Name == config.Name && cfg.Platform != config.Platform)
					blackListed.Add(cfg.Platform.ToLower());

			var platform_lower = config.Platform.ToLower();
			foreach (var platform in proj.SupportedPlatforms)
			{
				// Skip already taken platforms
				if(blackListed.Contains(platform.ToLower()))
					continue;

				var it = model_Platforms.Append();
				if (platform_lower == platform.ToLower())
					combo_Platform.SetActiveIter(it);
				model_Platforms.SetValue(it, 0, platform);
			}
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			// Store used compiler vendor
			project.UseDefaultCompilerVendor = cbUseDefaultCompiler.Active;
			configuration.UnittestMode = cbIsUnittestConfig.Active;
			project.PreferOneStepBuild = cbPreferOneStepCompilation.Active;
			
			Gtk.TreeIter iter;
			if (cmbCompiler.GetActiveIter (out iter))
				project.UsedCompilerVendor = cmbCompiler.Model.GetValue (iter, 0) as string;
			
			// Store args
			int oldHash = configuration.GetHashCode ();
			configuration.ExtraCompilerArguments = extraCompilerTextView.Buffer.Text;
			configuration.ExtraLinkerArguments = extraLinkerTextView.Buffer.Text;

			configuration.LinkinThirdPartyLibraries = check_LinkThirdPartyLibs.Active;

			configuration.OutputDirectory = text_BinDirectory.Text;
			configuration.Output = text_TargetFile.Text;
			configuration.ObjectDirectory = text_ObjectsDirectory.Text;
			configuration.DDocDirectory = text_DDocDir.Text;
			
			if (combo_ProjectType.GetActiveIter (out iter))
				configuration.CompileTarget = (DCompileTarget)model_compileTarget.GetValue (iter, 1);

			configuration.DebugLevel = (ulong)spin_debugLevel.ValueAsInt;
			configuration.CustomDebugIdentifiers = text_debugConstants.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			configuration.CustomVersionIdentifiers = text_versionConstants.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			configuration.UpdateGlobalVersionIdentifiers(project);
			
			// Store libs
			configuration.ExtraLibraries.Clear ();
			foreach (var p in text_Libraries.Buffer.Text.Split('\n'))
			{
				var p_ = p.Trim();
				if (!String.IsNullOrWhiteSpace(p_))
					configuration.ExtraLibraries.Add(p_);
			}

			combo_Platform.GetActiveIter(out iter);
			var oldPlatform = configuration.Platform;
			configuration.Platform = model_Platforms.GetValue(iter, 0) as string;
			// Update solution configuration <-> Project configuration mapping
			if (oldPlatform != configuration.Platform)
			{
				var slnConfig = project.ParentSolution.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration);
				var en = slnConfig.GetEntryForItem(project);
				if (en != null)
				{
					slnConfig.RemoveItem(project);
					var newEn = slnConfig.AddItem(project);
					newEn.ItemConfiguration = configuration.Id;
					newEn.Build = en.Build;
					newEn.Deploy = en.Deploy;
				}
			}

			if (oldHash != configuration.GetHashCode () && 
				Ide.IdeApp.Workspace.ActiveConfigurationId == configuration.Id) {
				project.NeedsFullRebuild = true;
			}

			return true;
		}
		
		protected virtual void OnUseDefaultCompilerChanged ()
		{
			cmbCompiler.Sensitive = (!cbUseDefaultCompiler.Active);	
		}
				
		protected void cbUseDefaultCompiler_Clicked (object sender, EventArgs e)
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
