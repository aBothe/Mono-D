using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using Gdk;
using D_Parser.Formatting;
using D_Parser.Resolver;

namespace MonoDevelop.D.Formatting
{
	public class DTextEditorIndentation:TextEditorExtension
	{
		D_Parser.Formatting.DFormatter formatter = new D_Parser.Formatting.DFormatter();

		public override void Initialize()
		{
			base.Initialize();
		}

		public override bool KeyPress(Key key, char keyChar, ModifierType modifier)
		{
			var ed = Document.Editor;

			if (key == Key.Return)
			{
				int lastBegin;
				int lastEnd;
				var caretCtxt=DResolver.CommentSearching.GetTokenContext(ed.Text, ed.Caret.Offset,out lastBegin, out lastEnd);

				if (lastBegin>=0 &&
					(caretCtxt == DResolver.CommentSearching.TokenContext.BlockComment ||
					caretCtxt == DResolver.CommentSearching.TokenContext.NestedComment))
				{
					char charToInsert=
						caretCtxt== DResolver.CommentSearching.TokenContext.BlockComment?
						'*':
						'+';

					var prevLineIndent=ed.GetLineIndent(ed.GetLineByOffset(lastBegin));

					ed.InsertAtCaret('\n'+prevLineIndent+' '+charToInsert+' ');
					return false;
				}
			}
			
			if(TextEditorProperties.IndentStyle == IndentStyle.Smart)
			{
				if (key== Key.Return)
				{
					var cb=D_Parser.Formatting.DFormatter.CalculateIndentation(ed.Text,ed.Caret.Offset);

					ed.InsertAtCaret("\n" + CalculateIndentationString(cb==null?0:cb.InnerIndentation));

					return false;
				}

				if (keyChar == '}' || keyChar == ':' || keyChar == ';')
				{
					ed.InsertAtCaret(keyChar.ToString());

					var cb = D_Parser.Formatting.DFormatter.CalculateIndentation(ed.Text, ed.Caret.Offset);

					var line=Document.Editor.GetLine(ed.Caret.Line);

					ed.Replace(
						line.Offset, 
						ed.GetLineIndent(line).Length, 
						CalculateIndentationString(cb == null ? 0 : cb.InnerIndentation));

					return false;
				}
			}
			
			return base.KeyPress(key, keyChar, modifier);
		}

		public static string CalculateIndentationString(int indentation)
		{
			return TextEditorProperties.ConvertTabsToSpaces ?
				new string(' ', indentation * TextEditorProperties.TabIndent) :
				new string('\t', indentation);
		}
	}
}
