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
using MonoDevelop.Ide.Commands;


namespace MonoDevelop.D
{
	class DEditorCompletionExtension:CompletionTextEditorExtension
	{
		#region Properties / Init
		public override bool CanRunCompletionCommand()
		{
			return documentEditor.CurrentMode is Mono.TextEditor.SimpleEditMode;	
		}
		public override bool CanRunParameterCompletionCommand()
		{
			return documentEditor.CurrentMode is Mono.TextEditor.SimpleEditMode;	
		}
		private Mono.TextEditor.TextEditorData documentEditor;
		
		public override void Initialize()
		{
			base.Initialize();
			
			documentEditor = Document.Editor;	
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
			// Return if e.g. renaming code symbols etc.

			if (!(triggerChar==' ' || 
				char.IsLetter(triggerChar) || 
				triggerChar == '@' ||
				triggerChar == '_' || 
				triggerChar == '.' || 
				triggerChar == '\0'))
				return null;
			else if (char.IsLetter(triggerChar) && !DResolver.IsTypeIdentifier(Document.Editor.Text, Document.Editor.Caret.Offset))
				return null; 
							
			triggerWordLength = (DCodeCompletionSupport.IsIdentifierChar(triggerChar) || triggerChar=='@') ? 1 : 0;

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

		public override bool ExtendsEditor(Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}

		[CommandHandler(Refactoring.Commands.OpenDDocumentation)]
		public void OpenDDocumentation()
		{
			//Refactoring.DDocumentationLauncher.LaunchReferenceInBrowser();
		}
	
		[CommandHandler(Refactoring.Commands.FindReferences)]
		public void FindReferences()
		{

		}

		[CommandHandler(Refactoring.Commands.GotoDeclaration)]
		public void GotoDeclaration()
		{

		}

		[CommandHandler(Refactoring.Commands.RenameSymbols)]
		public void StartRename()
		{
			
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
