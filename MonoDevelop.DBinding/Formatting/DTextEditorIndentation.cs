using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using Gdk;
using D_Parser.Formatting;
using D_Parser.Resolver;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.SourceEditor;
using MonoDevelop.Projects.Policies;
using D_Parser.Parser;
using Mono.TextEditor;

namespace MonoDevelop.D.Formatting
{
	public class DTextEditorIndentation:TextEditorExtension
	{
		D_Parser.Formatting.DFormatter formatter = new D_Parser.Formatting.DFormatter ();

		static DTextEditorIndentation ()
		{/*
			CompletionWindowManager.WordCompleted += delegate(object sender, CodeCompletionContextEventArgs e)
			{
				IExtensibleTextEditor editor = e.Widget as IExtensibleTextEditor;
				if (editor == null)
					return;
				ITextEditorExtension textEditorExtension = editor.Extension;
				while (textEditorExtension != null && !(textEditorExtension is DTextEditorIndentation))
				{
					textEditorExtension = textEditorExtension.Next;
				}
				var extension = textEditorExtension as DTextEditorIndentation;
				if (extension == null)
					return;

				// Do re-indent after word completion
			};*/
		}

		public override void Initialize ()
		{
			base.Initialize ();
		}

		public override bool KeyPress (Key key, char keyChar, ModifierType modifier)
		{
			var ed = Document.Editor;
			
			var dPolicy = Document.HasProject ? Document.Project.Policies.Get<DFormattingPolicy> ("text/x-d") :
				PolicyService.GetDefaultPolicy<DFormattingPolicy> ("text/x-d");
			
			if (key == Key.Return) {
				ed.DeleteSelectedText (true);

				int lastBegin;
				int lastEnd;
				var caretCtxt = CaretContextAnalyzer.GetTokenContext (ed.Text, ed.Caret.Offset, out lastBegin, out lastEnd);

				if (lastBegin >= 0 &&
					(caretCtxt == TokenContext.BlockComment ||
					caretCtxt == TokenContext.NestedComment)) {
					
					var charsToInsert = " " + 
						(caretCtxt == TokenContext.BlockComment ?
						'*' :
						'+') + " ";

					var commentBeginIndent = ed.GetLineIndent (ed.GetLineByOffset (lastBegin));

					ed.InsertAtCaret (
						Document.Editor.EolMarker + 
					    commentBeginIndent + 
						(dPolicy.InsertStarAtCommentNewLine ? charsToInsert : ""));
					return false;
				}
			}
			
			if (Document.Editor.Options.IndentStyle == IndentStyle.Smart || Document.Editor.Options.IndentStyle == IndentStyle.Virtual) {
				int newIndentation = 0;

				if (key == Key.Return) {
					ed.InsertAtCaret (Document.Editor.EolMarker);

					var tr = ed.Document.CreateReader();
					var cb = DCodeFormatter.NativeFormatterInstance.CalculateIndentation (tr, ed.Caret.Line);
					tr.Close ();

					newIndentation = cb == null ? 0 : cb.GetLineIndentation (ed.Caret.Line);

					ed.InsertAtCaret (CalculateIndentationString (newIndentation));

					return false;
				}

				else if (keyChar == '{' || keyChar == '}' || keyChar == ':')
				{
					ed.DeleteSelectedText(true);

					ed.InsertAtCaret(keyChar.ToString());

					// Ensure that we aren't in a non-code area right now - automatically trying to format comments is awful
					int lastBegin;
					int lastEnd;
					var caretCtxt = CaretContextAnalyzer.GetTokenContext (ed.Text, ed.Caret.Offset, out lastBegin, out lastEnd);

					if (lastBegin >= 0 && caretCtxt != TokenContext.None)
						return false;

					var origInd = ed.GetLineIndent(ed.Caret.Line);
					int originalIndentation = origInd.Length;

					var tr = ed.Document.CreateReader();
					var cb = DCodeFormatter.NativeFormatterInstance.CalculateIndentation(tr, ed.Caret.Line);
					tr.Close();

					newIndentation = cb == null ? 0 : cb.GetLineIndentation(ed.Caret.Line);
					var newInd = CalculateIndentationString(newIndentation);

					if (origInd != newInd)
						ed.Replace(
							Document.Editor.GetLine(ed.Caret.Line).Offset,
							originalIndentation,
							newInd);

					return false;
				}
			}
			
			return base.KeyPress (key, keyChar, modifier);
		}

		public string CalculateIndentationString (int indentation)
		{
			return Document.Editor.Options.TabsToSpaces ?
				new string (' ', indentation * Document.Editor.Options.TabSize) :
				new string ('\t', indentation);
		}
	}
}
