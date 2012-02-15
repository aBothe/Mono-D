using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Policies;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting
{
	[PolicyType("D formatting")]
	public class DFormattingPolicy: IEquatable<DFormattingPolicy>
	{
		public bool Equals (DFormattingPolicy other)
		{
			return base.Equals (other);
		}
		
		public DFormattingPolicy Clone ()
		{
			var p = new DFormattingPolicy ();
			
			p.CommentOutStandardHeaders = CommentOutStandardHeaders;
			p.InsertStarAtCommentNewLine = InsertStarAtCommentNewLine;
			
			return p;
		}
		
		public bool CommentOutStandardHeaders = true;
		public bool InsertStarAtCommentNewLine = true;
	}
}
