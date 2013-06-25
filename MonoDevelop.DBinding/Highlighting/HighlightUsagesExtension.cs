using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using Mono.TextEditor;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.D.Parser;
using MonoDevelop.D.Refactoring;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide.Gui.Content;
using ICSharpCode.NRefactory.Editor;
using MonoDevelop.D.Projects;

// Code taken and modified from MonoDevelop.CSharp.Highlighting.HighlightUsagesExtension.cs

namespace MonoDevelop.D.Highlighting
{
	class HighlightUsagesExtension:TextEditorExtension
	{
		TextEditorData textEditorData;
		public DModule SyntaxTree
		{
			get { return (Document.ParsedDocument as ParsedDModule).DDom; }
		}

		#region Init & Doc edit events
		public override void Initialize()
		{
			base.Initialize();

			textEditorData = base.Document.Editor;
			textEditorData.Caret.PositionChanged += HandleTextEditorDataCaretPositionChanged;
			textEditorData.Document.TextReplaced += HandleTextEditorDataDocumentTextReplaced;
			textEditorData.SelectionChanged += HandleTextEditorDataSelectionChanged;
		}

		void HandleTextEditorDataSelectionChanged(object sender, EventArgs e)
		{
			RemoveMarkers();
		}

		void HandleTextEditorDataDocumentTextReplaced(object sender, DocumentChangeEventArgs e)
		{
			RemoveMarkers();
		}

		public override void Dispose()
		{
			textEditorData.SelectionChanged -= HandleTextEditorDataSelectionChanged;
			textEditorData.Caret.PositionChanged -= HandleTextEditorDataCaretPositionChanged;
			textEditorData.Document.TextReplaced -= HandleTextEditorDataDocumentTextReplaced;
			base.Dispose();
			RemoveTimer();
		}

		uint popupTimer = 0;

		public bool IsTimerOnQueue
		{
			get
			{
				return popupTimer != 0;
			}
		}

		public void ForceUpdate()
		{
			RemoveTimer();
			UpdateMarkers();
		}

		void RemoveTimer()
		{
			if (popupTimer != 0)
			{
				GLib.Source.Remove(popupTimer);
				popupTimer = 0;
			}
		}

		void HandleTextEditorDataCaretPositionChanged(object sender, DocumentLocationEventArgs e)
		{
			if (!MonoDevelop.Core.PropertyService.Get<bool>("EnableHighlightUsages"))
				return;
			if (!textEditorData.IsSomethingSelected && markers.Values.Any(m => m.Contains(textEditorData.Caret.Offset)))
				return;
			RemoveMarkers();
			RemoveTimer();
			if (!textEditorData.IsSomethingSelected)
				popupTimer = GLib.Timeout.Add(1000, UpdateMarkers);
		}
		#endregion

		#region Marker management
		Dictionary<int, UsageMarker> markers = new Dictionary<int, UsageMarker>();

		public Dictionary<int, UsageMarker> Markers
		{
			get { return this.markers; }
		}

		void RemoveMarkers()
		{
			if (markers.Count == 0)
				return;
			textEditorData.Parent.TextViewMargin.AlphaBlendSearchResults = false;
			foreach (var pair in markers)
			{
				textEditorData.Document.RemoveMarker(pair.Value, true);
			}
			markers.Clear();
		}

		UsageMarker GetMarker(int line)
		{
			UsageMarker result;
			if (!markers.TryGetValue(line, out result))
			{
				result = new UsageMarker();
				textEditorData.Document.AddMarker(line, result);
				markers.Add(line, result);
			}
			return result;
		}
		#endregion

		public class UsageMarker : TextLineMarker, IBackgroundMarker
		{
			List<TextSegment> usages = new List<TextSegment>();

			public List<TextSegment> Usages
			{
				get { return this.usages; }
			}

			public bool Contains(int offset)
			{
				return usages.Any(u => u.Offset <= offset && offset <= u.EndOffset);
			}
			/*
			public bool DrawBackground (TextEditor editor, Cairo.Context cr, double y, LineMetrics metrics, ref bool drawBg)
			{
				return DrawBackground (editor, cr, metrics.Layout, metrics.SelectionStart, metrics.SelectionEnd, metrics.TextStartOffset, metrics.TextEndOffset, y, metrics.TextRenderStartPosition, metrics.TextRenderEndPosition, ref drawBg);
			}*/

