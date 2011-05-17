using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D
{
	public class DFormatter: AbstractAdvancedFormatter
	{
		public override void CorrectIndenting(PolicyContainer policyParent, IEnumerable<string> mimeTypeChain, Mono.TextEditor.TextEditorData data, int line)
		{
			base.CorrectIndenting(policyParent, mimeTypeChain, data, line);
		}

		public override bool SupportsCorrectingIndent
		{
			get
			{
				return true;
			}
		}

		public override string FormatText(PolicyContainer policyParent, IEnumerable<string> mimeTypeChain, string input, int startOffset, int endOffset)
		{
			return input;
		}
	}
}
