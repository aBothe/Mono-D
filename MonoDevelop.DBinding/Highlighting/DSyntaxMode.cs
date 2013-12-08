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
			if (e.Key == "EnableSemanticHighlighting") {
				SemanticHighlightingEnabled = PropertyService.Get ("EnableSemanticHighlighting", true);
				if (!SemanticHighlightingEnabled)
					RemoveOldTypeMarkers ();
			}
		}

		void HandleDocumentParsed (object sender, EventArgs e)
		{
			return;

			if (cancelTokenSource != null)
				cancelTokenSource.Cancel ();

			if (guiDoc != null && !guiDoc.IsProjectContextInUpdate &&
			    SemanticHighlightingEnabled && guiDoc.ParsedDocument != null) {
				cancelTokenSource = new CancellationTokenSource ();
				System.Threading.Tasks.Task.Factory.StartNew (updateTypeHighlightings, cancelTokenSource.Token);
			}
		}

		/// <summary>
		/// The text locations to highlight. Key = Line.
		/// </summary>
		List<TypeIdSegmMarker> segments = new List<TypeIdSegmMarker>();

		void RemoveOldTypeMarkers()
		{
			Ide.DispatchService.GuiSyncDispatch (() => {
				try{
					guiDoc.Editor.Parent.TextViewMargin.PurgeLayoutCache();
					for (int i = segments.Count; i > 0;)
						doc.RemoveMarker (segments [--i]);
					segments.Clear ();
				}catch(Exception ex)
				{
					LoggingService.LogError ("Error during semantic highlighting", ex);
				}
			});
		}

		void updateTypeHighlightings ()
		{
			try{
				if (guiDoc == null)
					return;
				var parsedDoc = guiDoc.ParsedDocument as Parser.ParsedDModule;
				if (parsedDoc == null)
					return;
				var ast = (guiDoc.ParsedDocument as Parser.ParsedDModule).DDom;
				if (ast == null)
					return;

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

				RemoveOldTypeMarkers ();
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
						doc.AddMarker (marker);
					}
				}
			}
			catch(Exception ex) {
				LoggingService.LogError ("Error during semantic highlighting", ex);
			}
		}

		class TypeIdSegmMarker : TextSegmentMarker, IChunkMarker
		{
			public TypeIdSegmMarker(int off, int len) : base(off,len){}

			public void TransformChunks (List<Chunk> chunks)
			{
				try{
					var off = Offset;
					var endOff = EndOffset;

					for(int i = 0; i < chunks.Count; i++)
					{
						var chunk = chunks [i];
						if (chunk.Offset == off && chunk.EndOffset == endOff) {
							chunk.Style = "User Types";
							return;
						} else if (chunk.Next != null ? chunk.Next.Offset >= endOff : chunk.Offset <= off) {
							var remaining = chunk.EndOffset - endOff;
							chunk.Length = off - chunk.Offset;

							var insertee = new Chunk (off, endOff - off, "User Types");
							chunk.Next = insertee;

							if(remaining > 0)
							{
								var filler = new Chunk(endOff, remaining, chunk.Style);
								insertee.Next = filler;
								filler.Next = chunk.Next;
								chunks.Insert(i+1, filler);
							}
							else
								insertee.Next = chunk.Next;

							chunks.Insert(i+1, insertee);
							return;
						}
					}
				}catch(Exception ex) {
					LoggingService.LogError ("Error during semantic highlighting", ex);
				}
			}

			public void ChangeForeColor (TextEditor editor, Chunk chunk, ref Cairo.Color color) { }
		}
		#endregion
	}
}
