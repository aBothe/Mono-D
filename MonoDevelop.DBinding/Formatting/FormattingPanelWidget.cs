using System;

namespace MonoDevelop.D.Formatting
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class FormattingPanelWidget : Gtk.Bin
	{
		public DFormattingPolicy Policy {
			get {
				var p = new DFormattingPolicy ();
				
				p.CommentOutStandardHeaders = chk_CommentOutStdHeaders.Active;
				p.InsertStarAtCommentNewLine = chk_InsertStarAtCommentNewLine.Active;
				
				return p;
			}
			set {
				chk_CommentOutStdHeaders.Active = value.CommentOutStandardHeaders;
				chk_InsertStarAtCommentNewLine.Active = value.InsertStarAtCommentNewLine;
			}
		}
		
		public FormattingPanelWidget ()
		{
			this.Build ();
		}
	}
}

