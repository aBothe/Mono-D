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
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Resolver;
using D_Parser.Dom;

namespace MonoDevelop.D.Gui
{
	public class DDebugValueTooltipProvider: TooltipProvider, IDisposable
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
		
		public override TooltipItem GetItem (TextEditor editor, int offset)
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

				var editorData = DResolverWrapper.CreateEditorData(doc);
				if (editorData == null)
					return null;
				editorData.CaretOffset = offset;
				var edLoc = ed.OffsetToLocation(offset);
				editorData.CaretLocation = new CodeLocation(edLoc.Column,edLoc.Line);

				var o = DResolver.GetScopedCodeObject(editorData);

				if (o is INode)
					expression = (o as INode).Name;
				else if(o != null)
					expression = o.ToString();
			}
			
			if (string.IsNullOrEmpty (expression))
				return null;
			
			ObjectValue val;
			if (!cachedValues.TryGetValue (expression, out val)) {
				val = frame.GetExpressionValue (expression, true);
				cachedValues [expression] = val;
			}
			
			if (val == null || val.IsUnknown || val.IsError || val.IsNotSupported)
				return null;
			
			val.Name = expression;
			
			return new TooltipItem (val, startOffset, length);
		}
		
		protected override Gtk.Window CreateTooltipWindow (TextEditor editor, int offset, Gdk.ModifierType modifierState, TooltipItem item)
		{
			return new DebugValueWindow (editor, offset, DebuggingService.CurrentFrame, (ObjectValue) item.Item, null);
		}
		
		public override bool IsInteractive (TextEditor editor, Gtk.Window tipWindow)
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

