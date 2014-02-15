using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Mono.TextEditor;
using Mono.TextEditor.Highlighting;
using System.IO;
using System;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;
using System.Threading;
using D_Parser.Refactoring;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using MonoDevelop.Components.Commands;

namespace MonoDevelop.D.Highlighting
{
	public class DSyntaxMode : SyntaxMode, IDisposable
	{
		static SyntaxMode baseMode;
		Document guiDoc;
		internal Document GuiDocument
		{
			get { return guiDoc; }
			set
			{
				if (guiDoc != null)
					guiDoc.DocumentParsed -= HandleDocumentParsed;
				guiDoc = value;
				if (value != null)
				{
					if (DiffbasedHighlighting.Enabled)
						TryInjectDiffbasedMarker();

					HandleDocumentParsed(this, EventArgs.Empty);
					guiDoc.DocumentParsed += HandleDocumentParsed;
				}
			}
		}

		public DSyntaxMode()
		{
			var matches = new List<Match>();

			if (baseMode == null)
			{
				var provider = new ResourceStreamProvider(
					typeof(DSyntaxMode).Assembly,
					typeof(DSyntaxMode).Assembly.GetManifestResourceNames().First(s => s.Contains("DSyntaxHighlightingMode")));
				using (Stream s = provider.Open())
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
			matches.Add(workaroundMatchCtor(
				"Number"
				, @"(?<!\w)(0((x|X)[0-9a-fA-F_]+|(b|B)[0-1_]+)|([0-9]+[_0-9]*)[L|U|u|f|i]*)"));

			// extern linkages attributes
			//matches.Add(workaroundMatchCtor("constant.digit", "(?<=extern[\\s]*\\()[\\s]*(C(\\+\\+)?|D|Windows|System|Pascal|Java)[\\s]*(?=\\))"));

			// version checks
			//matches.Add(workaroundMatchCtor("constant.digit", @"(?<=version[\s]*\()[\s]*(DigitalMars|GNU|LDC|Windows|OSX|linux|FreeBSD|OpenBSD|BSD|Solaris|Posix|D_Version2)[\s]*(?=\))"));

			// type declaration names
			//matches.Add(workaroundMatchCtor("keyword.semantic.type", @"(?<=(class|struct|union|interface|template)[\s]+)[\w]+"));

			SemanticHighlightingEnabled = PropertyService.Get("EnableSemanticHighlighting", true);
			PropertyService.PropertyChanged += HandlePropertyChanged;
			GlobalParseCache.ParseTaskFinished += GlobalParseCacheFilled;

			this.matches = matches.ToArray();
		}

		public virtual void Dispose()
		{
			if (doc != null && segmentMarkerTree != null)
				segmentMarkerTree.RemoveListener();
			GuiDocument = null;
			if (cancelTokenSource != null)
				cancelTokenSource.Cancel();
			PropertyService.PropertyChanged -= HandlePropertyChanged;
			GlobalParseCache.ParseTaskFinished -= GlobalParseCacheFilled;
			segmentMarkerTree = null;
		}

		public static Match workaroundMatchCtor(string color, string regex)
		{
			var st = new StringReader("<Match color = \"" + color + "\"><![CDATA[" + regex + "]]></Match>");

			var x = new XmlTextReader(st);
			x.Read();
			var m = Match.Read(x);
			st.Close();

			return m;
		}

		protected override void OnDocumentSet(EventArgs e)
		{
			base.OnDocumentSet(e);

			if (doc != null)
			{
				segmentMarkerTree = new SegmentTree<TextSegmentMarker>();
				segmentMarkerTree.InstallListener(doc);
			}
			else
				segmentMarkerTree = null;
		}

		void GlobalParseCacheFilled(ParsingFinishedEventArgs ea)
		{
			var GuiDoc = GuiDocument;
			if (GuiDoc != null && Document != null && ea.Package != null)
			{
				var root = ea.Package.Root;
				if (root == null)
					return;

				var pcl = MonoDevelop.D.Resolver.DResolverWrapper.CreateCacheList(GuiDoc);
				if (pcl.Contains(root))
					HandleDocumentParsed(this, EventArgs.Empty);
			}
		}

		#region Semantic highlighting
		SegmentTree<TextSegmentMarker> segmentMarkerTree = new SegmentTree<TextSegmentMarker>();

		bool SemanticHighlightingEnabled;
		CancellationTokenSource cancelTokenSource;

		void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.Key == DiffbasedHighlighting.DiffBasedHighlightingProp)
			{
				if (DiffbasedHighlighting.Enabled)
				{
					TryInjectDiffbasedMarker();
					SemanticHighlightingEnabled = false;
					RemoveOldTypeMarkers(true);
				}
				else
				{
					TryRemoveDiffbasedMarker();
					if(SemanticHighlightingEnabled = PropertyService.Get("EnableSemanticHighlighting", true))
						HandleDocumentParsed(sender, e);
				}
			}
			else if (e.Key == "EnableSemanticHighlighting")
			{
				SemanticHighlightingEnabled = PropertyService.Get("EnableSemanticHighlighting", true);
				if (!SemanticHighlightingEnabled)
					RemoveOldTypeMarkers();
			}
		}