			public bool DrawBackground(TextEditor editor, Cairo.Context cr, TextViewMargin.LayoutWrapper layout, int selectionStart, int selectionEnd, int startOffset, int endOffset, double y, double startXPos, double endXPos, ref bool drawBg)
			{
				drawBg = false;
				
				var color_Bg = (HslColor)editor.ColorStyle.UsagesRectangle.GetColor("secondcolor");
				var color_Rect = (HslColor)editor.ColorStyle.UsagesRectangle.GetColor("color");

				if (selectionStart >= 0 || editor.CurrentMode is TextLinkEditMode || editor.TextViewMargin.SearchResultMatchCount > 0)
					return true;
				foreach (var usage in Usages)
				{
					int markerStart = usage.Offset;
					int markerEnd = usage.EndOffset;

					if (markerEnd < startOffset || markerStart > endOffset)
						return true;

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

						uint curIndex = 0, byteIndex = 0;
						TextViewMargin.TranslateToUTF8Index(layout.LineChars, (uint)(start - startOffset), ref curIndex, ref byteIndex);

						int x_pos = layout.Layout.IndexToPos((int)byteIndex).X;

						@from = startXPos + (int)(x_pos / Pango.Scale.PangoScale);

						TextViewMargin.TranslateToUTF8Index(layout.LineChars, (uint)(end - startOffset), ref curIndex, ref byteIndex);
						x_pos = layout.Layout.IndexToPos((int)byteIndex).X;

						to = startXPos + (int)(x_pos / Pango.Scale.PangoScale);
					}

					@from = System.Math.Max(@from, editor.TextViewMargin.XOffset);
					to = System.Math.Max(to, editor.TextViewMargin.XOffset);
					if (@from < to)
					{
						cr.Color = color_Bg;
						cr.Rectangle(@from + 1, y + 1, to - @from - 1, editor.LineHeight - 2);
						cr.Fill();

						cr.Color = color_Rect;
						cr.Rectangle(@from + 0.5, y + 0.5, to - @from, editor.LineHeight - 1);
						cr.Stroke();
					}
				}
				return true;
			}
		}


		#region Main functionality

		bool UpdateMarkers()
		{
			try
			{
				var dom = SyntaxTree;

				if (dom == null)
					return false;

				ResolutionContext ctxt;
				var rr = DResolverWrapper.ResolveHoveredCode(out ctxt, Document);

				if (rr == null || rr.Length < 1)
					return false;

				var parseCache = Document.HasProject ?
						(Document.Project as AbstractDProject).ParseCache :
						ParseCacheList.Create( DCompilerService.Instance.GetDefaultCompiler().ParseCache);

				var mr = rr[0] as DSymbol;

				if (mr == null)
					return false;

				var referencedNode = mr.Definition;

				// Slightly hacky: To keep highlighting the id of e.g. a NewExpression, take the ctor's parent node (i.e. the class node)
				if (referencedNode is DMethod && ((DMethod)referencedNode).SpecialType == DMethod.MethodType.Constructor)
				{
					mr = mr.Base as DSymbol;
					referencedNode = mr.Definition;
				}
				try
				{
					var references = D_Parser.Refactoring.ReferencesFinder.Scan(dom, referencedNode, ctxt).ToList();

					if (references.Count > 0)
						ShowReferences(references);
				}
				catch (Exception ex)
				{
					LoggingService.LogWarning("Error during usage highlighting analysis", ex);
				}
			}
			catch (Exception ex)
			{
				LoggingService.LogDebug("Error while highlighting symbol usages", ex);
			}
			return false;
		}


		void ShowReferences(List<ISyntaxRegion> references)
		{
			RemoveMarkers();
			HashSet<int> lineNumbers = new HashSet<int>();
			if (references != null)
			{
				int nameLength = references[0].EndLocation.Column - references[0].Location.Column;

				bool alphaBlend = false;
				foreach (var r in references)
				{
					var loc = r is AbstractTypeDeclaration ? ((AbstractTypeDeclaration)r).NonInnerTypeDependendLocation : r.Location;

					var marker = GetMarker(loc.Line);
					int offset = textEditorData.Document.LocationToOffset(loc.Line, loc.Column);

					if (!alphaBlend && textEditorData.Parent.TextViewMargin.SearchResults.Any(sr => 
							sr.Contains(offset) || 
							sr.Contains(offset + nameLength) ||
							offset < sr.Offset && sr.EndOffset < offset + nameLength))
					{
						textEditorData.Parent.TextViewMargin.AlphaBlendSearchResults = alphaBlend = true;
					}
					
					marker.Usages.Add(new TextSegment(offset, nameLength));
					lineNumbers.Add(loc.Line);
				}
			}
			foreach (int line in lineNumbers)
				textEditorData.Document.CommitLineUpdate(line);
		}

		#endregion
	}
}
