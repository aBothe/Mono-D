using System;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Profiler.Gui
{
	public class CodeCoverageEditorExtension : TextEditorExtension
	{
		public override bool ExtendsEditor (MonoDevelop.Ide.Gui.Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}

		public override void Initialize ()
		{
			Document.Window.AttachViewContent (new CodeCoverageView (Document));
			base.Initialize ();
		}
	}
}

