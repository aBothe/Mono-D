using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using Mono.TextEditor;
using D_Parser.Dom;
using MonoDevelop.D.Parser;
using System.Threading;
using D_Parser.Resolver;
using MonoDevelop.D.Completion;
using D_Parser.Resolver.ASTScanner;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Highlighting
{
	public class TypeHighlightingExtension:TextEditorExtension
	{
		#region Properties
		Thread th;

		public IAbstractSyntaxTree SyntaxTree
		{
			get { return (Document.ParsedDocument as ParsedDModule).DDom; }
		}

		List<HighlightMarker> markers = new List<HighlightMarker>();
		#endregion

		#region Init
		public override void Initialize()
		{
			base.Initialize();

			Document.DocumentParsed += Document_DocumentParsed;
		}
		#endregion



		void Document_DocumentParsed(object sender, EventArgs e)
		{
			if (th != null && th.IsAlive)
			{
				th.Abort();
				th = null;
			}

			//TODO: Handle a storage-reparsed event, so refresh the symbols then
			

			th = new Thread(RefreshMarkers);
			th.IsBackground = true;

			th.Start();
		}

		void RefreshMarkers()
		{
			CodeSymbolsScanner.CodeScanResult res=null;
			try
			{
				var ParseCache = DCodeCompletionSupport.EnumAvailableModules(Document);

				res = CodeSymbolsScanner.ScanSymbols(new ResolverContextStack(ParseCache, new ResolverContext
				{
					ScopedBlock = SyntaxTree,
				}));

				RemoveMarkers(false);

				var txtDoc = Document.Editor.Document;

				LineSegment curLine=null;
				int ln=-1;
				foreach (var kv in res.ResolvedIdentifiers)
				{
					var id = kv.Key;

					if(ln!=id.Location.Line)
					{
						ln=id.Location.Line;
						curLine=Document.Editor.GetLine(ln);
					}

					var m = new HighlightMarker(txtDoc, curLine, "keyword.semantic.type", id.Location.Column, id.Id);
					txtDoc.AddMarker(curLine, m);
					markers.Add(m);
				}
			}
			catch
			{
				
			}
		}

		void RemoveMarkers(bool updateLine)
		{
			if (markers.Count == 0)
				return;

			var txtDoc = Document.Editor.Document;

			foreach (var m in markers)
			{
				txtDoc.RemoveMarker(m, updateLine);
			}

			markers.Clear();
		}


		public class HighlightMarker : TextMarker, IDisposable
		{
			string text;
			TextDocument doc;
			string style;
			int startColumn;
			LineSegment line;

			public HighlightMarker(TextDocument doc,LineSegment line, string style, int startColumn, string text)
			{
				this.doc = doc;
				this.line = line;
				this.style = style;
				this.startColumn = startColumn;
				this.text = text;
				doc.LineChanged += HandleDocLineChanged;
			}

			void HandleDocLineChanged(object sender, LineEventArgs e)
			{
				if (line == e.Line)
					doc.RemoveMarker(this);
			}

			public void Dispose()
			{
				doc.LineChanged -= HandleDocLineChanged;
			}

			public override void Draw(
				TextEditor editor, 
				Cairo.Context cr, 
				Pango.Layout layout, 
				bool selected, 
				int startOffset, 
				int endOffset, 
				double y, 
				double startXPos, 
				double endXPos)
			{
				int markerStart = line.Offset + startColumn - 1;
				int markerEnd = line.Offset + startColumn - 1 + text.Length;

				if (markerEnd < startOffset || markerStart > endOffset)
					return;
				
				double @from;
				double to;

				if (markerStart < startOffset && endOffset < markerEnd)
				{
					@from = startXPos;
					to = endXPos;
				}
				else
				{
					int start = startOffset < markerStart ? markerStart : startOffset;
					int end = endOffset < markerEnd ? endOffset : markerEnd;
					int x_pos = layout.IndexToPos(start - startOffset).X;

					@from = startXPos + (int)(x_pos / Pango.Scale.PangoScale);

					x_pos = layout.IndexToPos(end - startOffset).X;

					to = startXPos + (int)(x_pos / Pango.Scale.PangoScale);
				}

				@from = System.Math.Max(@from, editor.TextViewMargin.XOffset);
				to = System.Math.Max(to, editor.TextViewMargin.XOffset);
				if (@from < to)
				{
					cr.DrawLine(editor.ColorStyle.GetChunkStyle(style).CairoColor, 
						@from,
						y + editor.LineHeight,
						to,
						y + editor.LineHeight);
				}
			}
		}
	}
}
