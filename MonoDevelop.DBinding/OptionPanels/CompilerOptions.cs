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
		private DCompiler configuration;

		public CompilerOptions () 
		{
			this.Build ();			
		}
		
		public void Load (DCompiler config)
		{
			configuration = config;
			
			cmbCompiler.Active = (int)config.DefaultCompiler;
		}


		public bool Validate()
		{
			return true;
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			configuration.DefaultCompiler = (DCompilerVendor)cmbCompiler.Active;			
			return true;
		}

	}
	
	public class CompilerOptionsBinding : OptionsPanel
	{
		private CompilerOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new CompilerOptions ();
			panel.Load(DCompiler.Instance);
			return panel;
		}

		public override bool ValidateChanges()
		{
			return panel.Validate();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
