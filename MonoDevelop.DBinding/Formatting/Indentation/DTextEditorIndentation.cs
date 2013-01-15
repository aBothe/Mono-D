using System;
using D_Parser.Parser;
using Mono.TextEditor;
using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.CodeTemplates;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.SourceEditor;

/*
 * Note: Most of the code was stolen from ./main/src/addins/CSharpBinding/MonoDevelop.CSharp.Formatting/CSharpTextEditorIndentation.cs
 * Credits for the original code go to Mike Krüger.
 * Code fitted to D by Alexander Bothe.
 */

namespace MonoDevelop.D.Formatting.Indentation
{
	public class DTextEditorIndentation:TextEditorExtension
	{
		#region Properties
		DocumentStateTracker<DIndentEngine> stateTracker;
		internal DocumentStateTracker<DIndentEngine> StateTracker { get { return stateTracker; } }
		
		int cursorPositionBeforeKeyPress;
		TextEditorData textEditorData;
		DFormattingPolicy policy;
		TextStylePolicy textStylePolicy;

		char lastCharInserted;

		public static bool OnTheFlyFormatting {
			get {
				return PropertyService.Get ("OnTheFlyFormatting", true);
			}
			set {
				PropertyService.Set ("OnTheFlyFormatting", value);
			}
		}
		#endregion
		
		#region Constructor/Init
		public DTextEditorIndentation()
		{
			var types = MonoDevelop.Ide.DesktopService.GetMimeTypeInheritanceChain (DCodeFormatter.MimeType);
			policy = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<DFormattingPolicy> (types);
			textStylePolicy = MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> (types);
		}
		
		static DTextEditorIndentation ()
		{
			CompletionWindowManager.WordCompleted += delegate(object sender,CodeCompletionContextEventArgs e) {
				var editor = e.Widget as IExtensibleTextEditor;
				if (editor == null)
					return;
				var textEditorExtension = editor.Extension;
				while (textEditorExtension != null && !(textEditorExtension is DTextEditorIndentation)) {
					textEditorExtension = textEditorExtension.Next;
				}
				var extension = textEditorExtension as DTextEditorIndentation;
				if (extension == null)
					return;
				extension.stateTracker.UpdateEngine ();
				if (extension.stateTracker.Engine.NeedsReindent)
					extension.DoReSmartIndent ();
			};
		}
		
		public override void Initialize ()
		{
			base.Initialize ();

			var types = MonoDevelop.Ide.DesktopService.GetMimeTypeInheritanceChain (DCodeFormatter.MimeType);
			if (base.Document.Project != null && base.Document.Project.Policies != null) {
				policy = base.Document.Project.Policies.Get<DFormattingPolicy> (types);
				textStylePolicy = base.Document.Project.Policies.Get<TextStylePolicy> (types);
			}

			textEditorData = Document.Editor;
			if (textEditorData != null) {
				textEditorData.Options.Changed += delegate {
					var project = base.Document.Project;
					if (project != null) {
						policy = project.Policies.Get<DFormattingPolicy> (types);
						textStylePolicy = project.Policies.Get<TextStylePolicy> (types);
					}
					textEditorData.IndentationTracker = new DIndentationTracker (
						textEditorData,
						new DocumentStateTracker<DIndentEngine> (new DIndentEngine (policy, textStylePolicy), textEditorData)
					);
				};
				textEditorData.IndentationTracker = new DIndentationTracker (
					textEditorData,
					new DocumentStateTracker<DIndentEngine> (new DIndentEngine (policy, textStylePolicy), textEditorData)
				);
			}

			// Init tracker
			stateTracker = new DocumentStateTracker<DIndentEngine> (new DIndentEngine (policy, textStylePolicy), textEditorData);
			
			Document.Editor.Paste += HandleTextPaste;
		}
		#endregion
		
		void HandleTextPaste (int insertionOffset, string text, int insertedChars)
		{
			var documentLine = Editor.GetLineByOffset (insertionOffset + insertedChars);
			while (documentLine != null && insertionOffset < documentLine.EndOffset) {
				stateTracker.UpdateEngine (documentLine.Offset);
				DoReSmartIndent (documentLine.Offset);
				documentLine = documentLine.PreviousLine;
			}
		}

