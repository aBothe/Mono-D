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

namespace MonoDevelop.D.Formatting
{
	public class DTextEditorIndentation:TextEditorExtension
	{
		D_Parser.Formatting.DFormatter formatter = new D_Parser.Formatting.DFormatter();

		static DTextEditorIndentation()
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

		public override void Initialize()
		{
			base.Initialize();
		}

		public override bool KeyPress(Key key, char keyChar, ModifierType modifier)
		{
			var ed = Document.Editor;

			if (key == Key.Return)
			{
				ed.DeleteSelectedText(true);

				int lastBegin;
				int lastEnd;
				var caretCtxt = CaretContextAnalyzer.GetTokenContext(ed.Text, ed.Caret.Offset, out lastBegin, out lastEnd);

				if (lastBegin>=0 &&
					(caretCtxt == TokenContext.BlockComment ||
					caretCtxt == TokenContext.NestedComment))
				{
					char charToInsert =
						caretCtxt == TokenContext.BlockComment ?
						'*' :
						'+';

					var prevLineIndent=ed.GetLineIndent(ed.GetLineByOffset(lastBegin));

					ed.InsertAtCaret(Document.Editor.EolMarker +prevLineIndent+' '+charToInsert+' ');
					return false;
				}
			}
			
			if(TextEditorProperties.IndentStyle == IndentStyle.Smart)
			{
				int newIndentation = 0;

				if (key== Key.Return)
				{
					ed.InsertAtCaret(Document.Editor.EolMarker);

					var cb=DCodeFormatter.NativeFormatterInstance.CalculateIndentation(ed.Text,ed.Caret.Line);

					newIndentation=cb==null?0:cb.GetLineIndentation(ed.Caret.Line);

					ed.InsertAtCaret(CalculateIndentationString(newIndentation));

					return false;
				}

				if (keyChar=='{' || keyChar == '}')
				{
					ed.DeleteSelectedText(true);

					ed.InsertAtCaret(keyChar.ToString());

					int originalIndentation = ed.GetLineIndent(ed.Caret.Line).Length;

					var cb = DCodeFormatter.NativeFormatterInstance.CalculateIndentation(ed.Text, ed.Caret.Line);
					
					newIndentation= cb == null ? 0 : cb.GetLineIndentation(ed.Caret.Line);

					var newInd=CalculateIndentationString(newIndentation);
					var line=Document.Editor.GetLine(ed.Caret.Line);

					ed.Replace(
						line.Offset, 
						originalIndentation,
						newInd);

					ed.Caret.Offset += newInd.Length - originalIndentation;

					return false;
				}
			}
			
			return base.KeyPress(key, keyChar, modifier);
		}

		public static string CalculateIndentationString(int indentation)
		{
			return DefaultSourceEditorOptions.Instance.TabsToSpaces ?
				new string(' ', indentation * DefaultSourceEditorOptions.Instance.TabSize) :
				new string('\t', indentation);
		}
	}
}
