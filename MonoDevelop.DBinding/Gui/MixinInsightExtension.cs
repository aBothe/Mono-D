using System;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Gui
{
	public class MixinInsightExtension : TextEditorExtension
	{
		ExpressionEvaluationPad pad;

		public override void CursorPositionChanged ()
		{
			base.CursorPositionChanged ();

			if(pad != null)
				pad.Update(Document);
		}

		public override bool ExtendsEditor (MonoDevelop.Ide.Gui.Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}

		public MixinInsightExtension ()
		{
			var p = Ide.IdeApp.Workbench.GetPad<ExpressionEvaluationPad>();
			if(p != null)
				pad = p.Content as ExpressionEvaluationPad;
		}
	}
}

