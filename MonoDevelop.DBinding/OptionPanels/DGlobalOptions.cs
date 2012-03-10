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
	public partial class DGlobalOptions : Gtk.Bin
	{
		public DGlobalOptions ()
		{
			this.Build ();			
		}
	
		public void Load ()
		{
			text_ManualBaseUrl.Text = D.Refactoring.DDocumentationLauncher.DigitalMarsUrl;	
		}

		public bool Validate ()
		{
			return !string.IsNullOrWhiteSpace (text_ManualBaseUrl.Text);
		}
		
		public bool Store ()
		{
			Refactoring.DDocumentationLauncher.DigitalMarsUrl = text_ManualBaseUrl.Text;
			
			return true;
		}
	}
	
	public class DGlobalOptionsBinding : OptionsPanel
	{
		private DGlobalOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DGlobalOptions ();
			panel.Load ();
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
