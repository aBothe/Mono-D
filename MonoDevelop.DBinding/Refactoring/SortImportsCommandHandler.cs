//
// SortImportsCommandHandler.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
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
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Core;
using D_Parser.Refactoring;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Parser;
using D_Parser.Dom;
using MonoDevelop.D.Refactoring;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Refactoring
{
	class SortImportsCommandHandler : CommandHandler
	{
		const string SortImportsSeparatePackagesFromEachOtherPropId = "MonoDevelop.D.SortImportsSeparatePackagesFromEachOther";
		public static bool SortImportsSeparatePackagesFromEachOther
		{
			get{ return PropertyService.Get (SortImportsSeparatePackagesFromEachOtherPropId, false); }
			set{ PropertyService.Set (SortImportsSeparatePackagesFromEachOtherPropId, value); }
		}

		public static bool CanSortImports(Document doc = null)
		{
			doc = doc ?? IdeApp.Workbench.ActiveDocument;

			if (doc == null)
				return false;

			var ddoc = doc.ParsedDocument as ParsedDModule;
			if (ddoc == null || ddoc.DDom == null)
				return false;

			return DResolver.SearchBlockAt (ddoc.DDom, new CodeLocation(doc.Editor.Caret.Column, doc.Editor.Caret.Line)) is DBlockNode;
		}

		public static void SortImports(Document doc = null)
		{
			doc = doc ?? IdeApp.Workbench.ActiveDocument;

			if (doc == null)
				return;

			var ddoc = doc.ParsedDocument as ParsedDModule;
			if (ddoc == null || ddoc.DDom == null)
				return;

			var scope = DResolver.SearchBlockAt (ddoc.DDom, new CodeLocation(doc.Editor.Caret.Column, doc.Editor.Caret.Line)) as DBlockNode;

			if (scope == null)
				return;

			var editor = doc.Editor;
			using (editor.Document.OpenUndoGroup (Mono.TextEditor.OperationType.Undefined)) {
				SortImportsRefactoring.SortImports(scope, new TextDocumentAdapter(editor), SortImportsSeparatePackagesFromEachOther);
			}

			editor.Parent.TextViewMargin.PurgeLayoutCache();
			editor.Parent.QueueDraw();
		}

		protected override void Update (CommandInfo info)
		{
			if (!CanSortImports(info.DataItem as Document))
			{
				info.Bypass = true;
				return;
			}

			base.Update (info);
		}

		protected override void Run (object dataItem)
		{
			SortImports (dataItem as Document);
		}
	}
}

