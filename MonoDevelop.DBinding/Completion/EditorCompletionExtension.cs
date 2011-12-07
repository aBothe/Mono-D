using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using Gtk;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using D_Parser;
using D_Parser.Resolver;
using MonoDevelop.Components.Commands;

namespace MonoDevelop.D
{
	public class DEditorCompletionExtension:CompletionTextEditorExtension
	{
		#region Properties / Init
		public override bool CanRunCompletionCommand(){		return true;	}
		public override bool CanRunParameterCompletionCommand(){	return false;	}

		public override void Initialize()
		{
			base.Initialize();

			
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

		public override void CursorPositionChanged()
		{
			base.CursorPositionChanged();
		}

		public override void TextChanged(int startIndex, int endIndex)
		{
			base.TextChanged(startIndex, endIndex);
		}

		public override bool ExtendsEditor(Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}

		public override bool KeyPress(Gdk.Key key, char keyChar, Gdk.ModifierType modifier)
		{
			return base.KeyPress(key, keyChar, modifier);
		}
	}
}
