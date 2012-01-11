using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Dialogs;
using Gtk;

namespace MonoDevelop.D.Formatting
{
	class FormattingPanel : MimeTypePolicyOptionsPanel<DFormattingPolicy>
	{
		FormattingPanelWidget panel;
		
		public override Widget CreatePanelWidget ()
		{
			return panel = new FormattingPanelWidget ();
		}
		
		protected override void LoadFrom (DFormattingPolicy policy)
		{
			panel.policy = policy.Clone ();
		}
		
		protected override DFormattingPolicy GetPolicy ()
		{
			return panel.policy;
		}
	}
}
