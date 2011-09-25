using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Gui.Dialogs;

namespace MonoDevelop.D.OptionPanels
{
	
	public partial class DGlobalOptions : Gtk.Bin
	{
		private DProjectConfiguration configuration;
		
		public DGlobalOptions () 
		{
			this.Build ();
			
		}
		
		public void Load (DProjectConfiguration config)
		{
			configuration = config;
			
		}
		
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			return true;
		}

	}
	
	public class DGlobalOptionsBinding : OptionsPanel
	{
		private DGlobalOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel = new DGlobalOptions ();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
