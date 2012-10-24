using System;
using MonoDevelop.D.Building;
using System.Collections.Generic;
using Gtk;

namespace MonoDevelop.D.OptionPanels
{
	public partial class BuildArgumentOptions : Gtk.Dialog
	{		
		public bool IsDebug{ get; private set; }

		Gtk.ListStore model_compileTarget = new Gtk.ListStore (typeof(string), typeof(DCompileTarget));
		Dictionary<DCompileTarget,BuildConfiguration> argsStore = new Dictionary<DCompileTarget, BuildConfiguration> (4);
		BuildConfiguration _currArgCfg;
		
		public BuildArgumentOptions ()
		{
			this.Build ();
			
			combo_SelectedBuildTarget.Model = model_compileTarget;
			model_compileTarget.AppendValues ("Consoleless executable", DCompileTarget.ConsolelessExecutable);
			model_compileTarget.AppendValues ("Executable", DCompileTarget.Executable);
			model_compileTarget.AppendValues ("Shared library", DCompileTarget.SharedLibrary);
			model_compileTarget.AppendValues ("Static library", DCompileTarget.StaticLibrary);
		}
		
		public DCompileTarget SelectedCompileTarget {
			get {
				TreeIter i;
				
				if (combo_SelectedBuildTarget.GetActiveIter (out i))
					return (DCompileTarget)combo_SelectedBuildTarget.Model.GetValue (i, 1);
				return DCompileTarget.ConsolelessExecutable;
			}
			set {
				TreeIter i;
				
				if (combo_SelectedBuildTarget.Model.GetIterFirst (out i))
					do {
						if ((DCompileTarget)combo_SelectedBuildTarget.Model.GetValue (i, 1) == value) {
							combo_SelectedBuildTarget.SetActiveIter (i);
							Load (argsStore [value]);
							break;
						}
					} while(combo_SelectedBuildTarget.Model.IterNext(ref i));
			}
		}

		public DCompilerConfiguration Configuration{ get; set; }
		
		protected override void OnShown ()
		{
			base.OnShown ();
			
			this.Title = (IsDebug ? "Debug" : "Release") + " build arguments";					
		}
		
		void Load (BuildConfiguration bc)
		{
			if ((_currArgCfg = bc) == null) {
				text_CompilerArguments.Text = text_LinkerArguments.Text = text_OneStepBuildArguments.Text = "";
				return;
			}
			
			text_CompilerArguments.Text = bc.CompilerArguments;
			text_LinkerArguments.Text = bc.LinkerArguments;
			text_OneStepBuildArguments.Text = bc.OneStepBuildArguments;
			check_enableLibPrefixing.Active = bc.EnableGDCLibPrefixing;
		}
		
		void SaveToDict ()
		{
			if (_currArgCfg != null) {
				_currArgCfg.CompilerArguments = text_CompilerArguments.Text;
				_currArgCfg.LinkerArguments = text_LinkerArguments.Text;
				_currArgCfg.OneStepBuildArguments = text_OneStepBuildArguments.Text;
				_currArgCfg.EnableGDCLibPrefixing = check_enableLibPrefixing.Active;
			}
		}
		
		public void Load (DCompilerConfiguration config, bool isDebug)
		{
			Configuration = config;
			IsDebug = isDebug;

			if (config == null) {
				Load (null);
				return;
			}
			
			//compiler targets
			argsStore [DCompileTarget.ConsolelessExecutable] = config
					.GetOrCreateTargetConfiguration (DCompileTarget.ConsolelessExecutable)
					.GetArguments (isDebug)
					.Clone ();
			
			argsStore [DCompileTarget.Executable] = config
					.GetOrCreateTargetConfiguration (DCompileTarget.Executable)
					.GetArguments (isDebug)
					.Clone ();
			
			argsStore [DCompileTarget.SharedLibrary] = config
					.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary)
					.GetArguments (isDebug)
					.Clone ();
			
			argsStore [DCompileTarget.StaticLibrary] = config
					.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary)
					.GetArguments (isDebug)
					.Clone ();
			
			SelectedCompileTarget = DCompileTarget.ConsolelessExecutable;
		}
		
		public void Store ()
		{
			if (Configuration == null)			
				return;
			
			SaveToDict ();
			
			foreach (var kv in argsStore) {
				var ltc = Configuration.GetOrCreateTargetConfiguration (kv.Key);
				
				if (IsDebug)
					ltc.DebugArguments = kv.Value;
				else
					ltc.ReleaseArguments = kv.Value;
			}
		}

		protected void buttonOk_Clicked (object sender, System.EventArgs e)
		{
			Hide ();
		}

		protected void OnButtonCancelClicked (object sender, System.EventArgs e)
		{
			Hide ();
		}

		protected void OnComboSelectedBuildTargetChanged (object sender, System.EventArgs e)
		{
			SaveToDict ();
			Load (argsStore [SelectedCompileTarget]);
		}
	}
}

