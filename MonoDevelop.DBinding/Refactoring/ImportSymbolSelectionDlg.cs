using System;
using Gtk;
using D_Parser.Dom;

namespace MonoDevelop.D
{
	public partial class ImportSymbolSelectionDlg : Dialog
	{
		public ImportSymbolSelectionDlg (INode[] nodes)
		{
			this.Build ();

			SetResponseSensitive(ResponseType.Ok, true);
			SetResponseSensitive(ResponseType.Cancel, true);

			buttonOk.GrabFocus();
			Modal = true;
			WindowPosition = Gtk.WindowPosition.CenterOnParent;

			// Init name column
			var nameCol = new TreeViewColumn();
			var textRenderer = new CellRendererText();
			nameCol.PackStart(textRenderer, true);
			nameCol.AddAttribute(textRenderer, "text", 0);
			list.AppendColumn(nameCol);

			// Init list model
			var nodeStore = new ListStore(typeof(string),typeof(INode));
			list.Model = nodeStore;

			// Fill list
			foreach (var n in nodes)
				if(n!=null)
					nodeStore.AppendValues(n.ToString(), n);

			// Select first result
			TreeIter iter;
			if(nodeStore.GetIterFirst(out iter))
				list.Selection.SelectIter(iter);
		}

		public INode SelectedNode
		{
			get{
				TreeIter iter;
				if(!list.Selection.GetSelected(out iter))
					return null;

				return (INode)list.Model.GetValue(iter, 1);
			}
		}
	
		[GLib.ConnectBefore]
		protected void OnListButtonPressEvent (object o, ButtonPressEventArgs args)
		{
			// Handle double-click
			if (args.Event.Type == Gdk.EventType.TwoButtonPress)
			{
				Respond(ResponseType.Ok);
				args.RetVal = true;
				Hide();
			}
		}

		protected void OnButtonCancelClicked (object sender, EventArgs e)
		{
			Respond(ResponseType.Cancel);
			Hide();
		}

		protected void OnButtonOkClicked (object sender, EventArgs e)
		{
			Respond(ResponseType.Ok);
			Hide();
		}

		protected void OnKeyPressEvent(object o, KeyPressEventArgs e)
		{
			if (e.Event.Key == Gdk.Key.Return)
			{
				Respond(ResponseType.Ok);
				Hide();
			}
		}
	}
}