		public bool DoInsertTemplate ()
		{
			string word = CodeTemplate.GetWordBeforeCaret (textEditorData);
			foreach (CodeTemplate template in CodeTemplateService.GetCodeTemplates (DCodeFormatter.MimeType)) {
				if (template.Shortcut == word) 
					return true;
			}
			return false;
		}

		public override bool KeyPress (Gdk.Key key, char keyChar, Gdk.ModifierType modifier)
		{
			bool skipFormatting = StateTracker.Engine.IsInsideOrdinaryCommentOrString ||
					StateTracker.Engine.IsInsidePreprocessorDirective;

			cursorPositionBeforeKeyPress = textEditorData.Caret.Offset;

			if (keyChar == ';' && !(textEditorData.CurrentMode is TextLinkEditMode))
				DoInsertTemplate ();
			
			if (key == Gdk.Key.Tab) {
				stateTracker.UpdateEngine ();
				if (stateTracker.Engine.IsInsideStringLiteral && !textEditorData.IsSomethingSelected) {
					var tokenCtxt = CaretContextAnalyzer.GetTokenContext(textEditorData.Document.Text, textEditorData.Caret.Offset);
					if (tokenCtxt == TokenContext.String || tokenCtxt == TokenContext.VerbatimString) {
						textEditorData.InsertAtCaret ("\\t");
						return false;
					}
				}
			}


			if (key == Gdk.Key.Tab && DefaultSourceEditorOptions.Instance.TabIsReindent && !CompletionWindowManager.IsVisible && !(textEditorData.CurrentMode is TextLinkEditMode) && !DoInsertTemplate () && !textEditorData.IsSomethingSelected) {
				int cursor = textEditorData.Caret.Offset;
				if (stateTracker.Engine.IsInsideVerbatimString && cursor > 0 && cursor < textEditorData.Document.TextLength && textEditorData.GetCharAt (cursor - 1) == '"')
					stateTracker.UpdateEngine (cursor + 1);

				if (stateTracker.Engine.IsInsideVerbatimString) {
					// insert normal tab inside r" ... "
					if (textEditorData.IsSomethingSelected) {
						textEditorData.SelectedText = "\t";
					} else {
						textEditorData.Insert (cursor, "\t");
					}
					textEditorData.Document.CommitLineUpdate (textEditorData.Caret.Line);
				} else if (cursor >= 1) {
					if (textEditorData.Caret.Column > 1) {
						int delta = cursor - this.cursorPositionBeforeKeyPress;
						if (delta < 2 && delta > 0) {
							textEditorData.Remove (cursor - delta, delta);
							textEditorData.Caret.Offset = cursor - delta;
							textEditorData.Document.CommitLineUpdate (textEditorData.Caret.Line);
						}
					}
					stateTracker.UpdateEngine ();
					DoReSmartIndent ();
				}
				return false;
			}

			//do the smart indent
			if (textEditorData.Options.IndentStyle == IndentStyle.Smart || textEditorData.Options.IndentStyle == IndentStyle.Virtual) {
				bool retval;
				//capture some of the current state
				int oldBufLen = textEditorData.Length;
				int oldLine = textEditorData.Caret.Line + 1;
				bool hadSelection = textEditorData.IsSomethingSelected;
				bool reIndent = false;

				//pass through to the base class, which actually inserts the character
				//and calls HandleCodeCompletion etc to handles completion
				using (var undo = textEditorData.OpenUndoGroup ()) {
					DoPreInsertionSmartIndent (key);
				}

				bool automaticReindent;
				using (var undo = textEditorData.OpenUndoGroup ()) {

					retval = base.KeyPress (key, keyChar, modifier);

					//handle inserted characters
					if (textEditorData.Caret.Offset <= 0 || textEditorData.IsSomethingSelected)
						return retval;

					lastCharInserted = TranslateKeyCharForIndenter (key, keyChar, textEditorData.GetCharAt (textEditorData.Caret.Offset - 1));
					if (lastCharInserted == '\0')
						return retval;

					stateTracker.UpdateEngine ();

					if (key == Gdk.Key.Return && modifier == Gdk.ModifierType.ControlMask) {
						FixLineStart (textEditorData, stateTracker, textEditorData.Caret.Line + 1);
					} else {
						if (!(oldLine == textEditorData.Caret.Line + 1 && lastCharInserted == '\n') && (oldBufLen != textEditorData.Length || lastCharInserted != '\0'))
							DoPostInsertionSmartIndent (lastCharInserted, hadSelection, out reIndent);
					}
					//reindent the line after the insertion, if needed
					//N.B. if the engine says we need to reindent, make sure that it's because a char was 
					//inserted rather than just updating the stack due to moving around

					stateTracker.UpdateEngine ();
					automaticReindent = (stateTracker.Engine.NeedsReindent && lastCharInserted != '\0');
					if (key == Gdk.Key.Return && (reIndent || automaticReindent))
						DoReSmartIndent ();
				}

				if (key != Gdk.Key.Return && (reIndent || automaticReindent)) {
					using (var undo = textEditorData.OpenUndoGroup ()) {
						DoReSmartIndent ();
					}
				}

				if (!skipFormatting && keyChar == '}') {
					using (var undo = textEditorData.OpenUndoGroup ()) {
						RunFormatter (new DocumentLocation (textEditorData.Caret.Location.Line, textEditorData.Caret.Location.Column));
					}
				}

				stateTracker.UpdateEngine ();
				lastCharInserted = '\0';
				return retval;
			}

			if (textEditorData.Options.IndentStyle == IndentStyle.Auto && DefaultSourceEditorOptions.Instance.TabIsReindent && key == Gdk.Key.Tab) {
				bool retval = base.KeyPress (key, keyChar, modifier);
				DoReSmartIndent ();
				return retval;
			}

			//pass through to the base class, which actually inserts the character
			//and calls HandleCodeCompletion etc to handles completion
			var result = base.KeyPress (key, keyChar, modifier);

			if (!skipFormatting && keyChar == '}')
				RunFormatter (new DocumentLocation (textEditorData.Caret.Location.Line, textEditorData.Caret.Location.Column));
			return result;
		}

