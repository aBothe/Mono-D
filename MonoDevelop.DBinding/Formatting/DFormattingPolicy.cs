using System;
using D_Parser.Formatting;
using MonoDevelop.Core.Serialization;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D.Formatting
{	
	[PolicyType("D formatting")]
	public class DFormattingPolicy: IEquatable<DFormattingPolicy>, DFormattingOptionsFactory
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
		
		DFormattingOptions o = DFormattingOptions.CreateDStandard();
		
		public DFormattingOptions Options {
			get {
				return o;
			}
		}

		[ItemProperty]
		public bool CommentOutStandardHeaders { get; set; }
		[ItemProperty]
		public bool InsertStarAtCommentNewLine { get; set; }
		
		#region Indenting
		[ItemProperty]
		public bool IndentSwitchBody {
			get{ return o.IndentSwitchBody; }
			set{ o.IndentSwitchBody = value; }
		}
		[ItemProperty]
		public GotoLabelIndentStyle LabelIndentStyle {
			get{ return o.LabelIndentStyle; }
			set{ o.LabelIndentStyle = value; }
		}
		#endregion
	}
}