		void HandleDocumentParsed(object sender, EventArgs e)
		{
			if (segmentMarkerTree == null)
				return;

			if (cancelTokenSource != null)
				cancelTokenSource.Cancel();

			if (guiDoc != null &&
				SemanticHighlightingEnabled && guiDoc.ParsedDocument != null)
			{
				cancelTokenSource = new CancellationTokenSource();
				System.Threading.Tasks.Task.Factory.StartNew(updateTypeHighlightings, cancelTokenSource.Token);
			}
		}

		void RemoveOldTypeMarkers(bool commitUpdate = true)
		{
			if (segmentMarkerTree != null)
				segmentMarkerTree.Clear();
			if (commitUpdate)
				Ide.DispatchService.GuiSyncDispatch(() =>
				{
					try
					{
						if(doc != null)
							doc.CommitDocumentUpdate();
					}
					catch (Exception ex)
					{
						LoggingService.LogError("Error during semantic highlighting", ex);
					}
				});
		}

		void updateTypeHighlightings()
		{
			if (guiDoc == null)
				return;
			var parsedDoc = guiDoc.ParsedDocument as Parser.ParsedDModule;
			if (parsedDoc == null || parsedDoc.IsInvalid)
				return;
			var ast = (guiDoc.ParsedDocument as Parser.ParsedDModule).DDom;
			if (ast == null)
				return;

			RemoveOldTypeMarkers(false);

			try
			{
				var textLocationsToHighlight = TypeReferenceFinder.Scan(ast,
					MonoDevelop.D.Completion.DCodeCompletionSupport.CreateContext(guiDoc));

				int off, len;

				foreach (var kv in textLocationsToHighlight)
				{
					var line = doc.GetLine(kv.Key);
					foreach (var kvv in kv.Value)
					{
						var sr = kvv.Key;
						var ident = "";
						if (sr is INode)
						{
							var n = sr as INode;
							var nameLine = n.NameLocation.Line == kv.Key ? line : doc.GetLine(n.NameLocation.Line);
							off = nameLine.Offset + n.NameLocation.Column - 1;
							len = n.Name.Length;
						}
						else if (sr is TemplateParameter)
						{
							var tp = sr as TemplateParameter;
							if (tp.NameLocation.IsEmpty)
								continue;
							var nameLine = tp.NameLocation.Line == kv.Key ? line : doc.GetLine(tp.NameLocation.Line);
							off = nameLine.Offset + tp.NameLocation.Column - 1;
							len = tp.Name.Length;
						}
						else
						{
							var templ = sr as TemplateInstanceExpression;
							if (templ != null)
								ident = templ.TemplateId;
							GetIdentifier(ref sr);
							off = line.Offset + sr.Location.Column - 1;
							len = sr.EndLocation.Column - sr.Location.Column;

						}

						segmentMarkerTree.Add(new TypeIdSegmMarker(ident, off, len, kvv.Value));
					}
				}
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error during semantic highlighting", ex);
			}

			Ide.DispatchService.GuiDispatch(() =>
			{
				guiDoc.Editor.Parent.TextViewMargin.PurgeLayoutCache();
				guiDoc.Editor.Parent.QueueDraw();
			});
		}

		static void GetIdentifier(ref ISyntaxRegion sr)
		{
			if (sr is TemplateInstanceExpression)
			{
				sr = (sr as TemplateInstanceExpression).Identifier;
				GetIdentifier(ref sr);
			}
			else if (sr is NewExpression)
			{
				sr = (sr as NewExpression).Type;
				GetIdentifier(ref sr);
			}
		}

		class TypeIdSegmMarker : TextSegmentMarker
		{
			public byte SemanticType;
			public string Style;

			public TypeIdSegmMarker(string ident, int off, int len, byte type)
				: base(off, len)
			{
				this.SemanticType = type;
				this.Style = GetSemanticStyle(ident, type);
			}

			public static string GetSemanticStyle(string ident, byte type)
			{
				switch (type)
				{
					case DTokens.Delegate:
					case DTokens.Function:
						return "User Types(Delegates)";
					case DTokens.Enum:
						return "User Types(Enums)";
					case DTokens.Interface:
						return "User Types(Interfaces)";
					case DTokens.Not: // template parameters
						return "User Types(Type parameters)";
					case DTokens.Struct:
						return "User Types(Value types)";
					case DTokens.Template:
						if (ident.Length > 0 && char.IsLower(ident[0]))
						{
							if (
								(ident.Length > 1 && ident.Substring(0, 2) == "is")
								|| (ident.Length > 2 && ident.Substring(0, 3) == "has"))
							{
								return "User Method Usage";
							}
							else
								return "User Types(Enums)";
						}
						else
							return "User Types";
					default:
						return "User Types";
				}
			}
		}

