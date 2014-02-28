using D_Parser.Dom;
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
		AstUpdater updater;

		public override void Initialize()
		{
			base.Initialize();
			updater = new AstUpdater(document, document.Editor);
		}
		#endregion

		#region Code completion
		public override ICompletionDataList CodeCompletionCommand(CodeCompletionContext completionContext)
		{
			int i = 0;
			return HandleCodeCompletion(completionContext, '\0', ref i);
		}

		public override ICompletionDataList HandleCodeCompletion(CodeCompletionContext completionContext, char triggerChar, ref int triggerWordLength)
		{
			var isLetter = char.IsLetter (triggerChar) || triggerChar == '_';

			if (char.IsDigit(triggerChar) || !EnableAutoCodeCompletion && isLetter)
				return null;

			if (isLetter)
			{
				if (completionContext.TriggerOffset > 1){
					var prevChar = document.Editor.GetCharAt(completionContext.TriggerOffset - 2);
					if(char.IsLetterOrDigit(prevChar) || prevChar =='_' || prevChar == '"' || prevChar == '#') // Don't trigger if we're already typing an identifier or if we're typing a string suffix (kinda hacky though)
						return null;
				}
			}
			else if (!(triggerChar==' ' ||
				triggerChar == '@' ||
				triggerChar == '(' ||
				triggerChar == '.' || 
				triggerChar == '\0'))
				return null;
			
			triggerWordLength = isLetter ? 1 : 0;

			// Require a parsed D source
			
			var dom = base.Document.ParsedDocument as ParsedDModule;
			if (dom == null || dom.DDom == null)
				return null;

			updater.FinishUpdate();
			lastTriggerOffset = completionContext.TriggerOffset;
			var l = new CompletionDataList();

			if (D_Parser.Misc.CompletionOptions.Instance.EnableSuggestionMode)
			{
				l.AddKeyHandler(new SuggestionKeyHandler());
				l.AutoCompleteUniqueMatch = false;
				l.AutoCompleteEmptyMatch = false;
				l.AutoSelect = true;
			}
			else
				l.AddKeyHandler(new DoubleUnderScoreWorkaroundHandler(this));

			lock(dom.DDom)
				DCodeCompletionSupport.BuildCompletionData(
					Document,
					dom.DDom,
					completionContext,
					l,
					triggerChar);

			return l.Count != 0 ? l : null;
		}

		class DoubleUnderScoreWorkaroundHandler : ICompletionKeyHandler
		{
			readonly DEditorCompletionExtension ext;

			public DoubleUnderScoreWorkaroundHandler(DEditorCompletionExtension ext)
			{
				this.ext = ext;
			}

			public bool PostProcessKey(CompletionListWindow listWindow, Gdk.Key key, char keyChar, Gdk.ModifierType modifier, out KeyActions keyAction)
			{
				keyAction = KeyActions.None;
				if (keyChar == '_')
				{
					listWindow.PostProcessKey(key, keyChar, modifier);
					return true;
				}

				Mono.TextEditor.TextEditorData ed;
				if (keyChar == '.' && (ed = ext.document.Editor).GetCharAt(ed.Caret.Offset-1) == '.') {
					// optional: Distinguish whether we are in an an index/slice expression and do not close down the completion window if so..
					keyAction = KeyActions.CloseWindow;
					listWindow.PostProcessKey(key, keyChar, modifier);
					return true;
				}

				return false;
			}

			public bool PreProcessKey(CompletionListWindow listWindow, Gdk.Key key, char keyChar, Gdk.ModifierType modifier, out KeyActions keyAction)
			{
				keyAction = KeyActions.None;
				if (keyChar == '_')
				{
					listWindow.PreProcessKey(key, keyChar, modifier);
					return true;
				}
				else if ((keyChar == ' ' || key == Gdk.Key.Return || key == Gdk.Key.ISO_Enter || key == Gdk.Key.Key_3270_Enter || key == Gdk.Key.KP_Enter) && listWindow.CurrentPartialWord == listWindow.CurrentCompletionText)
				{
					keyAction = KeyActions.Process | KeyActions.CloseWindow;
					return true;
				}

				return false;
			}
		}

		class SuggestionKeyHandler : ICompletionKeyHandler
		{
			public bool PostProcessKey(CompletionListWindow listWindow, Gdk.Key key, char keyChar, Gdk.ModifierType modifier, out KeyActions keyAction)
			{
				if (key == Gdk.Key.Return)
					keyAction = KeyActions.Complete;
				else if (key == Gdk.Key.BackSpace)
				{
					keyAction = KeyActions.None;
					return false;
				}
				else if (keyChar != '\0' && !D_Parser.Parser.Lexer.IsIdentifierPart(keyChar))
					keyAction = KeyActions.CloseWindow;
				else
					keyAction = KeyActions.None;
				
				listWindow.PostProcessKey(key, keyChar, modifier);
				return true;
			}

			public bool PreProcessKey(CompletionListWindow listWindow, Gdk.Key key, char keyChar, Gdk.ModifierType modifier, out KeyActions keyAction)
			{
				keyAction = KeyActions.None;
				return false;
			}
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
			updater.BeginUpdate();
			if (this.CompletionWidget != null) {
				if ((keyChar == '(' || keyChar == ')' || keyChar == ';'))
					ParameterInformationWindowManager.HideWindow (this, CompletionWidget);
				else if (lastTriggerOffset >= 0 && char.IsDigit (keyChar)) {

					bool containsDigitsOnly = true;

					for(int offset = lastTriggerOffset; offset < CompletionWidget.CaretOffset; offset++)
						if (!char.IsDigit (CompletionWidget.GetChar(offset))) {
							containsDigitsOnly = false;
							break;
						}

					if(containsDigitsOnly)
						CompletionWindowManager.HideWindow ();
				}
			}

			var ret = base.KeyPress(key, keyChar, modifier);
			updater.FinishUpdate();
			return ret;
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
