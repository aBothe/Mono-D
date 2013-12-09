using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Mono.TextEditor;
using Mono.TextEditor.Highlighting;
using System.IO;
using MonoDevelop.Ide.Tasks;
using System;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;
using System.Threading;
using D_Parser.Refactoring;
using D_Parser.Dom;
using D_Parser.Misc;
using System.Collections.Concurrent;
using System.Reflection;

namespace MonoDevelop.D.Highlighting
{
	public class DSyntaxMode : SyntaxMode, IDisposable
	{
		public const string UserTypesStyle = "User Types";
		static SyntaxMode baseMode;
		Document guiDoc;
		internal Document GuiDocument
		{
			get{ return guiDoc; }
			set{
				if (guiDoc != null)
					guiDoc.DocumentParsed -= HandleDocumentParsed;
				guiDoc = value;
				if (value != null) {
					HandleDocumentParsed (this, EventArgs.Empty);
					guiDoc.DocumentParsed += HandleDocumentParsed;
				}
			}
		}

		public DSyntaxMode()
		{
			if(textSegmentMarkerTreeFI == null)
				textSegmentMarkerTreeFI = typeof(TextDocument).GetField ("textSegmentMarkerTree", 
					System.Reflection.BindingFlags.NonPublic | 
					BindingFlags.Public |
					System.Reflection.BindingFlags.Instance | 
					System.Reflection.BindingFlags.IgnoreCase);

			var matches = new List<Mono.TextEditor.Highlighting.Match>();

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

			SemanticHighlightingEnabled = PropertyService.Get ("EnableSemanticHighlighting", true);
			PropertyService.PropertyChanged += HandlePropertyChanged;
			GlobalParseCache.ParseTaskFinished += GlobalParseCacheFilled;

			this.matches = matches.ToArray();
		}
		/*
		protected override void OnDocumentSet (EventArgs e)
		{
			base.OnDocumentSet (e);
			if(base.doc != null)
				Document.LineChanged += HandleDocumentChanged;
		}
*/
		public virtual void Dispose ()
		{
			GuiDocument = null;
			if (cancelTokenSource != null)
				cancelTokenSource.Cancel ();
			PropertyService.PropertyChanged -= HandlePropertyChanged;
			GlobalParseCache.ParseTaskFinished -= GlobalParseCacheFilled;
			segmentMarkerTree = null;
		}

		public static Match workaroundMatchCtor(string color, string regex)
		{
			var st = new StringReader("<Match color = \""+color+"\"><![CDATA["+regex+"]]></Match>");

			var x = new XmlTextReader(st);
			x.Read();
			var m=Match.Read(x);
			st.Close();

			return m;
		}

		protected override void OnDocumentSet (EventArgs e)
		{
			base.OnDocumentSet (e);

			if (doc != null)
				segmentMarkerTree = textSegmentMarkerTreeFI.GetValue (doc) as SegmentTree<TextSegmentMarker>;
			else
				segmentMarkerTree = null;
		}

		void GlobalParseCacheFilled(ParsingFinishedEventArgs ea)
		{
			var GuiDoc = GuiDocument;
			if(GuiDoc != null && Document != null)
			{
				var pcl = MonoDevelop.D.Resolver.DResolverWrapper.CreateCacheList(GuiDocument);
				if (pcl.Contains (ea.Package.Root))
					HandleDocumentParsed (this, EventArgs.Empty);
			}
		}

		#region Semantic highlighting
		static FieldInfo textSegmentMarkerTreeFI;
		SegmentTree<TextSegmentMarker> segmentMarkerTree;
		List<TypeIdSegmMarker> oldSegments;

		bool SemanticHighlightingEnabled;
		CancellationTokenSource cancelTokenSource;

		void HandlePropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			if (e.Key == "EnableSemanticHighlighting") {
				SemanticHighlightingEnabled = PropertyService.Get ("EnableSemanticHighlighting", true);
				if (!SemanticHighlightingEnabled)
					RemoveOldTypeMarkers ();
			}
		}

		void HandleDocumentParsed (object sender, EventArgs e)
		{
			//if(segmentMarkerTree == null)
				return;

			if (cancelTokenSource != null)
				cancelTokenSource.Cancel ();

			if (guiDoc != null &&
			    SemanticHighlightingEnabled && guiDoc.ParsedDocument != null) {
				cancelTokenSource = new CancellationTokenSource ();
				System.Threading.Tasks.Task.Factory.StartNew (updateTypeHighlightings, cancelTokenSource.Token);
			}
		}

