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
		public static void GenToolTipBody(DNode n, ColorScheme st, out string summary, out Dictionary<string,string> categories)
		{
			categories = null;
			summary = null;

			var desc = n.Description;
			if (!string.IsNullOrWhiteSpace(desc)) {
				categories = new Dictionary<string, string>();

				var match = ddocSectionRegex.Match (desc);

				if (!match.Success) {
					summary = DDocToMarkup(st, desc).Trim();
					return;
				}

				summary = DDocToMarkup (st, desc.Substring (0, match.Index - 1)).Trim();
				if (string.IsNullOrWhiteSpace (summary))
					summary = null;

				int k = 0;
				while((k = match.Index + match.Length) < desc.Length) {
					var nextMatch = ddocSectionRegex.Match (desc, k);
					if (nextMatch.Success) {
						AssignToCategories (st, categories, match.Groups ["cat"].Value, desc.Substring (k, nextMatch.Index - k));
						match = nextMatch;
					}
					else
						break;
				}

				// Handle last match
				AssignToCategories (st, categories, match.Groups ["cat"].Value, desc.Substring (k));
			}
		}

		static void AssignToCategories(ColorScheme st,Dictionary<string,string> cats, string catName, string rawContent)
		{
			rawContent = rawContent.Trim ();

			if (catName.ToLower ().StartsWith ("example"))
				cats [catName] = HandleExampleCode (st, rawContent);
			else
				cats [catName] = DDocToMarkup(st,rawContent);
		}

		const char ExampleCodeInit='-';
		static string HandleExampleCode(ColorScheme st,string categoryContent)
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

			return DCodeToMarkup(st, categoryContent.Substring(i, lastI-i));
		}

		private static System.Text.RegularExpressions.Regex ddocSectionRegex = new System.Text.RegularExpressions.Regex(
			@"^\s*(?<cat>[\w][\w\d_]*):",RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

		static string DDocToMarkup(ColorScheme st, string ddoc)
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
						if(firstParam != null)
							sb.Append ("<i>").Append(firstParam).Append("</i>");
						break;
					case "U":
						if(firstParam != null)
							sb.Append ("<u>").Append(firstParam).Append("</u>");
						break;
					case "B":
						if(firstParam != null)
							sb.Append ("<b>").Append(firstParam).Append("</b>");
						break;
					case "D_CODE":
					case "D":
						if (firstParam != null)
							sb.Append(DCodeToMarkup (st, firstParam));
						break;
					case "BR":
						sb.AppendLine ();
						break;
					case "RED":
						if (firstParam != null)
							sb.Append("<span color=\"red\">").Append(firstParam).Append("</span>");
						break;
					case "BLUE":
						if (firstParam != null)
							sb.Append("<span color=\"blue\">").Append(firstParam).Append("</span>");
						break;
					case "GREEN":
						if (firstParam != null)
							sb.Append("<span color=\"green\">").Append(firstParam).Append("</span>");
						break;
					case "YELLOW":
						if (firstParam != null)
							sb.Append("<span color=\"yellow\">").Append(firstParam).Append("</span>");
						break;
					case "BLACK":
						if (firstParam != null)
							sb.Append("<span color=\"black\">").Append(firstParam).Append("</span>");
						break;
					case "WHITE":
						if (firstParam != null)
							sb.Append("<span color=\"white\">").Append(firstParam).Append("</span>");
						break;
					default:
						if (firstParam != null) {
							sb.Append (firstParam);
						}
						break;
				}
			}

			while (i < ddoc.Length)
				sb.Append (ddoc [i++]);

			return sb.ToString ();
		}

		#region Pseudo-Highlighting
		//TODO: Use DLexer to walk through code and highlight tokens (also comments and meta tokens)
		static TextDocument markupDummyTextDoc = new TextDocument ();
		static DSyntaxMode markupDummySyntaxMode = new DSyntaxMode ();
		static string DCodeToMarkup(ColorScheme st,string code)
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
					//TODO: Apply Underline/weight/other styles?
					sb.Append (string.Format("<span color=\"#{0:x2}{1:x2}{2:x2}\">", (int)(col.R * 255.0), (int)(col.G * 255.0), (int)(col.B * 255.0)));
					sb.Append(textDoc.GetTextAt(chunk.Offset, chunk.Length));
					sb.Append ("</span>");
				}

				if (i < lineCount)
					sb.AppendLine ();
			}

			return sb.ToString ();
		}
		#endregion
	}
}

