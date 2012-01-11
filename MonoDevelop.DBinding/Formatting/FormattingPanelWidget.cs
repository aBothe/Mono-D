using System;
using MonoDevelop.D.Formatting;

namespace MonoDevelop.D
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class FormattingPanelWidget : Gtk.Bin
	{
		public DFormattingPolicy policy;
		
		public FormattingPanelWidget ()
		{
			this.Build ();
		}
	}
}