		void RemoveOldTypeMarkers(bool commitUpdate = true)
		{
			Ide.DispatchService.GuiSyncDispatch (() => {
				try{
					if(oldSegments != null)
						foreach(var segm in oldSegments)
							segmentMarkerTree.Remove (segm);
					oldSegments = null;
					if(commitUpdate)
						doc.CommitDocumentUpdate();
				}catch(Exception ex)
				{
					LoggingService.LogError ("Error during semantic highlighting", ex);
				}
			});
		}

		void updateTypeHighlightings ()
		{
			if (guiDoc == null)
				return;
			var parsedDoc = guiDoc.ParsedDocument as Parser.ParsedDModule;
			if (parsedDoc == null || parsedDoc.IsInvalid)
				return;
			var ast = (guiDoc.ParsedDocument as Parser.ParsedDModule).DDom;
			if (ast == null)
				return;

			RemoveOldTypeMarkers (false);

			var segments = new List<TypeIdSegmMarker>();
			try{
				var textLocationsToHighlight = TypeReferenceFinder.Scan(ast, 
					MonoDevelop.D.Completion.DCodeCompletionSupport.CreateContext(guiDoc)).Matches;
				/*textLocationsToHighlight.Clear ();

				List<ISyntaxRegion> l;
				foreach (var n in ast) {
					if (n is DClassLike) {
						var name = n.Name;
						var nameLoc = n.NameLocation;

						if (!textLocationsToHighlight.TryGetValue (nameLoc.Line, out l))
							textLocationsToHighlight [nameLoc.Line] = l = new List<ISyntaxRegion> ();

						l.Add (new D_Parser.Dom.Expressions.IdentifierExpression (name) { 
							Location = nameLoc, EndLocation = new CodeLocation (nameLoc.Column + name.Length, nameLoc.Line)
						});
					}
				}*/

				int off, len;

				foreach (var kv in textLocationsToHighlight) {
					var line = doc.GetLine (kv.Key);
					foreach (var sr in kv.Value) {
						if (sr is INode) {
							var n = sr as INode;
							var nameLine = n.NameLocation.Line == kv.Key ? line : doc.GetLine (n.NameLocation.Line);
							off = nameLine.Offset + n.NameLocation.Column - 1;
							len = n.Name.Length;
						} else {
							off = line.Offset + sr.Location.Column - 1;
							len = sr.EndLocation.Column - sr.Location.Column;
						}

						var marker = new TypeIdSegmMarker (off, len);
						segments.Add (marker);
					}
				}
			}
			catch(Exception ex) {
				LoggingService.LogError ("Error during semantic highlighting", ex);
			}

			Ide.DispatchService.GuiDispatch(()=>{ 
				foreach(var m in segments)
					segmentMarkerTree.Add(m);

				guiDoc.Editor.Parent.TextViewMargin.PurgeLayoutCache ();
				guiDoc.Editor.Parent.QueueDraw ();

				if (oldSegments != null)
					RemoveOldTypeMarkers ();
				oldSegments = segments;
			});
		}

		class TypeIdSegmMarker : TextSegmentMarker
		{
			public TypeIdSegmMarker(int off, int len) : base(off,len){}
		}

		public override ChunkParser CreateChunkParser (SpanParser spanParser, ColorScheme style, DocumentLine line)
		{
			return new DChunkParser(this, spanParser, style, line);
		}

		class DChunkParser : ChunkParser
		{
			public DChunkParser(DSyntaxMode syn,SpanParser s, ColorScheme st, DocumentLine ln)
				: base(syn, s, st, ln) {}

			protected override void AddRealChunk (Chunk chunk)
			{
				if (spanParser.CurSpan != null && (spanParser.CurSpan.Rule == "Comment" || spanParser.CurSpan.Rule == "PreProcessorComment")) {
					base.AddRealChunk (chunk);
					return;
				}

				foreach (var m in doc.GetTextSegmentMarkersAt(line)) {
					var tm = m as TypeIdSegmMarker;
					if (chunk.Offset == tm.Offset && tm != null && tm.IsVisible) {
						var endLoc = tm.EndOffset;
						if (endLoc < chunk.EndOffset) {
							base.AddRealChunk (new Chunk (chunk.Offset, endLoc - chunk.Offset, DSyntaxMode.UserTypesStyle));
							base.AddRealChunk (new Chunk (endLoc, chunk.EndOffset - endLoc, chunk.Style));
							return;
						}
						chunk.Style = DSyntaxMode.UserTypesStyle;
						break;
					}
				}

				base.AddRealChunk (chunk);
			}
		}
		#endregion
	}
}
