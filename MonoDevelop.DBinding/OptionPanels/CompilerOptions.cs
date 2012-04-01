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
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class CompilerOptions : Gtk.Bin
	{
		private Gtk.ListStore compilerStore = new Gtk.ListStore (typeof(string));
		private DCompilerService configuration;

		public CompilerOptions ()
		{
			this.Build ();			
			
			cmbCompiler.Clear ();			
			Gtk.CellRendererText cellRenderer = new Gtk.CellRendererText ();
			cmbCompiler.PackStart (cellRenderer, false);
			cmbCompiler.AddAttribute (cellRenderer, "text", 0);

			cmbCompiler.Model = compilerStore;

			foreach (var cmp in DCompilerService.Instance.Compilers)
				compilerStore.AppendValues (cmp.Vendor);
		}
		
		public void Load (DCompilerService config)
		{
			configuration = config;
			
			//cmbCompiler.Active = (int)config.DefaultCompiler;
			Gtk.TreeIter iter;
			cmbCompiler.Model.GetIterFirst (out iter);
			if (cmbCompiler.Model.GetIterFirst (out iter)) {
				do {
					if (config.DefaultCompiler == cmbCompiler.Model.GetValue (iter, 0) as string) {
						cmbCompiler.SetActiveIter (iter);
						break;
					}
				} while (cmbCompiler.Model.IterNext (ref iter));
			}
			
			check_EnableUFCSCompletion.Active = config.CompletionOptions.ShowUFCSItems;
		}

		public bool Validate ()
		{
			return true;
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			//configuration.DefaultCompiler = (DCompilerVendor)cmbCompiler.Active;			
			Gtk.TreeIter iter;
			if (cmbCompiler.GetActiveIter (out iter))
				configuration.DefaultCompiler = cmbCompiler.Model.GetValue (iter, 0) as string;
			
			configuration.CompletionOptions.ShowUFCSItems = check_EnableUFCSCompletion.Active;
			
			return true;
		}

	}
	
	public class CompilerOptionsBinding : OptionsPanel
	{
		private CompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new CompilerOptions ();
			panel.Load (DCompilerService.Instance);
			return panel;
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
