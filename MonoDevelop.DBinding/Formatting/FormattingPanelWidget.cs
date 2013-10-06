using System;

namespace MonoDevelop.D.Formatting
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class FormattingPanelWidget : Gtk.Bin
	{
		public void Save(DFormattingPolicy policy)
		{
			if (policy == null)
				throw new ArgumentNullException("policy");

			policy.CommentOutStandardHeaders = chk_CommentOutStdHeaders.Active;
			policy.InsertStarAtCommentNewLine = chk_InsertStarAtCommentNewLine.Active;
			policy.KeepAlignmentSpaces = check_KeepAlignmentSpaces.Active;
			policy.IndentPastedCodeLines = check_IndentPastedCodeLines.Active;
		}

		public void Load(DFormattingPolicy policy)
		{
			if (policy == null)
				throw new ArgumentNullException("policy");

			chk_CommentOutStdHeaders.Active = policy.CommentOutStandardHeaders;
			chk_InsertStarAtCommentNewLine.Active = policy.InsertStarAtCommentNewLine;
			check_KeepAlignmentSpaces.Active = policy.KeepAlignmentSpaces;
			check_IndentPastedCodeLines.Active = policy.IndentPastedCodeLines;
		}

		public FormattingPanelWidget ()
		{
			this.Build ();
		}
	}
}

