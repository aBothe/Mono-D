using System.Collections.Generic;
using System.Linq;
using System.Xml;
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
using System.Threading.Tasks;
using MonoDevelop.Ide.Editor.Highlighting;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Core.Text;

namespace MonoDevelop.D.Editing.Highlighting
{
	public class DSyntaxMode : SemanticHighlighting
	{
		public const string EnableConditionalHighlightingProp = "EnableConditionalHighlightingInD";
		public static bool EnableConditionalHighlighting
		{
			get { return PropertyService.Get(EnableConditionalHighlightingProp, true); }
			set { PropertyService.Set(EnableConditionalHighlightingProp, value); }
		}

		CancellationTokenSource src = new CancellationTokenSource ();

		public DSyntaxMode(TextEditor editor, DocumentContext documentContext) : base (editor, documentContext)
		{
		}

		protected override void DocumentParsed ()
		{
			var parsedDocument = documentContext.ParsedDocument;
			if (parsedDocument == null)
				return;
			//var resolver = parsedDocument.GetAst<SemanticModel> ();
			//if (resolver == null)
			//	return;
			CancelHighlightingTask ();
			var token = src.Token;

			//Task.Run (async delegate {
			//	try {
			//		var root = await resolver.SyntaxTree.GetRootAsync (token);
			//		var newTree = new HighlightingSegmentTree ();

			//		var visitor = new HighlightingVisitior (resolver, newTree.Add, token, TextSegment.FromBounds(0, root.FullSpan.Length));
			//		visitor.Visit (root);

			//		if (!token.IsCancellationRequested) {
			//			Gtk.Application.Invoke (delegate {
			//				if (token.IsCancellationRequested)
			//					return;
			//				if (highlightTree != null) {
			//					highlightTree.RemoveListener ();
			//				}
			//				highlightTree = newTree;
			//				highlightTree.InstallListener (editor);
			//				NotifySemanticHighlightingUpdate ();
			//			});
			//		}
			//	} catch (OperationCanceledException) {
			//	} catch (AggregateException ae) {
			//		ae.Flatten ().Handle (x => x is OperationCanceledException); 
			//	}
			//}, token);
		}

		void CancelHighlightingTask ()
		{
			src.Cancel ();
			src = new CancellationTokenSource ();
		}

		public override IEnumerable<ColoredSegment> GetColoredSegments (ISegment segment)
		{// new ColoredSegment (segment, "Keyword(Type)")
			return null;
			/*
			var result = new List<ColoredSegment> ();
			if (highlightTree == null)
				return result;
			return highlightTree.GetSegmentsOverlapping (segment).Select (seg => seg.GetColoredSegment () );*/
		}

		public override void Dispose ()
		{
			CancelHighlightingTask ();
			//if (highlightTree != null)
			//	highlightTree.RemoveListener ();
			//highlightTree = null;
			base.Dispose ();
		}
	}
}