		static char TranslateKeyCharForIndenter (Gdk.Key key, char keyChar, char docChar)
		{
			switch (key) {
			case Gdk.Key.Return:
			case Gdk.Key.KP_Enter:
				return '\n';
			case Gdk.Key.Tab:
				return '\t';
			default:
				if (docChar == keyChar)
					return keyChar;
				break;
			}
			return '\0';
		}


		// removes "\s*\+\s*" patterns (used for special behaviour inside strings)
		void HandleStringConcatinationDeletion (int start, int end)
		{
			if (start < 0 || end >= textEditorData.Length || textEditorData.IsSomethingSelected)
				return;
			char ch = textEditorData.GetCharAt (start);
			if (ch == '"') {
				int sgn = Math.Sign (end - start);
				bool foundPlus = false;
				for (int max = start + sgn; max != end && max >= 0 && max < textEditorData.Length; max += sgn) {
					ch = textEditorData.GetCharAt (max);
					if (char.IsWhiteSpace (ch))
						continue;
					if (ch == '+') {
						if (foundPlus)
							break;
						foundPlus = true;
					} else if (ch == '"') {
						if (!foundPlus)
							break;
						if (sgn < 0) {
							textEditorData.Remove (max, start - max);
							textEditorData.Caret.Offset = max + 1;
						} else {
							textEditorData.Remove (start + sgn, max - start);
							textEditorData.Caret.Offset = start;
						}
						break;
					} else {
						break;
					}
				}
			}
		}

		void DoPreInsertionSmartIndent (Gdk.Key key)
		{
			switch (key) {
			case Gdk.Key.BackSpace:
				stateTracker.UpdateEngine ();
				HandleStringConcatinationDeletion (textEditorData.Caret.Offset - 1, 0);
				break;
			case Gdk.Key.Delete:
				stateTracker.UpdateEngine ();
				HandleStringConcatinationDeletion (textEditorData.Caret.Offset, textEditorData.Length);
				break;
			}
		}

		//special handling for certain characters just inserted , for comments etc
		void DoPostInsertionSmartIndent (char charInserted, bool hadSelection, out bool reIndent)
		{
			stateTracker.UpdateEngine ();
			reIndent = false;
			switch (charInserted) {
			case '}':
			case ';':
				reIndent = true;
				break;
			case '\n':
				if (FixLineStart (textEditorData, stateTracker, stateTracker.Engine.LineNumber)) 
					return;
				//newline always reindents unless it's had special handling
				reIndent = true;
				break;
			}
		}

