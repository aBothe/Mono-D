using D_Parser.Dom;
using ICSharpCode.NRefactory.Completion;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D
{
	class DEditorCompletionExtension:CompletionTextEditorExtension
	{
		#region Properties / Init
		int lastTriggerOffset;
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
			char ch = completionContext.TriggerOffset > 0 ? document.Editor.GetCharAt(completionContext.TriggerOffset - 1) : '\0';
			return HandleCodeCompletion(completionContext, ch, ref i);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char triggerChar, ref int triggerWordLength)
		{
			if (!EnableCodeCompletion)
				return null;
			if (!EnableAutoCodeCompletion && char.IsLetter(triggerChar))
				return null;

			if (char.IsLetterOrDigit(triggerChar) || triggerChar == '_')
			{
				if (completionContext.TriggerOffset > 1){
					var prevChar = document.Editor.GetCharAt(completionContext.TriggerOffset - 2);
					if(char.IsLetterOrDigit(prevChar) || prevChar == '"') // Don't trigger if we're already typing an identifier or if we're typing a string suffix (kinda hacky though)
						return null;
				}
			}
			else if (!(triggerChar==' ' ||
				triggerChar == '@' ||
				triggerChar == '(' ||
				triggerChar == '.' || 
				triggerChar == '\0'))
				return null;
			
			triggerWordLength = (char.IsLetter(triggerChar) || triggerChar=='_' || triggerChar=='@') ? 1 : 0;

			// Require a parsed D source
			
			var dom = base.Document.ParsedDocument as ParsedDModule;
			if (dom == null || dom.DDom == null)
				return null;

			var l = new CompletionDataList();
			l.AutoSelect = true;

			lock(dom.DDom)
				DCodeCompletionSupport.BuildCompletionData(
					Document,
					dom.DDom,
					completionContext,
					l,
					triggerChar);

			return l.Count != 0 ? l : null;
		}
		#endregion

		#region Parameter completion
		DParameterDataProvider dParamProv;
		
		public override int GetCurrentParameterIndex(int startOffset)
		{
			var loc = Editor.Document.OffsetToLocation(startOffset);
			
			return dParamProv == null ? 0 : dParamProv.GetCurrentParameterIndex(new CodeLocation(loc.Column,loc.Line));
		}

		public override void CursorPositionChanged()
		{
			if (CompletionWidget != null && Document.Editor.Caret.Offset < lastTriggerOffset)
			{
				ParameterInformationWindowManager.HideWindow(this,CompletionWidget);
				lastTriggerOffset = -1;
			}

			base.CursorPositionChanged();
		}

		public override bool KeyPress(Gdk.Key key, char keyChar, Gdk.ModifierType modifier)
		{
			if (this.CompletionWidget != null && (keyChar == ')' || keyChar == ';'))
				ParameterInformationWindowManager.HideWindow(this, CompletionWidget);

			return base.KeyPress(key, keyChar, modifier);
		}
		
		public override ParameterDataProvider HandleParameterCompletion(CodeCompletionContext completionContext, char completionChar)
		{
			if (completionChar != ',' &&
				completionChar != '(' &&
				completionChar != '!')
			{
				return null;
			}
						
			// Require a parsed D source
			var dom = base.Document.ParsedDocument as ParsedDModule;

			if (dom == null)
				return null;

			lastTriggerOffset=completionContext.TriggerOffset;
			return dParamProv = DParameterDataProvider.Create(Document, dom.DDom, completionContext);
		}
		#endregion

		public override bool ExtendsEditor(Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}
	}
}
