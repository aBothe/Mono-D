//
// DietTemplateSyntaxMode.cs
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
using Mono.TextEditor.Highlighting;
using System.Collections.Generic;
using System.Linq;
using Mono.TextEditor;

namespace MonoDevelop.D.Highlighting
{
	public class DietTemplateSyntaxMode : SyntaxMode
	{
		static SyntaxMode baseMode;

		public DietTemplateSyntaxMode ()
		{
			var matches = new List<Match>();

			if (baseMode == null)
			{
				var provider = new ResourceStreamProvider(
					typeof(DietTemplateSyntaxMode).Assembly,
					typeof(DietTemplateSyntaxMode).Assembly.GetManifestResourceNames().First(s => s.Contains("DietTemplateSyntaxDefinition")));
				using (var s = provider.Open())
					baseMode = SyntaxMode.Read(s);
			}

			this.rules = new List<Rule>(baseMode.Rules);
			this.keywords = new List<Keywords>(baseMode.Keywords);
			this.spans = new List<Span>(baseMode.Spans.Where(span => span.Begin.Pattern != "#")).ToArray();
			matches.AddRange(baseMode.Matches);
			this.prevMarker = baseMode.PrevMarker;
			this.SemanticRules = new List<SemanticRule>(baseMode.SemanticRules);
			this.keywordTable = baseMode.keywordTable;
			this.keywordTableIgnoreCase = baseMode.keywordTableIgnoreCase;
			this.properties = baseMode.Properties;

			// D Number literals
			matches.Add(DSyntaxMode.workaroundMatchCtor(
				"Number"			
				, @"				(?<!\w)(0((x|X)[0-9a-fA-F_]+|(b|B)[0-1_]+)|([0-9]+[_0-9]*)[L|U|u|f|i]*)"));

			this.matches = matches.ToArray();
		}

		class InlineDSemRule : SemanticRule
		{
			private bool inUpdate = false;

			public override void Analyze (TextDocument doc, DocumentLine line, Chunk startChunk, int startOffset, int endOffset)
			{
				/*if (endOffset > startOffset && startOffset < doc.TextLength && !this.inUpdate)
				{
					this.inUpdate = true;
					try
					{
						string textAt = doc.GetTextAt (startOffset, Math.Min (endOffset, doc.TextLength) - startOffset);
						int num = startOffset - line.Offset;
						List<UrlMarker> list = new List<UrlMarker> ((from m in line.Markers
							where m is UrlMarker
							select m).Cast<UrlMarker> ());
						list.ForEach (delegate (UrlMarker m)
							{
								doc.RemoveMarker (m, false);
							});
						IEnumerator enumerator = HighlightUrlSemanticRule.UrlRegex.Matches (textAt).GetEnumerator ();
						try
						{
							while (enumerator.MoveNext ())
							{
								Match match = (Match)enumerator.get_Current ();
								doc.AddMarker (line, new UrlMarker (doc, line, match.Value, UrlType.Url, this.syntax, num + match.Index, num + match.Index + match.Length), false);
							}
						}
						finally
						{
							IDisposable disposable;
							if ((disposable = (enumerator as IDisposable)) != null)
							{
								disposable.Dispose ();
							}
						}
						IEnumerator enumerator2 = HighlightUrlSemanticRule.MailRegex.Matches (textAt).GetEnumerator ();
						try
						{
							while (enumerator2.MoveNext ())
							{
								Match match2 = (Match)enumerator2.get_Current ();
								doc.AddMarker (line, new UrlMarker (doc, line, match2.Value, UrlType.Email, this.syntax, num + match2.Index, num + match2.Index + match2.Length), false);
							}
						}
						finally
						{
							IDisposable disposable2;
							if ((disposable2 = (enumerator2 as IDisposable)) != null)
							{
								disposable2.Dispose ();
							}
						}
					}
					finally
					{
						this.inUpdate = false;
					}
				}*/
			}
		}
	}
}

