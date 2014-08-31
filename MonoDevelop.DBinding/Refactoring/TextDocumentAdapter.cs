using D_Parser.Refactoring;
using Mono.TextEditor;
using MonoDevelop.Ide.Gui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Refactoring
{
	class TextDocumentAdapter : ITextDocument
	{
		readonly TextEditorData doc;

		public TextDocumentAdapter(TextEditorData doc)
		{
			this.doc = doc;
		}

		public int Length
		{
			get {
				return doc.Length;
			}
		}

		public char GetCharAt(int offset)
		{
			return doc.GetCharAt(offset);
		}

		public void Remove(int offset, int length)
		{
			doc.Remove(offset, length);
		}

		public void Insert(int offset, string text)
		{
			doc.Insert(offset, text);
		}

		public int LocationToOffset(int line, int col)
		{
			return doc.LocationToOffset(line, col);
		}

		public int OffsetToLineNumber(int offset)
		{
			return doc.OffsetToLineNumber(offset);
		}

		public string EolMarker
		{
			get { return doc.EolMarker; }
		}

		public string GetLineIndent(int line)
		{
			return doc.GetLineIndent(line);
		}
	}
}
