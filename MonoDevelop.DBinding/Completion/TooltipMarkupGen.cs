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
using D_Parser.Resolver;
using D_Parser.Dom;

namespace MonoDevelop.D.Completion
{
	public partial class TooltipMarkupGen
	{
		ColorScheme st;

		public TooltipMarkupGen(ColorScheme st)
		{
			this.st = st;
		}

		public void GenToolTipBody(DNode n, out string summary, out Dictionary<string,string> categories)
		{
			categories = null;
			summary = null;

			var desc = n.Description;
			if (!string.IsNullOrWhiteSpace(desc)) {
				categories = new Dictionary<string, string>();

				var match = ddocSectionRegex.Match (desc);

				if (!match.Success) {
					summary = DDocToMarkup(desc).Trim();
					return;
				}

				summary = DDocToMarkup (desc.Substring (0, match.Index - 1)).Trim();
				if (string.IsNullOrWhiteSpace (summary))
					summary = null;

				int k = 0;
				while((k = match.Index + match.Length) < desc.Length) {
					var nextMatch = ddocSectionRegex.Match (desc, k);
					if (nextMatch.Success) {
						AssignToCategories (categories, match.Groups ["cat"].Value, desc.Substring (k, nextMatch.Index - k));
						match = nextMatch;
					}
					else
						break;
				}

				// Handle last match
				AssignToCategories (categories, match.Groups ["cat"].Value, desc.Substring (k));
			}
		}

		void AssignToCategories(Dictionary<string,string> cats, string catName, string rawContent)
		{
			rawContent = rawContent.Trim ();
			cats [catName] = catName.ToLower ().StartsWith ("example") ? HandleExampleCode (DDocToMarkup(rawContent)) : DDocToMarkup(rawContent);
		}

		const char ExampleCodeInit='-';
		string HandleExampleCode(string categoryContent)
		{
			int i = categoryContent.IndexOf(ExampleCodeInit);
			if (i >= 0) {
				while (i < categoryContent.Length && categoryContent [i] == ExampleCodeInit)
					i++;
			} else
				i = 0;

			int lastI = categoryContent.LastIndexOf (ExampleCodeInit);
			if (lastI < i) {
				lastI = categoryContent.Length - 1;
			} else {
				while (lastI > i && categoryContent [lastI] == ExampleCodeInit)
					lastI--;
			}

			return DCodeToMarkup(categoryContent.Substring(i, lastI-i));
		}

		static System.Text.RegularExpressions.Regex ddocSectionRegex = new System.Text.RegularExpressions.Regex(
			@"^\s*(?<cat>[\w][\w\d_]*):",RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

		string DDocToMarkup(string ddoc)
		{
			if (ddoc == null)
				return string.Empty;

			var sb = new StringBuilder (ddoc.Length);
			int i = 0, len = 0;
			while (i < ddoc.Length) {

				string macroName;
				Dictionary<string, string> parameters;
				var k = i+len;

				DDocParser.FindNextMacro(ddoc, i+len, out i, out len, out macroName, out parameters);

				if (i < 0) {
					i = k;
					break;
				}

				while (k < i)
					sb.Append (ddoc [k++]);

				var firstParam = parameters != null ? parameters["$0"] : null;

				//TODO: Have proper macro infrastructure
				switch (macroName) {
					case "I":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Italic);
						break;
					case "U":
						if(firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Underline);
						break;
					case "B":
						if(firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Bold);
						break;
					case "D_CODE":
					case "D":
						if (firstParam != null)
							sb.Append(DCodeToMarkup (DDocToMarkup(firstParam)));
						break;
					case "BR":
						sb.AppendLine ();
						break;
					case "RED":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color, 1.0);
						break;
					case "BLUE":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color, 0,0,1.0);
						break;
					case "GREEN":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color, 0,1,0);
						break;
					case "YELLOW":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color, 1,1,0);
						break;
					case "BLACK":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color);
						break;
					case "WHITE":
						if (firstParam != null)
							AppendFormat (DDocToMarkup (firstParam), sb, FormatFlags.Color, 1,1,1);
						break;
					default:
						if (firstParam != null) {
							sb.Append(DDocToMarkup(firstParam));
						}
						break;
				}
			}

			while (i < ddoc.Length)
				sb.Append (ddoc [i++]);

			return sb.ToString ();
		}

		[Flags]
		protected enum FormatFlags{
			None=0,Color=1<<0,Underline=1<<1,Bold=1<<2,Italic=1<<3
		}

		protected virtual void AppendFormat(string content, StringBuilder sb, FormatFlags flags, double r=0.0, double g=0.0, double b=0.0)
		{
			if (flags == FormatFlags.None) {
				sb.Append (content);
				return;
			}

			sb.Append ("<span");

			if ((flags & FormatFlags.Bold) != 0)
				sb.Append (" weight='bold'");
			if ((flags & FormatFlags.Italic) != 0)
				sb.Append (" font_style='italic'");
			if ((flags & FormatFlags.Underline) != 0)
				sb.Append (" underline='single'");
			if ((flags & FormatFlags.Color) != 0) {
				sb.Append (string.Format (" color='#{0:x2}{1:x2}{2:x2}'", 
					(int)(r * 255.0), (int)(g * 255.0), (int)(b * 255.0)));
			}

			sb.Append ('>').Append(content).Append("</span>");
		}

		#region Pseudo-Highlighting
		//TODO: Use DLexer to walk through code and highlight tokens (also comments and meta tokens)
		static TextDocument markupDummyTextDoc = new TextDocument ();
		static DSyntaxMode markupDummySyntaxMode = new DSyntaxMode ();
		string DCodeToMarkup(string code)
		{
			//TODO: Semantic highlighting
			var sb = new StringBuilder ();
			var textDoc = markupDummyTextDoc;
			var syntaxMode = markupDummySyntaxMode;

			textDoc.Text = code;
			if(syntaxMode.Document == null)
				syntaxMode.Document = textDoc;

			var plainText = st.PlainText;

			var lineCount = textDoc.LineCount;
			for (int i = 1; i <= lineCount; i++) {
				var line = textDoc.GetLine (i);

				foreach (var chunk in syntaxMode.GetChunks (st, line, line.Offset, line.Length)) {
					var s = st.GetChunkStyle (chunk);

					// Avoid unnecessary non-highlighting
					if (s == plainText) {
						sb.Append(textDoc.GetTextAt(chunk.Offset, chunk.Length));
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

