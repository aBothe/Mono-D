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
			SemanticRules.Add(InlineDHighlighting.Instance);
			this.keywordTable = baseMode.keywordTable;
			this.keywordTableIgnoreCase = baseMode.keywordTableIgnoreCase;
			this.properties = baseMode.Properties;

			// D Number literals
			matches.Add(DSyntaxMode.workaroundMatchCtor(
				"Number"			
				, @"				(?<!\w)(0((x|X)[0-9a-fA-F_]+|(b|B)[0-1_]+)|([0-9]+[_0-9]*)[L|U|u|f|i]*)"));

			this.matches = matches.ToArray();
		}

		class InlineDHighlighting : SemanticRule
		{
			public readonly static InlineDHighlighting Instance = new InlineDHighlighting();
			public readonly DSyntaxMode DSyntax = new DSyntaxMode();

			public override void Analyze(TextDocument doc, DocumentLine line, Chunk startChunk, int startOffset, int endOffset)
			{
				// Check line start
				int o = line.Offset;
				char c = '\0';
				for (; o < line.EndOffset && char.IsWhiteSpace(c = doc.GetCharAt(o)); o++) ;

				if (c != '-' && c != '#')
					return;

				DSyntax.Document = doc;
				var spanParser = new SpanParser(DSyntax, new CloneableStack<Span>());
				var chunkP = new ChunkParser(DSyntax, spanParser, Ide.IdeApp.Workbench.ActiveDocument.Editor.ColorStyle, line);

				var n = chunkP.GetChunks(startOffset, endOffset - startOffset);
				if (n == null)
					return;
				startChunk.Next = n;
				startChunk.Length = n.Offset - startChunk.Offset;
			}
		}
	}
}

