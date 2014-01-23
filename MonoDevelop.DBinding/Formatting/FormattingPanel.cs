using MonoDevelop.Ide.Gui.Dialogs;
using Gtk;

namespace MonoDevelop.D.Formatting
{
	class FormattingPanel : MimeTypePolicyOptionsPanel<DFormattingPolicy>
	{
		FormattingPanelWidget panel;
		DFormattingPolicy pol;

		public override Widget CreatePanelWidget ()
		{
			return panel = new FormattingPanelWidget ();
		}
		
		protected override void LoadFrom (DFormattingPolicy policy)
		{
			panel.Load(pol = policy);
		}
		
		protected override DFormattingPolicy GetPolicy ()
		{
			if (pol == null)
				pol = new DFormattingPolicy();

			panel.Save(pol);
			return pol;
		}
	}
}
