using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using Mono.TextEditor;
using D_Parser.Dom;
using MonoDevelop.D.Parser;

namespace MonoDevelop.D.Highlighting
{
	public class TypeHighlightingExtension:TextEditorExtension
	{
		TextEditorData textEditorData;
		public IAbstractSyntaxTree SyntaxTree
		{
			get { return (Document.ParsedDocument as ParsedDModule).DDom; }
		}

		public override void Initialize()
		{
			base.Initialize();

			Document.DocumentParsed += Document_DocumentParsed;
		}

		void Document_DocumentParsed(object sender, EventArgs e)
		{
			
		}


		void RefreshMarkers()
		{
			RefreshMarkers();
		}

		void RemoveMarkers()
		{

		}

		class TypeMarker : TextMarker
		{
			public TypeMarker() { 
				
			}
		}
	}
}
