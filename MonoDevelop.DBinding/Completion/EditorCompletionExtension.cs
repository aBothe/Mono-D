using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using Gtk;

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

		#region

		#region Code completion

		public override ICompletionDataList CodeCompletionCommand(CodeCompletionContext completionContext)
		{
			var l = new CompletionDataList();

			l.Add("An item", Core.IconId.Null, "description", "itemId");

			return l;
		}

		public override bool GetCompletionCommandOffset(out int cpos, out int wlen)
		{
			return base.GetCompletionCommandOffset(out cpos, out wlen);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char completionChar)
		{
			return base.HandleCodeCompletion(completionContext, completionChar);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char completionChar, ref int triggerWordLength)
		{
			return base.HandleCodeCompletion(completionContext, completionChar, ref triggerWordLength);
		}

		public override void RunCompletionCommand()
		{
			base.RunCompletionCommand();
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
			return DLanguageBinding.IsDFile(doc.FileName);
		}

		public override bool KeyPress(Gdk.Key key, char keyChar, Gdk.ModifierType modifier)
		{
			return base.KeyPress(key, keyChar, modifier);
		}
	}
}
