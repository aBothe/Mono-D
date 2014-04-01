//
// TooltipMarkupGen.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Mono.TextEditor.Highlighting;
using D_Parser.Parser;
using Mono.TextEditor;
using MonoDevelop.D.Highlighting;
using D_Parser.Dom;
using D_Parser.Completion.ToolTips;

namespace MonoDevelop.D.Completion
{
	public partial class TooltipMarkupGen : NodeTooltipRepresentationGen
	{
		ColorScheme st;

		public TooltipMarkupGen (ColorScheme st)
		{
			this.st = st;
		}
		
		#region Pseudo-Highlighting

		//TODO: Use DLexer to walk through code and highlight tokens (also comments and meta tokens)
		static TextDocument markupDummyTextDoc = new TextDocument ();
		static DSyntaxMode markupDummySyntaxMode = new DSyntaxMode ();

		public override string DCodeToMarkup(string code)
		{
			//TODO: Semantic highlighting
			var sb = new StringBuilder ();
			var textDoc = markupDummyTextDoc;
			var syntaxMode = markupDummySyntaxMode;

			textDoc.Text = code;
			if (syntaxMode.Document == null)
				syntaxMode.Document = textDoc;

			var plainText = st.PlainText;

			var lineCount = textDoc.LineCount;
			for (int i = 1; i <= lineCount; i++) {
				var line = textDoc.GetLine (i);

				foreach (var chunk in syntaxMode.GetChunks (st, line, line.Offset, line.Length)) {
					var s = st.GetChunkStyle (chunk);

					// Avoid unnecessary non-highlighting
					if (s == plainText) {
						sb.Append (textDoc.GetTextAt (chunk.Offset, chunk.Length));
						continue;
					}

					var col = st.GetForeground (s);
					// TODO: Have other format flags applied?
					AppendFormat (textDoc.GetTextAt (chunk.Offset, chunk.Length), sb, FormatFlags.Color, col.R, col.G, col.B);
				}

				if (i < lineCount)
					sb.AppendLine ();
			}

			return sb.ToString ();
		}

		#endregion
	}
}

