using System.Collections.Generic;
using System.IO;
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
		/// <summary>
		/// True if one of these formatting routines shall only correct the file's indents.
		/// False, if e.g. brace relocation shall be done too.
		/// </summary>
		public static bool IndentCorrectionOnly = true;
		
		internal const string MimeType = "text/x-d";

		public override bool SupportsOnTheFlyFormatting	{	get	{return true;}	}

		// CorrectIndenting is completely unused in the entire MonoDevelopment code environment - doesn't have to be implemented

		/// <summary>
		/// Used for formatting selected code
		/// </summary>
		public override void OnTheFlyFormat(Ide.Gui.Document _doc, int startOffset, int endOffset)
		{
			var doc = _doc.Editor.Document;
			
			DFormattingPolicy policy = null;
			TextStylePolicy textStyle = null;

			if(_doc.HasProject)
			{
				policy = _doc.Project.Policies.Get<DFormattingPolicy>(Indentation.DTextEditorIndentation.mimeTypes);
				textStyle = _doc.Project.Policies.Get<TextStylePolicy>(Indentation.DTextEditorIndentation.mimeTypes);
			}
			else
			{
				policy = PolicyService.GetDefaultPolicy<DFormattingPolicy> (Indentation.DTextEditorIndentation.mimeTypes);
				textStyle = PolicyService.GetDefaultPolicy<TextStylePolicy> (Indentation.DTextEditorIndentation.mimeTypes);
			}
			
			if(IndentCorrectionOnly)
			{
				using(doc.OpenUndoGroup())
				using(var r = doc.CreateReader())
					D_Parser.Formatting.Indent.IndentEngineWrapper.CorrectIndent(r, 
						startOffset, endOffset, 
						doc.Replace, policy.Options, 
						new TextStyleAdapter(textStyle) { KeepAlignmentSpaces = policy.KeepAlignmentSpaces });
				return;
			}
			
			var ast = _doc.GetDAst();
			
			if(ast == null)
				return;
			
			var formattingVisitor = new DFormattingVisitor(policy.Options, new DocAdapt(doc), ast, new TextStyleAdapter(textStyle));
			
			formattingVisitor.CheckFormattingBoundaries = true;
			var dl = doc.OffsetToLocation(startOffset);
			formattingVisitor.FormattingStartLocation = new CodeLocation(dl.Column, dl.Line);
			dl = doc.OffsetToLocation(endOffset);
			formattingVisitor.FormattingEndLocation = new CodeLocation(dl.Column, dl.Line);
			
			formattingVisitor.WalkThroughAst();
			
			using(doc.OpenUndoGroup())
				formattingVisitor.ApplyChanges(doc.Replace);
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

			public bool KeepAlignmentSpaces
			{
				get;
				set;
			}

			public bool RemoveTrailingWhitespaces
			{
				get { return textStyle.RemoveTrailingWhitespace; }
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
			
			public int ToOffset(CodeLocation loc)
			{
				return doc.LocationToOffset(loc.Line, loc.Column);
			}
			
			public int ToOffset(int line, int column)
			{
				return doc.LocationToOffset(line,column);
			}
			
			public CodeLocation ToLocation(int offset)
			{
				var dl = doc.OffsetToLocation(offset);
				return new CodeLocation(dl.Column, dl.Line);
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
			var data = new TextEditorData{ Text = input };
			
			if(IndentCorrectionOnly)
			{
				using (data.Document.OpenUndoGroup())
				using(var s = data.OpenStream())
					using(var r = new StreamReader(s))
						D_Parser.Formatting.Indent.IndentEngineWrapper.CorrectIndent(r,
							startOffset, endOffset,
							data.Document.Replace, policy.Options,
							new TextStyleAdapter(textPolicy) { KeepAlignmentSpaces = policy.KeepAlignmentSpaces });
				return data.Text;
			}
			
			var ast = DParser.ParseString (input, false, true);
			var formattingVisitor = new DFormattingVisitor(policy.Options, new DocAdapt(data.Document), ast, new TextStyleAdapter(textPolicy));
			
			// Only clip to a region if it's necessary
			if(startOffset > 0 || endOffset < input.Length-1)
			{
				formattingVisitor.CheckFormattingBoundaries = true;
				var dl = data.Document.OffsetToLocation(startOffset);
				formattingVisitor.FormattingStartLocation = new CodeLocation(dl.Column, dl.Line);
				dl = data.Document.OffsetToLocation(endOffset);
				formattingVisitor.FormattingEndLocation = new CodeLocation(dl.Column, dl.Line);
			}
			
			formattingVisitor.WalkThroughAst();

			using (data.Document.OpenUndoGroup())
				formattingVisitor.ApplyChanges(data.Document.Replace);
			
			return data.Text;
		}
	}
}
