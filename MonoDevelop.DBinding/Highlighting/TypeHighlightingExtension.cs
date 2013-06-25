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
using D_Parser.Refactoring;
using MonoDevelop.D.Resolver;

TODO: Reimplement type highlighting in semantic highlighting!

namespace MonoDevelop.D.Highlighting
{
	public class TypeHighlightingExtension:TextEditorExtension
	{
		#region Properties
		Thread th;

		public DModule SyntaxTree
		{
			get { return (Document.ParsedDocument as ParsedDModule).DDom; }
		}

		readonly List<TextSegmentMarker> markers = new List<TextSegmentMarker>();
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
			TypeReferencesResult res=null;
			try
			{
				var ParseCache = DResolverWrapper.CreateCacheList(Document);

				res = TypeReferenceFinder.Scan(SyntaxTree, ParseCache);

				RemoveMarkers(false);

				var txtDoc = Document.Editor.Document;

				DocumentLine curLine=null;
				int ln=-1;
				int len = 0;
				foreach (var id in res.TypeMatches)
				{
					var loc = DeepASTVisitor.ExtractIdLocation(id, out len);
					if(ln!=loc.Line)
						curLine = Document.Editor.GetLine(ln = loc.Line);

					var segment = new TextSegment(curLine.Offset,len);
					var m = new UnderlineTextSegmentMarker("keyword.semantic.type", loc.Column, TypeReferenceFinder.ExtractId(id));

					txtDoc.AddMarker(m);
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

		class MyUnderlineMarker : UnderlineTextSegmentMarker
		{
		}
	}
}
