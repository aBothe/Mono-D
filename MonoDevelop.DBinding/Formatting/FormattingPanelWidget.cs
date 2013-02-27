using System;

namespace MonoDevelop.D.Formatting
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class FormattingPanelWidget : Gtk.Bin
	{
		public void Save(DFormattingPolicy p)
		{
			if (p == null)
				throw new ArgumentNullException("policy");

			p.CommentOutStandardHeaders = chk_CommentOutStdHeaders.Active;
			p.InsertStarAtCommentNewLine = chk_InsertStarAtCommentNewLine.Active;
			p.KeepAlignmentSpaces = check_KeepAlignmentSpaces.Active;
		}

		public void Load(DFormattingPolicy p)
		{
			if (p == null)
				throw new ArgumentNullException("policy");

			chk_CommentOutStdHeaders.Active = p.CommentOutStandardHeaders;
			chk_InsertStarAtCommentNewLine.Active = p.InsertStarAtCommentNewLine;
			check_KeepAlignmentSpaces.Active = p.KeepAlignmentSpaces;
		}

		public FormattingPanelWidget ()
		{
			this.Build ();
		}
	}
}

