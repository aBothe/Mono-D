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
			triggerWordLength = DCodeCompletionSupport.IsIdentifierChar(triggerChar) ? 1 : 0;

			// Require a parsed D source
			var dom = base.Document.ParsedDocument as DParserWrapper.ParsedDSource;

			if (dom == null)
			{
				return null;
			}

			// Check if in comment or string literal
			if (DCodeResolver.Commenting.IsInCommentAreaOrString(Document.Editor.Text, completionContext.TriggerOffset))
				return null;

			var l = new CompletionDataList();

			DCodeCompletionSupport.Instance.BuildCompletionData(Document,dom.DDom,completionContext,l,triggerChar.ToString());

			return l;
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
			return base.HandleParameterCompletion(completionContext, completionChar);
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
			return true;
		}

		public override bool KeyPress(Gdk.Key key, char keyChar, Gdk.ModifierType modifier)
		{
			return base.KeyPress(key, keyChar, modifier);
		}
	}
}
