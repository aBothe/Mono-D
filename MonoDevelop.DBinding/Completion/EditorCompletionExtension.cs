using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gtk;
using MonoDevelop.Core;
using MonoDevelop.Components;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Output;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Gui;
using MonoDevelop.D.Parser;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Completion;
using D_Parser.Resolver;


namespace MonoDevelop.D
{
	public class DEditorCompletionExtension:CompletionTextEditorExtension, IPathedDocument
	{
		#region Properties / Init
		public override bool CanRunCompletionCommand(){		return true;	}
		public override bool CanRunParameterCompletionCommand(){	return false;	}
		private Mono.TextEditor.TextEditorData documentEditor;
		
		public override void Initialize()
		{
			base.Initialize();
			
			documentEditor = Document.Editor;
			UpdatePath (null, null);
			documentEditor.Caret.PositionChanged += UpdatePath;
			Document.DocumentParsed += delegate { UpdatePath (null, null); };			
		}

		#endregion

		#region Code completion

		public override ICompletionDataList CodeCompletionCommand(CodeCompletionContext completionContext)
		{
			int i = 0;
			return HandleCodeCompletion(completionContext,'\0',ref i);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char completionChar)
		{
			int i = 0;
			return HandleCodeCompletion(completionContext, completionChar, ref i);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char triggerChar, ref int triggerWordLength)
		{
			if (!(triggerChar==' ' || char.IsLetter(triggerChar) || triggerChar == '_' || triggerChar == '.' || triggerChar == '\0'))
				return null;
			else if ((char.IsLetter(triggerChar) && !DResolver.IsTypeIdentifier(Document.Editor.Text, Document.Editor.Caret.Offset)))
				return null; 
							
			triggerWordLength = DCodeCompletionSupport.IsIdentifierChar(triggerChar) ? 1 : 0;

			// Require a parsed D source
			var dom = base.Document.ParsedDocument as ParsedDModule;

			if (dom == null)
			{
				return null;
			}

			// Check if in comment or string literal
			if (DResolver.CommentSearching.IsInCommentAreaOrString(Document.Editor.Text, completionContext.TriggerOffset))
				return null;

			var l = new CompletionDataList();

			DCodeCompletionSupport.BuildCompletionData(Document,dom.DDom,completionContext,l,triggerChar=='\0'?"":triggerChar.ToString());

			return l;
		}

		// Taken from CSharpTextEditorCompletion.cs
		public override bool GetCompletionCommandOffset(out int cpos, out int wlen)
		{
			cpos = wlen = 0;
			int pos = Editor.Caret.Offset - 1;
			while (pos >= 0)
			{
				char c = Editor.GetCharAt(pos);
				if (!char.IsLetterOrDigit(c) && c != '_')
					break;
				pos--;
			}
			if (pos == -1)
				return false;

			pos++;
			cpos = pos;
			int len = Editor.Length;

			while (pos < len)
			{
				char c = Editor.GetCharAt(pos);
				if (!char.IsLetterOrDigit(c) && c != '_')
					break;
				pos++;
			}
			wlen = pos - cpos;
			return true;
		}

		#endregion

		#region Parameter completion

		public override IParameterDataProvider ParameterCompletionCommand(CodeCompletionContext completionContext)
		{
			return base.ParameterCompletionCommand(completionContext);
		}

		public override bool GetParameterCompletionCommandOffset(out int cpos)
		{
			return base.GetParameterCompletionCommandOffset(out cpos);
		}

		public override IParameterDataProvider HandleParameterCompletion(CodeCompletionContext completionContext, char completionChar)
		{
			if (!(((completionChar == ',') && (!ParameterInformationWindowManager.IsWindowVisible)) || completionChar == '(' || completionChar=='!'))
				return null;
						
			// Require a parsed D source
			var dom = base.Document.ParsedDocument as ParsedDModule;

			if (dom == null)
				return null;

			return DParameterDataProvider.Create(Document, dom.DDom, completionContext);
		}

