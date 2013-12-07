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

namespace MonoDevelop.D.Highlighting
{
	public class DSyntaxMode : SyntaxMode, IDisposable
	{
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
			PropertyService.PropertyChanged -= HandlePropertyChanged;
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



		#region Semantic highlighting
		bool SemanticHighlightingEnabled;
		CancellationTokenSource cancelTokenSource;

		void HandlePropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			if (e.Key == "EnableSemanticHighlighting")
				SemanticHighlightingEnabled = PropertyService.Get ("EnableSemanticHighlighting", true);
		}

		void HandleDocumentChanged(object sender, LineEventArgs ea)
		{/*
			if (ea.LineNumber == -1)
				textLocationsToHighlight.Remove (ea.Line.LineNumber + 1);
			textLocationsToHighlight.Remove (ea.Line.LineNumber);*/
		}

		void HandleDocumentParsed (object sender, EventArgs e)
		{/*
			if (cancelTokenSource != null)
				cancelTokenSource.Cancel ();

			if (guiDoc != null && !guiDoc.IsProjectContextInUpdate && 
				SemanticHighlightingEnabled && guiDoc.ParsedDocument != null) {
				cancelTokenSource = new CancellationTokenSource ();
				System.Threading.Tasks.Task.Factory.StartNew (updateTypeHighlightings, cancelTokenSource.Token);
			}*/
		}

		/// <summary>
		/// The text locations to highlight. Key = Line. Value = Columns where type ids are located at (1-based)
		/// </summary>
		Dictionary<int, List<ISyntaxRegion>> textLocationsToHighlight = new Dictionary<int, List<ISyntaxRegion>>();

		void updateTypeHighlightings()
		{
			textLocationsToHighlight = TypeReferenceFinder.Scan(
				(guiDoc.ParsedDocument as Parser.ParsedDModule).DDom,
				Completion.DCodeCompletionSupport.CreateContext(guiDoc)).Matches;

		/*
			var visitor = new QuickTaskVisitor (newResolver, cancellationToken);
			try {
				newResolver.RootNode.AcceptVisitor (visitor);
			} catch (Exception ex) {
				LoggingService.LogError ("Error while analyzing the file for the semantic highlighting.", ex);
				return;
			}
			if (!cancellationToken.IsCancellationRequested) {
				Gtk.Application.Invoke (delegate {
					if (cancellationToken.IsCancellationRequested)
						return;
					var editorData = guiDocument.Editor;
					if (editorData == null)
						return;
//									compilation = newResolver.Compilation;
					resolver = newResolver;
					quickTasks = visitor.QuickTasks;
					OnTasksUpdated (EventArgs.Empty);
					foreach (var kv in lineSegments) {
						try {
							kv.Value.tree.RemoveListener ();
						} catch (Exception) {
						}
					}
					lineSegments.Clear ();
					var textEditor = editorData.Parent;
					if (textEditor != null) {
						if (!parsedDocument.HasErrors) {
							var margin = textEditor.TextViewMargin;
							margin.PurgeLayoutCache ();
							textEditor.QueueDraw ();
						}
					}
				});
			}
			*/
		}
		/*
		public override ChunkParser CreateChunkParser (SpanParser spanParser, ColorScheme style, DocumentLine line)
		{
			return new DChunkParser (this,spanParser, style, line);
		}
		*/
		/// Inserts custom highlighting sections per-line into the text document view.
		class DChunkParser : ChunkParser
		{
			DSyntaxMode dsyntaxmode;
			int lineNumber;

			public DChunkParser(DSyntaxMode sm,SpanParser spanParser, ColorScheme style, DocumentLine line)
				: base(sm, spanParser, style, line)
			{
				dsyntaxmode = sm;
				lineNumber = line.LineNumber;
			}

			/// <summary>
			/// All needed for nice type id highlighting is to alter the chunk style at the right positions.
			/// </summary>
			/// <returns>The style.</returns>
			/// <param name="chunk">A piece of the displayed text.</param>
			protected override string GetStyle (Chunk chunk)
			{
				List<ISyntaxRegion> offsets;
				if (chunk.Length != 0) {
					if (dsyntaxmode.textLocationsToHighlight.TryGetValue (lineNumber, out offsets)) {
						var chunkColumn = line.GetLogicalColumn(dsyntaxmode.guiDoc.Editor,chunk.Offset-line.Offset);

						INode n;
						foreach(var sr in offsets)
							if(sr.EndLocation.Column >= chunkColumn && ((n=sr as INode) == null ?
								sr.Location.Column <= chunkColumn :	n.NameLocation.Column <= chunkColumn))
								return "User Types";
					}
					return base.GetStyle (chunk) ?? "Plain Text";
				}

				return base.GetStyle (chunk);
			}
		}
		#endregion
	}
}
