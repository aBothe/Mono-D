using System;
using Mono.TextEditor;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting.Indentation
{
	public class DIndentVirtualSpaceManager: IIndentationTracker
	{
		Mono.TextEditor.TextEditorData data;
		DocumentStateTracker<DIndentEngine> stateTracker;

		public DIndentVirtualSpaceManager (Mono.TextEditor.TextEditorData data, DocumentStateTracker<DIndentEngine> stateTracker)
		{
			this.data = data;
			this.stateTracker = stateTracker;
		}

		string GetIndentationString (int offset, DocumentLocation loc)
		{
			stateTracker.UpdateEngine (Math.Min (data.Length, offset + 1));
			DocumentLine line = data.Document.GetLine (loc.Line);
			if (line == null)
				return "";
			// Get context to the end of the line w/o changing the main engine's state
			var ctx = stateTracker.Engine.Clone () as DIndentEngine;
			for (int max = offset; max < line.Offset + line.Length; max++) {
				ctx.Push (data.Document.GetCharAt (max));
			}
//			int pos = line.Offset;
			string curIndent = line.GetIndentation (data.Document);
			int nlwsp = curIndent.Length;
//			int o = offset > pos + nlwsp ? offset - (pos + nlwsp) : 0;
			if (!stateTracker.Engine.LineBeganInsideMultiLineComment || (nlwsp < line.LengthIncludingDelimiter && data.Document.GetCharAt (line.Offset + nlwsp) == '*')) {
				return ctx.ThisLineIndent;
			}
			return curIndent;
		}
		public string GetIndentationString (int lineNumber, int column)
		{
			return GetIndentationString (data.LocationToOffset (lineNumber, column), new DocumentLocation (lineNumber, column));
		}
		
		public string GetIndentationString (int offset)
		{
			return GetIndentationString (offset, data.OffsetToLocation (offset));
		}

		string GetIndent (int lineNumber, int column)
		{
			var line = data.GetLine (lineNumber);
			if (line == null)
				return "";
			int offset = line.Offset + Math.Min (line.Length, column - 1);
 
			stateTracker.UpdateEngine (offset);
			return stateTracker.Engine.NewLineIndent;
		}

		public int GetVirtualIndentationColumn (int offset)
		{
			return 1 + GetIndentationString (offset).Length;
		}
		
		public int GetVirtualIndentationColumn (int lineNumber, int column)
		{
			return 1 + GetIndentationString (lineNumber, column).Length;
		}
	}
}
