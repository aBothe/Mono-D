using System;
using MonoDevelop.Core.Serialization;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D.Formatting
{
	public enum GotoLabelIndentStyle {
		///<summary>Place goto labels in the leftmost column</summary>
		LeftJustify,
		
		/// <summary>
		/// Place goto labels one indent less than current
		/// </summary>
		OneLess,
		
		/// <summary>
		/// Indent goto labels normally
		/// </summary>
		Normal
	}
	
	[PolicyType("D formatting")]
	public class DFormattingPolicy: IEquatable<DFormattingPolicy>
	{
		public DFormattingPolicy()
		{
			//TODO: Generate default attributes or so
			CommentOutStandardHeaders = true;
			InsertStarAtCommentNewLine = true;
			IndentSwitchBody = true;
			LabelIndentStyle = GotoLabelIndentStyle.OneLess;
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
		
		#region Indenting
		[ItemProperty]
		public bool IndentSwitchBody {get;set;}
		[ItemProperty]
		public GotoLabelIndentStyle LabelIndentStyle {get;set;}
		#endregion
	}
}