		/// <summary>
		/// Insert e.g. * or + on the freshly created line in order to continue e.g. the multiline comment or string.
		/// </summary>
		public static bool FixLineStart (TextEditorData textEditorData, DocumentStateTracker<DIndentEngine> stateTracker, int lineNumber)
		{
			if (lineNumber > DocumentLocation.MinLine) {
				var line = textEditorData.Document.GetLine (lineNumber);
				if (line == null)
					return false;

				var prevLine = textEditorData.Document.GetLine (lineNumber - 1);
				if (prevLine == null)
					return false;
				string trimmedPreviousLine = textEditorData.Document.GetTextAt (prevLine).TrimStart ();

				//multi-line comments
				if (stateTracker.Engine.IsInsideMultiLineComment) {
					var commentChar = stateTracker.Engine.IsInsideNestedComment ? "+" : "*";
					
					if (textEditorData.GetTextAt (line.Offset, line.Length).TrimStart ().StartsWith (commentChar))
						return false;
					textEditorData.EnsureCaretIsNotVirtual ();
					string commentPrefix = string.Empty;
					if (trimmedPreviousLine.StartsWith (commentChar+" ")) {
						commentPrefix = commentChar+" ";
					} else if (trimmedPreviousLine.StartsWith ("/"+commentChar+commentChar) || trimmedPreviousLine.StartsWith ("/"+commentChar)) {
						commentPrefix = " "+commentChar+" ";
					} else if (trimmedPreviousLine.StartsWith (commentChar)) {
						commentPrefix = commentChar;
					}

					int indentSize = line.GetIndentation (textEditorData.Document).Length;
					var insertedText = prevLine.GetIndentation (textEditorData.Document) + commentPrefix;
					textEditorData.Replace (line.Offset, indentSize, insertedText);
					textEditorData.Caret.Offset = line.Offset + insertedText.Length;
					return true;
				}
			}
			return false;
		}

		//does re-indenting and cursor positioning
		void DoReSmartIndent ()
		{
			DoReSmartIndent (textEditorData.Caret.Offset);
		}

		void DoReSmartIndent (int cursor)
		{
			if (stateTracker.Engine.LineBeganInsideVerbatimString || stateTracker.Engine.LineBeganInsideMultiLineComment)
				return;
			string newIndent = string.Empty;
			DocumentLine line = textEditorData.Document.GetLineByOffset (cursor);
//			stateTracker.UpdateEngine (line.Offset);
			// Get context to the end of the line w/o changing the main engine's state
			var ctx = stateTracker.Engine.Clone () as DIndentEngine;
			for (int max = cursor; max < line.EndOffset; max++) {
				ctx.Push (textEditorData.Document.GetCharAt (max));
			}
			int pos = line.Offset;
			string curIndent = line.GetIndentation (textEditorData.Document);
			int nlwsp = curIndent.Length;
			int offset = cursor > pos + nlwsp ? cursor - (pos + nlwsp) : 0;
			if (!stateTracker.Engine.LineBeganInsideMultiLineComment || (nlwsp < line.LengthIncludingDelimiter && textEditorData.Document.GetCharAt (line.Offset + nlwsp) == '*')) {
				// Possibly replace the indent
				newIndent = ctx.ThisLineIndent;
				int newIndentLength = newIndent.Length;
				if (newIndent != curIndent) {
					if (CompletionWindowManager.IsVisible) {
						if (pos < CompletionWindowManager.CodeCompletionContext.TriggerOffset)
							CompletionWindowManager.CodeCompletionContext.TriggerOffset -= nlwsp;
					}

					newIndentLength = textEditorData.Replace (pos, nlwsp, newIndent);
					textEditorData.Document.CommitLineUpdate (textEditorData.Caret.Line);
					// Engine state is now invalid
					stateTracker.ResetEngineToPosition (pos);
				}
				pos += newIndentLength;
			} else {
				pos += curIndent.Length;
			}

			pos += offset;

			textEditorData.FixVirtualIndentation ();
		}
		
		void RunFormatter (DocumentLocation location)
		{
			if (OnTheFlyFormatting && textEditorData != null && !(textEditorData.CurrentMode is TextLinkEditMode) && !(textEditorData.CurrentMode is InsertionCursorEditMode)) {
				//OnTheFlyFormatter.Format (Document, location);
			}
		}
	}
}
