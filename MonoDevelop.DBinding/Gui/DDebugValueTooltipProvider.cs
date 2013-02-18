//
// DDebugValueTooltipProvider.cs
//
// Author:
//		 Lluis Sanchez Gual <lluis@novell.com>
//       Alexander Bothe, info@alexanderbothe.com
//
// Copyright (c) 2013 Alexander Bothe
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Mono.TextEditor;
using System.Collections.Generic;
using MonoDevelop.Debugger;
using MonoDevelop.SourceEditor;
using Mono.Debugging.Client;
using MonoDevelop.D.Parser;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Completion;
using D_Parser.Resolver;

namespace MonoDevelop.D.Gui
{
	public class DDebugValueTooltipProvider: ITooltipProvider, IDisposable
	{
		Dictionary<string,ObjectValue> cachedValues = new Dictionary<string,ObjectValue> ();
		
		public DDebugValueTooltipProvider()
		{
			DebuggingService.CurrentFrameChanged += HandleCurrentFrameChanged;
		}
		
		void HandleCurrentFrameChanged (object sender, EventArgs e)
		{
			// Clear the cached values every time the current frame changes
			cachedValues.Clear ();
		}
		
		#region ITooltipProvider implementation 
		
		public TooltipItem GetItem (Mono.TextEditor.TextEditor editor, int offset)
		{
			if (offset >= editor.Document.TextLength)
				return null;
			
			if (!DebuggingService.IsDebugging || DebuggingService.IsRunning)
				return null;
			
			var frame = DebuggingService.CurrentFrame;
			if (frame == null)
				return null;
			
			var ed = (ExtensibleTextEditor)editor;
			
			string expression = null;
			int startOffset = 0, length = 0;
			if (ed.IsSomethingSelected && offset >= ed.SelectionRange.Offset && offset <= ed.SelectionRange.EndOffset) {
				// This should be handled by the MD-internal Debug value tooltip provider already
				expression = ed.SelectedText;
				startOffset = ed.SelectionRange.Offset;
				length = ed.SelectionRange.Length;
			} else {
				var doc = Ide.IdeApp.Workbench.GetDocument(ed.FileName);
				if(doc == null)
					return null;

				var editorData = DCodeCompletionSupport.CreateEditorData(doc);
				editorData.CaretOffset = offset;
				var edLoc = ed.OffsetToLocation(offset);
				editorData.CaretLocation = new D_Parser.Dom.CodeLocation(edLoc.Column,edLoc.Line);
				var ctxt=ResolutionContext.Create(editorData);

				var o = DResolver.GetScopedCodeObject(editorData, ctxt, 
				                              DResolver.AstReparseOptions.AlsoParseBeyondCaret | 
				                              DResolver.AstReparseOptions.OnlyAssumeIdentifierList);

				if(o != null)
					expression = o.ToString();
			}
			
			if (string.IsNullOrEmpty (expression))
				return null;
			
			ObjectValue val;
			if (!cachedValues.TryGetValue (expression, out val)) {
				val = frame.GetExpressionValue (expression, true);
				cachedValues [expression] = val;
			}
			
			if (val == null || val.IsUnknown || val.IsNotSupported)
				return null;
			
			val.Name = expression;
			
			return new TooltipItem (val, startOffset, length);
		}
		
		public Gtk.Window CreateTooltipWindow (Mono.TextEditor.TextEditor editor, int offset, Gdk.ModifierType modifierState, TooltipItem item)
		{
			return new DebugValueWindow (editor, offset, DebuggingService.CurrentFrame, (ObjectValue) item.Item, null);
		}
		
		public void GetRequiredPosition (Mono.TextEditor.TextEditor editor, Gtk.Window tipWindow, out int requiredWidth, out double xalign)
		{
			xalign = 0.1;
			requiredWidth = tipWindow.SizeRequest ().Width;
		}
		
		public bool IsInteractive (Mono.TextEditor.TextEditor editor, Gtk.Window tipWindow)
		{
			return true;
		}
		#endregion 
		
		#region IDisposable implementation
		public void Dispose ()
		{
			DebuggingService.CurrentFrameChanged -= HandleCurrentFrameChanged;
		}
		#endregion
	}
}