		public override ChunkParser CreateChunkParser(SpanParser spanParser, ColorScheme style, DocumentLine line)
		{
			switch ((SemanticHighlightingEnabled ? 2 : 0) + (DiffbasedHighlighting.Enabled ? 1 : 0))
			{
				case 1:
				case 3:
					return new DiffbasedChunkParser(this, spanParser, style, line);
				case 2:
					return new DChunkParser(this, spanParser, style, line);
				default:
					return base.CreateChunkParser(spanParser, style, line);
			}
		}

		class DChunkParser : ChunkParser
		{
			public DChunkParser(DSyntaxMode syn, SpanParser s, ColorScheme st, DocumentLine ln)
				: base(syn, s, st, ln) { }

			protected override void AddRealChunk(Chunk chunk)
			{
				if (spanParser.CurSpan != null && (spanParser.CurSpan.Rule == "Comment" || spanParser.CurSpan.Rule == "PreProcessorComment"))
				{
					base.AddRealChunk(chunk);
					return;
				}
				var syn = mode as DSyntaxMode;
				foreach (var m in syn.segmentMarkerTree.GetSegmentsAt(chunk.Offset))
				{
					var tm = m as TypeIdSegmMarker;
					if (tm != null && tm.IsVisible)
					{
						var endLoc = tm.EndOffset;
						if (endLoc < chunk.EndOffset)
						{
							base.AddRealChunk(new Chunk(chunk.Offset, endLoc - chunk.Offset, tm.Style));
							base.AddRealChunk(new Chunk(endLoc, chunk.EndOffset - endLoc, chunk.Style));
							return;
						}
						chunk.Style = tm.Style;
						break;
					}
				}

				base.AddRealChunk(chunk);
			}
		}
		#endregion

		#region Diffbased coloring
		DiffbasedMarker diffMarker;

		void TryInjectDiffbasedMarker()
		{
			if (doc != null && diffMarker == null)
				doc.AddMarker(diffMarker = new DiffbasedMarker(guiDoc != null && guiDoc.Editor != null ? guiDoc.Editor.Length : doc.TextLength));
		}

		void TryRemoveDiffbasedMarker()
		{
			if (doc != null && diffMarker != null)
				doc.RemoveMarker(diffMarker);
			diffMarker = null;
		}

		class DiffbasedChunkParser : ChunkParser
		{
			public DiffbasedChunkParser(DSyntaxMode syn, SpanParser s, ColorScheme st, DocumentLine ln)
				: base(syn, s, st, ln) { }

			bool a;
			protected override void AddRealChunk(Chunk chunk)
			{
				if (chunk.Style == "Plain Text")
				{
					// Prevent 'Plain Text' chunk concatenation by giving following chunks a different style name.
					if (a = !a)
						chunk.Style = "Plain Texta";
				}

				base.AddRealChunk(chunk);
			}
		}

		class DiffbasedMarker : TextSegmentMarker, IChunkMarker
		{
			bool hasEvaluatedBackgroundBrightness;
			bool IsDarkBackground;

			public DiffbasedMarker(int len) : base(0, len) { 
				
			}

			public void ChangeForeColor(TextEditor editor, Chunk chunk, ref Cairo.Color color)
			{
				if (chunk.Length < 1)
					return;
				
				switch (chunk.Style)
				{
					case "Plain Text":
					case "Plain Texta":
						if (!hasEvaluatedBackgroundBrightness)
						{
							hasEvaluatedBackgroundBrightness = true;
							IsDarkBackground = HslColor.Brightness(editor.ColorStyle.PlainText.Background) < 0.5;
						}

						color = DiffbasedHighlighting.GetColor(editor.GetTextAt(chunk).Trim());
						if (IsDarkBackground)
							color = new Cairo.Color(1.0 - color.R, 1.0 - color.G, 1.0 - color.B, color.A);
						break;
					case "String":
						color = new Cairo.Color(0.8, 0, 0);
						break;
					default:
						if (chunk.Style.StartsWith("Keyword"))
							chunk.Style = "Plain Text";
						break;
				}
			}

			public void TransformChunks(List<Chunk> chunks)
			{
				// Unhighlight each normal keyword
				foreach (var c in chunks)
					if (c.Style.StartsWith("Key"))
						c.Style = "Plain Text____";
			}
		}
		#endregion
	}
}
