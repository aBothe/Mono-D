using System;
using D_Parser.Formatting;
using D_Parser.Formatting.Indent;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting.Indentation
{
	public class DIndentEngine : IndentEngine, IDocumentStateEngine, DFormattingOptionsFactory
	{
		DFormattingPolicy policy;
		TextStylePolicy textStyle;
		
		public DIndentEngine(DFormattingPolicy policy, TextStylePolicy textStyle)
			: base(policy.Options, textStyle.TabsToSpaces, textStyle.IndentWidth, policy.KeepAlignmentSpaces)
		{
			this.policy = policy;
			this.textStyle = textStyle;
		}
		
		protected override IndentEngine Construct()
		{
			return new DIndentEngine(policy, textStyle);
		}
	}
}
