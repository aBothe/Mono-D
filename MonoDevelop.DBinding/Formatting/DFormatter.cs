using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D.Formatting
{
	public class DFormatter: AbstractAdvancedFormatter
	{
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
