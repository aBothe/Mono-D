using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Formatting;
using D_Parser.Parser;
using Mono.TextEditor;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D.Formatting
{
	public class DCodeFormatter: AbstractAdvancedFormatter
	{
		internal const string MimeType = "text/x-d";

		public override bool SupportsOnTheFlyFormatting	{	get	{return true;}	}

		// CorrectIndenting is completely unused in the entire MonoDevelopment code environment - doesn't have to be implemented

		/// <summary>
		/// Used for formatting selected code
		/// </summary>
		public override void OnTheFlyFormat(Ide.Gui.Document doc, int startOffset, int endOffset)
		{
			var dpd = doc.ParsedDocument as ParsedDModule;
			
			if(dpd == null)
				return;
			DFormattingPolicy policy = null;
			TextStylePolicy textStyle = null;

			if(doc.HasProject)
			{
				policy = doc.Project.Policies.Get<DFormattingPolicy>(Indentation.DTextEditorIndentation.mimeTypes);
				textStyle = doc.Project.Policies.Get<TextStylePolicy>(Indentation.DTextEditorIndentation.mimeTypes);
			}
			else
			{
				policy = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<DFormattingPolicy> (Indentation.DTextEditorIndentation.mimeTypes);
				textStyle = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> (Indentation.DTextEditorIndentation.mimeTypes);
			}
			
			var formattingVisitor = new DFormattingVisitor(policy.Options, new DocAdapt(doc.Editor.Document), dpd.DDom as D_Parser.Dom.DModule, new TextStyleAdapter(textStyle));
			
			formattingVisitor.CheckFormattingBoundaries = true;
			var dl = doc.Editor.Document.OffsetToLocation(startOffset);
			formattingVisitor.FormattingStartLocation = new D_Parser.Dom.CodeLocation(dl.Column, dl.Line);
			dl = doc.Editor.Document.OffsetToLocation(endOffset);
			formattingVisitor.FormattingEndLocation = new D_Parser.Dom.CodeLocation(dl.Column, dl.Line);
			
			formattingVisitor.WalkThroughAst();
			
			formattingVisitor.ApplyChanges(doc.Editor.Document.Replace);
		}
		
		public class TextStyleAdapter : D_Parser.Formatting.ITextEditorOptions{
			public readonly TextStylePolicy textStyle;
			
			public TextStyleAdapter(TextStylePolicy txt)
			{
				this.textStyle = txt;
			}
			
			public string EolMarker {
				get {
					return textStyle.GetEolMarker();
				}
			}
			
			public bool TabsToSpaces {
				get {
					return textStyle.TabsToSpaces;
				}
			}
			
			public int TabSize {
				get {
					return textStyle.TabWidth;
				}
			}
			
			public int IndentSize {
				get {
					return textStyle.IndentWidth;
				}
			}
			
			public int ContinuationIndent {
				get {
					return textStyle.IndentWidth;
				}
			}
			
			public int LabelIndent {
				get {
					return textStyle.IndentWidth;
				}
			}
		}
		
		class DocAdapt : IDocumentAdapter
		{
			public readonly Mono.TextEditor.TextDocument doc;
			public DocAdapt(Mono.TextEditor.TextDocument doc)
			{
				this.doc = doc;
			}
			
			public char this[int o] {
				get {
					return doc.GetCharAt(o);
				}
			}
			
			public int TextLength {
				get {
					return doc.TextLength;
				}
			}
			
			public string Text {
				get {
					return doc.Text;
				}
			}
			
			public int ToOffset(D_Parser.Dom.CodeLocation loc)
			{
				return doc.LocationToOffset(loc.Line, loc.Column);
			}
			
			public int ToOffset(int line, int column)
			{
				return doc.LocationToOffset(line,column);
			}
			
			public D_Parser.Dom.CodeLocation ToLocation(int offset)
			{
				var dl = doc.OffsetToLocation(offset);
				return new D_Parser.Dom.CodeLocation(dl.Column, dl.Line);
			}
			
			public int LineCount {
				get {
					return doc.LineCount;
				}
			}
		}

		/// <summary>
		/// Used for formatting the entire document
		/// </summary>
		public override string FormatText(PolicyContainer policyParent, IEnumerable<string> mimeTypeChain, string input, int startOffset, int endOffset)
		{
			var policy = policyParent.Get<DFormattingPolicy> (mimeTypeChain);
			var textPolicy = policyParent.Get<TextStylePolicy> (mimeTypeChain);
			var ast = DParser.ParseString(input, false, true) as DModule;
			
			var data = new TextEditorData ();
			data.Text = input;
			
			var formattingVisitor = new DFormattingVisitor(policy.Options, new DocAdapt(data.Document), ast, new TextStyleAdapter(textPolicy));
			
			// Only clip to a region if it's necessary
			if(startOffset > 0 || endOffset < input.Length-1)
			{
				formattingVisitor.CheckFormattingBoundaries = true;
				var dl = data.Document.OffsetToLocation(startOffset);
				formattingVisitor.FormattingStartLocation = new D_Parser.Dom.CodeLocation(dl.Column, dl.Line);
				dl = data.Document.OffsetToLocation(endOffset);
				formattingVisitor.FormattingEndLocation = new D_Parser.Dom.CodeLocation(dl.Column, dl.Line);
			}
			
			formattingVisitor.WalkThroughAst();
			
			formattingVisitor.ApplyChanges(data.Document.Replace);
			
			return data.Text;
		}
	}
}
