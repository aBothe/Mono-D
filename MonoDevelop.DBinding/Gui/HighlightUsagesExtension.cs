using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects.Dom.Parser;
using Mono.TextEditor;
using MonoDevelop.D.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using MonoDevelop.D.Building;
using MonoDevelop.D.Resolver;
using MonoDevelop.Core;

// Code taken and modified from MonoDevelop.CSharp.Highlighting.HighlightUsagesExtension.cs

namespace MonoDevelop.D.Gui
{
	class HighlightUsagesExtension:TextEditorExtension
	{
		TextEditorData textEditorData;
		public IAbstractSyntaxTree SyntaxTree
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
			RemoveMarkers(false);
		}

		void HandleTextEditorDataDocumentTextReplaced(object sender, ReplaceEventArgs e)
		{
			RemoveMarkers(false);
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
			/*SourceEditor.DefaultSourceEditorOptions.Instance.EnableHighlightUsages*/
				return;
			if (!textEditorData.IsSomethingSelected && markers.Values.Any(m => m.Contains(textEditorData.Caret.Offset)))
				return;
			RemoveMarkers(textEditorData.IsSomethingSelected);
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

		void RemoveMarkers(bool updateLine)
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

		public class UsageMarker : TextMarker, IBackgroundMarker
		{
			List<ISegment> usages = new List<ISegment>();

			public List<ISegment> Usages
			{
				get { return this.usages; }
			}

			public bool Contains(int offset)
			{
				return usages.Any(u => u.Offset <= offset && offset <= u.EndOffset);
			}

			public bool DrawBackground(TextEditor editor, Cairo.Context cr, TextViewMargin.LayoutWrapper layout, int selectionStart, int selectionEnd, int startOffset, int endOffset, double y, double startXPos, double endXPos, ref bool drawBg)
			{
				drawBg = false;
				if (selectionStart >= 0 || editor.CurrentMode is TextLinkEditMode)
					return true;
				foreach (ISegment usage in Usages)
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
						cr.Color = (HslColor)editor.ColorStyle.BracketHighlightRectangle.BackgroundColor;
						cr.Rectangle(@from + 1, y + 1, to - @from - 1, editor.LineHeight - 2);
						cr.Fill();

						cr.Color = (HslColor)editor.ColorStyle.BracketHighlightRectangle.Color;
						cr.Rectangle(@from, y, to - @from, editor.LineHeight - 1);
						cr.Fill();
					}
				}
				return true;
			}
		}
		#endregion

		#region Main functionality

		bool UpdateMarkers()
		{
			try
			{
				var dom = SyntaxTree;

				if (dom == null)
					return false;

				ResolverContext ctxt;
				var rr = DResolverWrapper.ResolveHoveredCode(out ctxt, Document);

				if (rr == null || rr.Length < 1)
					return false;

				var parseCache = Document.HasProject ?
						(Document.Project as DProject).ParseCache :
						DCompilerService.Instance.GetDefaultCompiler().GlobalParseCache.ParseCache;

				var referencedNode = DResolver.GetResultMember(rr[0]);

				if (referencedNode == null)
					return false;

				var references = Refactoring.DReferenceFinder.ScanNodeReferencesInModule(dom,
							parseCache,
							DResolver.ResolveImports(dom as DModule, parseCache),
							referencedNode);

				if (referencedNode.NodeRoot is IAbstractSyntaxTree &&
					(referencedNode.NodeRoot as IAbstractSyntaxTree).FileName == dom.FileName)
					references.Add(new IdentifierDeclaration(referencedNode.Name)
					{
						Location = referencedNode.NameLocation,
						EndLocation = new CodeLocation(referencedNode.NameLocation.Column + referencedNode.Name.Length, referencedNode.NameLocation.Line)
					});

				if (references.Count > 0)
					ShowReferences(references);
			}
			catch (Exception ex)
			{
				LoggingService.LogDebug("Error while highlighting symbol usages", ex);
			}
			return false;
		}


		void ShowReferences(List<IdentifierDeclaration> references)
		{
			RemoveMarkers(false);
			HashSet<int> lineNumbers = new HashSet<int>();
			if (references != null)
			{
				int nameLength = references[0].EndLocation.Column - references[0].Location.Column;

				bool alphaBlend = false;
				foreach (var r in references)
				{
					var loc = r.NonInnerTypeDependendLocation;

					var marker = GetMarker(loc.Line);
					int offset = textEditorData.Document.LocationToOffset(loc.Line, loc.Column);

					if (!alphaBlend && textEditorData.Parent.TextViewMargin.SearchResults.Any(sr => 
							sr.Contains(offset) || 
							sr.Contains(offset + nameLength) ||
							offset < sr.Offset && sr.EndOffset < offset + nameLength))
					{
						textEditorData.Parent.TextViewMargin.AlphaBlendSearchResults = alphaBlend = true;
					}
					
					marker.Usages.Add(new Mono.TextEditor.Segment(offset, nameLength));
					lineNumbers.Add(loc.Line);
				}
			}
			foreach (int line in lineNumbers)
				textEditorData.Document.CommitLineUpdate(line);
		}

		#endregion
	}
}
