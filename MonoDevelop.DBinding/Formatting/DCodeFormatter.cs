using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Projects.Policies;
using D_Parser.Formatting;

namespace MonoDevelop.D.Formatting
{
	public class DCodeFormatter: AbstractAdvancedFormatter
	{
		public static readonly DFormatter NativeFormatterInstance=new DFormatter();

		public override bool SupportsOnTheFlyFormatting	{	get	{return true;}	}

		// CorrectIndenting is completely unused in the entire MonoDevelopment code environment - doesn't have to be implemented

		/// <summary>
		/// Used for format selected code
		/// </summary>
		public override void OnTheFlyFormat(PolicyContainer policyParent, IEnumerable<string> mimeTypeChain, Mono.TextEditor.TextEditorData data, int startOffset, int endOffset)
		{
			
		}

		/// <summary>
		/// Used for formatting the entire document
		/// </summary>
		public override string FormatText(PolicyContainer policyParent, IEnumerable<string> mimeTypeChain, string input, int startOffset, int endOffset)
		{
			return input;
		}
	}
}
