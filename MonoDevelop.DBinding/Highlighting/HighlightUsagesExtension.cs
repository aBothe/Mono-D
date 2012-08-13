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

// Code taken and modified from MonoDevelop.CSharp.Highlighting.HighlightUsagesExtension.cs

namespace MonoDevelop.D.Highlighting
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

			public bool DrawBackground(Mono.TextEditor.TextEditor editor, Cairo.Context cr, Mono.TextEditor.TextViewMargin.LayoutWrapper layout, int selectionStart, int selectionEnd, int startOffset, int endOffset, double y, double startXPos, double endXPos, ref bool drawBg)
			{
				drawBg = false;
				if (selectionStart >= 0 || editor.CurrentMode is TextLinkEditMode)
					return true;

				var color_Bg = (HslColor)editor.ColorStyle.BracketHighlightRectangle.BackgroundColor;
				var color_Rect=(HslColor)editor.ColorStyle.BracketHighlightRectangle.Color;

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
						cr.Color = color_Bg;
						cr.Rectangle(@from + 1, y + 1, to - @from - 1, editor.LineHeight - 2);
						cr.Fill();

						cr.Color = color_Rect;
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

				ResolverContextStack ctxt;
				var rr = DResolverWrapper.ResolveHoveredCode(out ctxt, Document);

				if (rr == null || rr.Length < 1)
					return false;

				var parseCache = Document.HasProject ?
						(Document.Project as DProject).ParseCache :
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

				var references = ReferenceFinding.ScanNodeReferencesInModule(dom,parseCache,referencedNode);

				// Highlight the node's definition location - only if the node is located in the current document
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
