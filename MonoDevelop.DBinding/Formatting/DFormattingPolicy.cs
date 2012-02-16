using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Policies;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Core.Serialization;

namespace MonoDevelop.D.Formatting
{
	[PolicyType("D formatting")]
	public class DFormattingPolicy: IEquatable<DFormattingPolicy>
	{
		public DFormattingPolicy()
		{
			CommentOutStandardHeaders = true;
			InsertStarAtCommentNewLine = true;
		}

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

		[ItemProperty]
		public bool CommentOutStandardHeaders { get; set; }
		[ItemProperty]
		public bool InsertStarAtCommentNewLine { get; set; }
	}
}