		public override void RunParameterCompletionCommand()
		{
			base.RunParameterCompletionCommand();
		}

		#endregion

		#region Code Templates

		public override void RunShowCodeTemplatesWindow()
		{
			base.RunShowCodeTemplatesWindow();
		}

		public override ICompletionDataList ShowCodeTemplatesCommand(CodeCompletionContext completionContext)
		{
			return base.ShowCodeTemplatesCommand(completionContext);
		}

		#endregion

		public override bool ExtendsEditor(Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}
		
		
		#region IPathedDocument implementation
		public event EventHandler<DocumentPathChangedEventArgs> PathChanged;
			
		public Widget CreatePathWidget (int index)
		{
			PathEntry[] path = CurrentPath;
			if (null == path || 0 > index || path.Length <= index) 
			{
				return null;
			}
			
			object tag = path[index].Tag;
			DropDownBoxListWindow.IListDataProvider provider = null;
			if (!((tag is D_Parser.Dom.IBlockNode) || (tag is DEnumValue) || (tag is NoSelectionCustomNode)))
			{
				return null;
			} 
			provider = new EditorPathbarProvider (Document, tag);
			
			DropDownBoxListWindow window = new DropDownBoxListWindow (provider);
			window.SelectItem (tag);
			return window;
		}	

		public MonoDevelop.Components.PathEntry[] CurrentPath 
		{
			get;
			private set;
		}
		
		protected virtual void OnPathChanged (DocumentPathChangedEventArgs args)
		{
			if (null != PathChanged) {
				PathChanged (this, args);
			}
		}		
		#endregion
		
		
		private void UpdatePath (object sender, Mono.TextEditor.DocumentLocationEventArgs e)
		{
			var ast = Document.ParsedDocument as ParsedDModule;
			if (ast == null)
				return;
			
			var SyntaxTree = ast.DDom;

			if (SyntaxTree == null)
				return;		
			
			// Resolve the hovered piece of code
			var loc = new CodeLocation(documentEditor.Caret.Location.Column, documentEditor.Caret.Location.Line);			
			IStatement stmt = null;
			DNode currentblock = (DNode) DResolver.SearchBlockAt(SyntaxTree, loc, out stmt);
			
			//could be an enum value, which is not IBlockNode
			if (currentblock is DEnum)
			{
				foreach(DNode nd in (currentblock as DEnum).Children)
				{
					if ((nd is DEnumValue) 
					&& ((nd.StartLocation <= loc) && (nd.EndLocation >= loc)))
					{
						currentblock = nd;
						break;
					}
				}
			}
			
			List<PathEntry> result = new List<PathEntry> ();
			D_Parser.Dom.INode node = currentblock;			
					
			while ((node != null) && ((node is IBlockNode) || (node is DEnumValue))) {
				PathEntry entry;										
				
				var icon=DCompletionData.GetNodeIcon(node as DNode);					
				entry = new PathEntry (ImageService.GetPixbuf(icon.Name, IconSize.Menu), node.Name + DParameterDataProvider.GetNodeParamString(node));
				entry.Position = EntryPosition.Left;
				entry.Tag = node;
				//do not include the module in the path bar
				if ((node.Parent != null) && !((node is DNode) && (node as DNode).IsAnonymous))
					result.Insert (0, entry);
				node = node.Parent;
			}						
			
			if (!((currentblock is DMethod) || (currentblock is DEnumValue))) {
				PathEntry noSelection = new PathEntry (GettextCatalog.GetString ("No Selection")) { Tag =  new NoSelectionCustomNode (currentblock) };
				result.Add (noSelection);
			}
			
			var prev = CurrentPath;
			CurrentPath = result.ToArray ();
			OnPathChanged (new DocumentPathChangedEventArgs (prev));
		}	
	}
	

	class NoSelectionCustomNode : DNode
	{
		public NoSelectionCustomNode (D_Parser.Dom.INode parent)
		{
			this.Parent = parent;
		}		
	}	
}
