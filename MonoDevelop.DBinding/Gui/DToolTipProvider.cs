using System;
using Mono.TextEditor;

namespace MonoDevelop.D.Gui
{
	/// <summary>
	/// Description of DToolTipProvider.
	/// </summary>
	public class DToolTipProvider:ITooltipProvider
	{
		public DToolTipProvider()
		{
		}
		
		public TooltipItem GetItem(TextEditor editor, int offset)
		{
			throw new NotImplementedException();
		}
		
		public Gtk.Window CreateTooltipWindow(TextEditor editor, int offset, Gdk.ModifierType modifierState, TooltipItem item)
		{
			throw new NotImplementedException();
		}
		
		public void GetRequiredPosition(TextEditor editor, Gtk.Window tipWindow, out int requiredWidth, out double xalign)
		{
			throw new NotImplementedException();
		}
		
		public bool IsInteractive(TextEditor editor, Gtk.Window tipWindow)
		{
			throw new NotImplementedException();
		}
	}
}
