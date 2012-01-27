using System;

namespace MonoDevelop.D.OptionPanels
{
	public partial class EditCompilerName : Gtk.Dialog
	{
		public EditCompilerName ()
		{
			this.Build ();
		}
		
		
		
		public string PresetName
		{ 
			get { return txtPresetName.Text.Trim (); } 
			set { txtPresetName.Text = value; }
		}
	}
}

