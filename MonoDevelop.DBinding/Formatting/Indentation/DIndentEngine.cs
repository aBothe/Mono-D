using System;
using System.Collections;
using System.Linq;
using System.Text;

using D_Parser.Parser;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting.Indentation
{
	public class DIndentEngine : ICloneable, IDocumentStateEngine
	{
		#region Properties
		public DFormattingPolicy Policy;
		public TextStylePolicy TextStyle;
		
		IndentStack stack;
		
		// Ponder: should linebuf be dropped in favor of a
		// 'wordbuf' and a 'int curLineLen'? No real need to
		// keep a full line buffer.
		StringBuilder linebuf;
		
		byte keyword;
		
		string curIndent;
		
		Inside beganInside;
		
		bool needsReindent;
		bool popVerbatim;
		bool canBeLabel;
		bool isEscaped;
		
		int firstNonLwsp;
		int lastNonLwsp;
		int wordStart;
		
		char lastChar;
		
		/// <summary>
		/// Previous char in the line
		/// </summary>
		char pc;
		
		/// <summary>
		/// last significant (real) char in the line
		/// (e.g. non-whitespace, not in a comment, etc)
		/// </summary>
		char rc;
		
		/// <summary>
		/// previous last significant (real) char in the line
		/// </summary>
		char prc;
		
		int curLineNr;
		int cursor;
		
		public int Position {
			get { return cursor; }
		}
		
		public int LineNumber {
			get { return curLineNr; }
		}
		
		public int LineOffset {
			get { return linebuf.Length; }
		}
		
		public bool NeedsReindent {
			get { return needsReindent; }
		}
		
		public int StackDepth {
			get { return stack.Count; }
		}
		
		public bool IsInsideVerbatimString {
			get { return stack.PeekInside (0) == Inside.VerbatimString; }
		}
		
		public bool IsInsideMultiLineComment {
			get { var i = stack.PeekInside (0);
				return i == Inside.BlockComment || i == Inside.NestedComment; }
		}
		
		public bool IsInsideNestedComment{
			get{ return stack.PeekInside(0) == Inside.NestedComment; }
		}
		
		public bool IsInsideDocLineComment {
			get { return stack.PeekInside (0) == Inside.DocComment; }
		}
		
		public bool LineBeganInsideVerbatimString {
			get { return beganInside == Inside.VerbatimString; }
		}
		
		public bool LineBeganInsideMultiLineComment {
			get { return beganInside == Inside.BlockComment || beganInside == Inside.NestedComment; }
		}
		
		public bool IsInsidePreprocessorDirective {
			get { return stack.PeekInside (0) == Inside.PreProcessor; }
		}
		
		public bool IsInsideOrdinaryCommentOrString {
			get { return (stack.PeekInside (0) & (Inside.LineComment | Inside.NestedComment | Inside.BlockComment | Inside.StringOrChar)) != 0; }
		}
		
		public bool IsInsideComment {
			get { return (stack.PeekInside (0) & (Inside.LineComment | Inside.NestedComment | Inside.BlockComment | Inside.DocComment)) != 0; }
		}
		
		public bool IsInsideStringLiteral {
			get { return (stack.PeekInside (0) & (Inside.StringLiteral)) != 0; }
		}
		
		static string TabsToSpaces (string indent, int indentWith = 4)
		{
			StringBuilder builder;

			if (indent == string.Empty)
				return string.Empty;
			
			builder = new StringBuilder ();
			for (int i = 0; i < indent.Length; i++) {
				if (indent[i] == '\t')
					builder.Append (' ', indentWith);
				else
					builder.Append (indent[i]);
			}
			
			return builder.ToString ();
		}
		
		public string ThisLineIndent {
			get {
				if (TextStyle.TabsToSpaces)
					return TabsToSpaces (curIndent, TextStyle.IndentWidth);
				
				return curIndent;
			}
		}
		
		public string NewLineIndent {
			get {
				if (TextStyle.TabsToSpaces)
					return TabsToSpaces (stack.PeekIndent (0), TextStyle.IndentWidth);
				
				return stack.PeekIndent (0);
			}
		}
		#endregion
		
		#region Constructor/Init
		public DIndentEngine(DFormattingPolicy policy, TextStylePolicy textStyle)
		{
			if (policy == null)
				throw new ArgumentNullException ("policy");
			if (textStyle == null)
				throw new ArgumentNullException ("textPolicy");
			this.Policy = policy;
			this.TextStyle = textStyle;
			stack = new IndentStack (this);
			linebuf = new StringBuilder ();
			Reset ();
		}
		#endregion
		
		#region Helpers
		public void Reset ()
		{
			stack.Reset ();

			linebuf.Length = 0;

			keyword = DTokens.INVALID;
			curIndent = String.Empty;

			needsReindent = false;
			popVerbatim = false;
			canBeLabel = true;
			isEscaped = false;

			firstNonLwsp = -1;
			lastNonLwsp = -1;
			wordStart = -1;

			prc = '\0';
			pc = '\0';
			rc = '\0';
			lastChar = '\0';
			curLineNr = 1;
			cursor = 0;
		}
		
		public object Clone ()
		{
			DIndentEngine engine = new DIndentEngine (Policy, TextStyle);
			
			engine.stack = (IndentStack) stack.Clone ();
			engine.linebuf = new StringBuilder (linebuf.ToString (), linebuf.Capacity);
			
			engine.keyword = keyword;
			engine.curIndent = curIndent;
			
			engine.needsReindent = needsReindent;
			engine.popVerbatim = popVerbatim;
			engine.canBeLabel = canBeLabel;
			engine.isEscaped = isEscaped;
			
			engine.firstNonLwsp = firstNonLwsp;
			engine.lastNonLwsp = lastNonLwsp;
			engine.wordStart = wordStart;
			
			engine.prc = prc;
			engine.pc = pc;
			engine.rc = rc;
			
			engine.curLineNr = curLineNr;
			engine.cursor = cursor;
			
			return engine;
		}
		
		void TrimIndent ()
		{
			switch (stack.PeekInside (0)) {
			case Inside.FoldedStatement:
			case Inside.Block:
			case Inside.Case:
				if (curIndent == String.Empty)
					return;
				
				// chop off the last tab (e.g. unindent 1 level)
				curIndent = curIndent.Substring (0, curIndent.Length - 1);
				break;
			default:
				curIndent = stack.PeekIndent (0);
				break;
			}
		}
		#endregion
		
		static BitArray specialKeywords = DTokens.NewSet(
			DTokens.Foreach,
			DTokens.Foreach_Reverse, 
			DTokens.While, 
			DTokens.For, 
			DTokens.While, 
			DTokens.If, 
			DTokens.Else);
		
		static BitArray interestingKeywords = DTokens.NewSet(
			DTokens.Import,
			DTokens.Interface,
			DTokens.Struct,
			DTokens.Class,
			DTokens.Template,
			DTokens.Enum,
			DTokens.Switch,
			DTokens.Case,
			
			DTokens.This,
			DTokens.Super,
			DTokens.Assign,
			DTokens.Return,
			
			DTokens.Foreach, 
			DTokens.Foreach_Reverse, 
			DTokens.While, 
			DTokens.For, 
			DTokens.While, 
			DTokens.If, 
			DTokens.Else);
		
		static bool KeywordIsSpecial (byte keyword)
		{
			return specialKeywords[keyword];
		}
		
		// Check to see if linebuf contains a keyword we're interested in (not all keywords)
		byte WordIsKeyword ()
		{
			var kw = DTokens.GetTokenID(linebuf.ToString (wordStart, Math.Min (linebuf.Length - wordStart, 15)));
			
			if(interestingKeywords[kw])
				return kw;
			return DTokens.INVALID;
		}
		
		bool WordIsDefault
		{
			get{
				var str = linebuf.ToString (wordStart, linebuf.Length - wordStart).Trim ();
				
				return str == "default";
			}
		}
		
		bool Folded2LevelsNonSpecial ()
		{
			return stack.PeekInside (0) == Inside.FoldedStatement &&
				stack.PeekInside (1) == Inside.FoldedStatement &&
				!KeywordIsSpecial (stack.PeekKeyword) &&
				!KeywordIsSpecial (keyword);
		}
		
		bool FoldedClassDeclaration ()
		{
			return stack.PeekInside (0) == Inside.FoldedStatement &&
				(keyword == DTokens.Super || keyword == DTokens.Class || keyword == DTokens.Interface || keyword == DTokens.Template);
		}
		
		void PushFoldedStatement ()
		{
			string indent = null;
			
			// Note: nesting of folded statements stops after 2 unless a "special" folded
			// statement is introduced, in which case the cycle restarts
			//
			// Note: We also try to only fold class declarations once
			
			if (Folded2LevelsNonSpecial () || FoldedClassDeclaration ())
				indent = stack.PeekIndent (0);
			
			if (indent != null)
				stack.Push (Inside.FoldedStatement, keyword, curLineNr, 0, indent);
			else
				stack.Push (Inside.FoldedStatement, keyword, curLineNr, 0);
			
			keyword = DTokens.INVALID;
		}
		
		#region Handlers for specific characters
		void PushHash (Inside inside)
		{
			if(this.cursor == 0)
			{
				stack.Push(Inside.Shebang, DTokens.INVALID, curLineNr, 0);
				curIndent = string.Empty;
				needsReindent = false;
				return;
			}
			
			// ignore if we are inside a string, char, or comment
			if ((inside & (Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			// ignore if '#' is not the first significant char on the line
			if (rc != '\0')
				return;
			
			stack.Push (Inside.PreProcessor, DTokens.INVALID, curLineNr, 0);
			
			curIndent = String.Empty;
			needsReindent = false;
		}
		
		void PushSlash (Inside inside)
		{
			// ignore these
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar )) != 0)
				return;
			if (inside == Inside.LineComment) {
				stack.Pop (); // pop line comment
				stack.Push (Inside.DocComment, keyword, curLineNr, 0);
			} else if (inside == Inside.BlockComment) {
				// check for end of multi-line comment block
				if (pc == '*') {
					// restore the keyword and pop the multiline comment
					keyword = stack.PeekKeyword;
					stack.Pop ();
				}
			}else if (inside == Inside.NestedComment) {
				if (pc == '+') {
					keyword = stack.PeekKeyword;
					stack.Pop ();
				}
			} else {
				
				// FoldedStatement, Block, Attribute or ParenList
				// check for the start of a single-line comment
				if (pc == '/') {
					stack.Push (Inside.LineComment, keyword, curLineNr, 0);
					
					// drop the previous '/': it belongs to this comment
					rc = prc;
				}
			}
		}
		
		void PushBackSlash (Inside inside)
		{
			// string and char literals can have \-escapes
			if ((inside & (Inside.StringLiteral | Inside.CharLiteral)) != 0)
				isEscaped = !isEscaped;
		}
		
		void PushStar (Inside inside, char c)
		{
			int n;
			
			if (pc != '/')
				return;
			
			//TODO: Multiline ddoc comments(?)
			
			// got a "/*" - might start a MultiLineComment
			if ((inside & (Inside.StringOrChar | Inside.Comment)) != 0) {
//				if ((inside & Inside.MultiLineComment) != 0)
//					Console.WriteLine ("Watch out! Nested /* */ comment detected!");
				return;
			}
			
			// push a new multiline comment onto the stack
			if (inside != Inside.PreProcessor)
				n = linebuf.Length - firstNonLwsp;
			else
				n = linebuf.Length;
			
			stack.Push (c == '*' ? Inside.BlockComment : Inside.NestedComment, keyword, curLineNr, n);
			
			// drop the previous '/': it belongs to this comment block
			rc = prc;
		}
		
		void PushQuote (Inside inside)
		{
			Inside type;
			
			// ignore if in these
			if ((inside & (Inside.PreProcessor | Inside.Comment | Inside.CharLiteral)) != 0)
				return;
			
			if (inside == Inside.VerbatimString) {
				if (popVerbatim) {
					// back in the verbatim-string-literal token
					popVerbatim = false;
				} else {
					/* need to see the next char before we pop the
					 * verbatim-string-literal */
					popVerbatim = true;
				}
			} else if (inside == Inside.StringLiteral) {
				// check that it isn't escaped
				if (!isEscaped) {
					keyword = stack.PeekKeyword;
					stack.Pop ();
				}
			} else {
				// FoldedStatement, Block, Attribute or ParenList
				if (pc == 'r')
					type = Inside.VerbatimString;
				else
					type = Inside.StringLiteral;
				
				// push a new string onto the stack
				stack.Push (type, keyword, curLineNr, 0);
			}
		}
		
		void PushSQuote (Inside inside)
		{
			if (inside == Inside.CharLiteral) {
				// check that it's not escaped
				if (isEscaped)
					return;
				
				keyword = stack.PeekKeyword;
				stack.Pop ();
				return;
			}
			
			if ((inside & (Inside.PreProcessor | Inside.String | Inside.Comment)) != 0) {
				// won't be starting a CharLiteral, so ignore it
				return;
			}
			
			// push a new char literal onto the stack 
			stack.Push (Inside.CharLiteral, keyword, curLineNr, 0);
		}
		
		void PushColon (Inside inside)
		{
			if (inside != Inside.Block && inside != Inside.Case)
				return;
			
			// can't be a case/label if there's no preceeding text
			if (wordStart == -1)
				return;
			
			// goto-label or case statement
			if (keyword == DTokens.Case || keyword == DTokens.Default) {
				// case (or default) statement
				if (stack.PeekKeyword != DTokens.Switch)
					return;
				
				if (inside == Inside.Case) {
					stack.Pop ();
					
					string newIndent = stack.PeekIndent (0);
					if (curIndent != newIndent) {
						curIndent = newIndent;
						needsReindent = true;
					}
				}
				
				if (!Policy.IndentSwitchBody) {
					needsReindent = true;
					TrimIndent ();
				}
				
				stack.Push (Inside.Case, DTokens.Switch, curLineNr, 0);
			} else if (canBeLabel) {
				var style = Policy.LabelIndentStyle;
				// indent goto labels as specified
				switch (style) {
				case GotoLabelIndentStyle.LeftJustify:
					needsReindent = true;
			//		curIndent = " ";
					break;
				case GotoLabelIndentStyle.OneLess:
					needsReindent = true;
					TrimIndent ();
			//		curIndent += " ";
					break;
				default:
					break;
				}
				canBeLabel = false;
			}
		}
		
		void PushSemicolon (Inside inside)
		{
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			if (inside == Inside.FoldedStatement) {
				// chain-pop folded statements
				while (stack.PeekInside (0) == Inside.FoldedStatement)
					stack.Pop ();
			}
			
			keyword = DTokens.INVALID;
		}
		
		void PushOpenSq (Inside inside)
		{
			int n = 1;
			
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			// push a new attribute onto the stack
			if (firstNonLwsp != -1)
				n += linebuf.Length - firstNonLwsp;
			
			stack.Push (Inside.Attribute, keyword, curLineNr, n);
		}
		
		void PushCloseSq (Inside inside)
		{
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			if (inside != Inside.Attribute) {
				//Console.WriteLine ("can't pop a '[' if we ain't got one?");
				return;
			}
			
			// pop this attribute off the stack
			keyword = stack.PeekKeyword;
			stack.Pop ();
		}
		
		void PushOpenParen (Inside inside)
		{
			int n = 1;
			
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			// push a new paren list onto the stack
			if (firstNonLwsp != -1)
				n += linebuf.Length - firstNonLwsp;
			
			stack.Push (Inside.ParenList, keyword, curLineNr, n);
			
			keyword = DTokens.INVALID;
		}
		
		void PushCloseParen (Inside inside)
		{
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			
			if (inside != Inside.ParenList) {
				//Console.WriteLine ("can't pop a '(' if we ain't got one?");
				return;
			}
			
			// pop this paren list off the stack
			keyword = stack.PeekKeyword;
			stack.Pop ();
		}
		
		void PushOpenBrace (Inside inside)
		{
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			// push a new block onto the stack
			if (inside == Inside.FoldedStatement) {
				byte pKeyword;
				
				if (firstNonLwsp == -1) {
					pKeyword = stack.PeekKeyword;
					stack.Pop ();
				} else {
					pKeyword = keyword;
				}
				
				while (true) {
					if (stack.PeekInside (0) != Inside.FoldedStatement)
						break;
					var kw = stack.PeekKeyword;
					stack.Pop ();
					TrimIndent ();
					if (kw != DTokens.INVALID) {
						pKeyword = kw;
						break;
					}
				}
				
				if (firstNonLwsp == -1)
					curIndent = stack.PeekIndent (0);
				
				stack.Push (Inside.Block, pKeyword, curLineNr, 0);
			} else if (inside == Inside.Case && (keyword == DTokens.Default || keyword == DTokens.Case)) {
				if (curLineNr == stack.PeekLineNr (0) || firstNonLwsp == -1) {
					// e.g. "case 0: {" or "case 0:\n{"
					stack.Push (Inside.Block, keyword, curLineNr, -1);
					
					if (firstNonLwsp == -1)
						TrimIndent ();
				} else {
					stack.Push (Inside.Block, keyword, curLineNr, 0);
				}
			} else {
				stack.Push (Inside.Block, keyword, curLineNr, 0);
// Destroys one lined expression block 'var s = "".Split (new char[] {' '});'
//				if (inside == Inside.ParenList)
//					TrimIndent ();
			}
			
			keyword = DTokens.INVALID;
			if (firstNonLwsp == -1)
				needsReindent = true;
		}
		
		void PushCloseBrace (Inside inside)
		{
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) != 0)
				return;
			if (inside != Inside.Block && inside != Inside.Case) {
				if (stack.PeekInside (0) == Inside.FoldedStatement) {
					while (stack.PeekInside (0) == Inside.FoldedStatement) {
						stack.Pop ();
					}
					curIndent = stack.PeekIndent (0);
					keyword = stack.PeekKeyword;
					inside = stack.PeekInside (0);
				}
				//Console.WriteLine ("can't pop a '{' if we ain't got one?");
				if (inside != Inside.Block && inside != Inside.Case)
					return;
			}

			if (inside == Inside.Case) {
				curIndent = stack.PeekIndent (1);
				keyword = stack.PeekKeyword;
				inside = stack.PeekInside (1);
				stack.Pop ();
			}
			
			if (inside == Inside.ParenList) {
				curIndent = stack.PeekIndent (0);
				keyword = stack.PeekKeyword;
				inside = stack.PeekInside (0);
			}
			
			// pop this block off the stack
			keyword = stack.PeekKeyword;
			if (keyword != DTokens.Case && keyword != DTokens.Default)
				keyword = DTokens.INVALID;

			stack.Pop ();

			while (stack.PeekInside (0) == Inside.FoldedStatement) {
				stack.Pop ();
			}

			if (firstNonLwsp == -1) {
				needsReindent = true;
				TrimIndent ();
			}
		}
		
		void PushNewLine (Inside inside)
		{
			top:
			switch (inside) {
			case Inside.Shebang:
			case Inside.PreProcessor:
				// pop the preprocesor state unless the eoln is escaped
				if (rc != '\\') {
					keyword = stack.PeekKeyword;
					stack.Pop ();
				}
				break;
			case Inside.BlockComment:
			case Inside.NestedComment:
				// nothing to do
				break;
			case Inside.DocComment:
			case Inside.LineComment:
				// pop the line comment
				keyword = stack.PeekKeyword;
				stack.Pop ();

				inside = stack.PeekInside (0);
				goto top;
			case Inside.VerbatimString:
				// nothing to do
				break;
			case Inside.StringLiteral:
				if (isEscaped) {
					/* I don't think c# allows breaking a
					 * normal string across lines even
					 * when escaping the carriage
					 * return... but how else should we
					 * handle this? */
					break;
				}

								/* not escaped... error!! but what can we do,
				 * eh? allow folding across multiple lines I
				 * guess... */
				break;
			case Inside.CharLiteral:
				/* this is an error... what to do? guess we'll
				 * just pretend it never happened */
				break;
			case Inside.Attribute:
				// nothing to do
				break;
			case Inside.ParenList:
				// nothing to do
				break;
			default:
				// Empty, FoldedStatement, and Block
				switch (rc) {
				case '\0':
					// nothing entered on this line
					break;
				case ':':
					canBeLabel = canBeLabel && inside != Inside.FoldedStatement;
					
					if (keyword == DTokens.Case || keyword == DTokens.Default || !canBeLabel)
						break;

					PushFoldedStatement ();
					break;
				case '[':
					// handled elsewhere
					break;
				case ']':
					// handled elsewhere
					break;
				case '(':
					// handled elsewhere
					break;
				case '{':
					// handled elsewhere
					break;
				case '}':
					// handled elsewhere
					break;
				case ';':
					// handled elsewhere
					break;
				case ',':
					if(keyword == DTokens.Import)
						PushFoldedStatement();
					// avoid indenting if we are in a list
					break;
				default:
					if (stack.PeekLineNr (0) == curLineNr) {
						// is this right? I don't remember why I did this...
						break;
					}

					if (inside == Inside.Block) {
						var peekKw = stack.PeekKeyword;
						if (peekKw == DTokens.Struct || peekKw == DTokens.Enum || peekKw == DTokens.Assign || peekKw == DTokens.Import) {
							// just variable/value declarations
							break;
						}
					}

					PushFoldedStatement ();
					break;
				}

				break;
			}
			
			linebuf.Length = 0;
			
			canBeLabel = true;
			
			beganInside = stack.PeekInside (0);
			curIndent = stack.PeekIndent (0);
						
			firstNonLwsp = -1;
			lastNonLwsp = -1;
			wordStart = -1;
			
			prc = '\0';
			pc = '\0';
			rc = '\0';
			
			curLineNr++;
			cursor++;
		}
		#endregion
		
		static string[] preProcessorIndents = new string[] {
			"line"
		};

		void CheckForParentList ()
		{
			var after = stack.PeekInside (0);
			if ((after & Inside.ParenList) == Inside.ParenList && pc == '(') {
//				var indent = stack.PeekIndent (0);
				var kw = stack.PeekKeyword;
				var line = stack.PeekLineNr (0);
				stack.Pop ();
				stack.Push (after, kw, line, 0);
			}
		}
		
		/// <summary>
		/// The engine's main logic
		/// </summary>
		public void Push (char c)
		{
			Inside inside, after;
			
			inside = stack.PeekInside (0);
			
			// Skip the first optional shebang line
			if(inside == Inside.Shebang && c != '\n' && c != '\r')
				return;
			
			// pop the verbatim-string-literal
			if (inside == Inside.VerbatimString && popVerbatim && c != '"') {
				keyword = stack.PeekKeyword;
				popVerbatim = false;
				stack.Pop ();
				
				inside = stack.PeekInside (0);
			}
			
			needsReindent = false;
			
			if ((inside & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) == 0 && wordStart != -1) {
				if (char.IsWhiteSpace (c) || c == '(' || c == '{') {
					var tmp = WordIsKeyword ();
					if (tmp != DTokens.INVALID)
						keyword = tmp;
				} else if (c == ':' && WordIsDefault) {
					keyword = DTokens.Default;
				}	
			//get the keyword for preprocessor directives
			} /*else if ((inside & (Inside.PreProcessor)) != 0 && stack.PeekKeyword == null) {
				//replace the stack item with a keyworded one
				var preProcessorKeyword = GetDirectiveKeyword (c);
				int peekLine = stack.PeekLineNr (0);
				stack.Pop ();
				stack.Push (Inside.PreProcessor, preProcessorKeyword, peekLine, 0);
				//regions need to pop back out
				if (preProcessorKeyword == "region" || preProcessorKeyword == "endregion") {
					curIndent = stack.PeekIndent (0);
					needsReindent = true;
					
				}
			}*/
			
			//Console.WriteLine ("Pushing '{0}'/#{3}; wordStart = {1}; keyword = {2}", c, wordStart, keyword, (int)c);
			
			switch (c) {
			case '#':
				PushHash (inside);
				lastChar = '#';
				break;
			case '/':
				PushSlash (inside);
				break;
			case '\\':
				PushBackSlash (inside);
				break;
			case '+':
			case '*':
				PushStar (inside,c);
				break;
			case '"':
				PushQuote (inside);
				break;
			case '\'':
				PushSQuote (inside);
				break;
			case ':':
				PushColon (inside);
				break;
			case ';':
				PushSemicolon (inside);
				break;
			case '[':
				PushOpenSq (inside);
				break;
			case ']':
				PushCloseSq (inside);
				break;
			case '(':
				PushOpenParen (inside);
				break;
			case ')':
				PushCloseParen (inside);
				break;
			case '{':
				PushOpenBrace (inside);
				break;
			case '}':
				PushCloseBrace (inside);
				break;
			case '\r':
				CheckForParentList ();
				PushNewLine (inside);
				lastChar = c;
				return;
			case '\n':
				CheckForParentList ();
				
				if (lastChar == '\r') {
					cursor++;
				} else {
					PushNewLine (inside);
				}
				lastChar = c;
				return;
			default:
				break;
			}
			after = stack.PeekInside (0);
			
			if ((after & Inside.PreProcessor) == Inside.PreProcessor) {
				for (int i = 0; i < preProcessorIndents.Length; i++) {
					int len = preProcessorIndents[i].Length - 1;
					if (linebuf.Length < len)
						continue;
					
					string str = linebuf.ToString (linebuf.Length - len, len) + c;
					if (str == preProcessorIndents[i]) {
						needsReindent = true;
						break;
					}
				}
			}
			
			if ((after & (Inside.PreProcessor | Inside.StringOrChar | Inside.Comment)) == 0) {
				if (!Char.IsWhiteSpace (c)) {
					if (firstNonLwsp == -1)
						firstNonLwsp = linebuf.Length;
					
					if (wordStart != -1 && c != ':' && Char.IsWhiteSpace (pc)) {
						// goto labels must be single word tokens
						canBeLabel = false;
					} else if (wordStart == -1 && Char.IsDigit (c)) {
						// labels cannot start with a digit
						canBeLabel = false;
					}
					
					lastNonLwsp = linebuf.Length;
					
					if (c != ':') {
						if (Char.IsWhiteSpace (pc) || rc == ':')
							wordStart = linebuf.Length;
						else if (pc == '\0')
							wordStart = 0;
					}
				}
			} else if (c != '\\' && (after & (Inside.StringLiteral | Inside.CharLiteral)) != 0) {
				// Note: PushBackSlash() will handle untoggling isEscaped if c == '\\'
				isEscaped = false;
			}
			
			pc = c;
			prc = rc;
			// Note: even though PreProcessor directive chars are insignificant, we do need to
			//       check for rc != '\\' at the end of a line.
			if ((inside & Inside.Comment) == 0 &&
			    (after & Inside.Comment) == 0 &&
			    !Char.IsWhiteSpace (c))
				rc = c;
			
			linebuf.Append (c);
			
			cursor++;
			lastChar = c;
		}
	}
}
