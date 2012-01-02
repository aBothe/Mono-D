using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using D_Parser.Parser;
using System.IO;

namespace D_Parser.Resolver
{
	public enum TokenContext
	{
		None = 0,
		String = 2 << 0,
		VerbatimString = 2 << 1,
		LineComment = 2 << 5,
		BlockComment = 2 << 2,
		NestedComment = 2 << 3,
		CharLiteral = 2 << 4
	}

	public class CaretContextAnalyzer
	{
		static IList<string> preParenthesisBreakTokens = new List<string> { 
			"if", 
			"while", 
			"for", 
			"foreach", 
			"foreach_reverse", 
			"with", 
			"try", 
			"catch", 
			"finally", 
			"synchronized", 
			"pragma" };

		public static int SearchExpressionStart(string Text, int CaretOffset, int MinimumSearchOffset = 0)
		{
			if (CaretOffset > Text.Length)
				throw new ArgumentOutOfRangeException("CaretOffset", "Caret offset must be smaller than text length");
			else if (CaretOffset == Text.Length)
				Text += ' ';

			// At first we only want to find the beginning of our identifier list
			// later we will pass the text beyond the beginning to the parser - there we parse all needed expressions from it
			int IdentListStart = -1;

			/*
			T!(...)>.<
			 */

			int isComment = 0;
			bool isString = false, expectDot = false, hadDot = true;
			bool hadString = false;
			var bracketStack = new Stack<char>();

			var identBuffer = "";
			bool hadBraceOpener = false;
			int lastBraceOpenerOffset = 0;

			bool stopSeeking = false;

			// Step backward
			for (int i = CaretOffset; i >= MinimumSearchOffset && !stopSeeking; i--)
			{
				IdentListStart = i;
				var c = Text[i];
				var str = Text.Substring(i);
				char p = ' ';
				if (i > 0) p = Text[i - 1];

				// Primitive comment check
				if (!isString && c == '/' && (p == '*' || p == '+'))
					isComment++;
				if (!isString && isComment > 0 && (c == '+' || c == '*') && p == '/')
					isComment--;

				// Primitive string check
				//TODO: "blah">.<
				hadString = false;
				if (isComment < 1 && c == '"' && p != '\\')
				{
					isString = !isString;

					if (!isString)
						hadString = true;
				}

				// If string or comment, just continue
				if (isString || isComment > 0)
					continue;

				// If between brackets, skip
				if (bracketStack.Count > 0 && c != bracketStack.Peek())
					continue;

				// Bracket check
				if (hadDot)
					switch (c)
					{
						case ']':
							bracketStack.Push('[');
							continue;
						case ')':
							bracketStack.Push('(');
							continue;
						case '}':
							if (bracketStack.Count < 1)
							{
								IdentListStart++;
								stopSeeking = true;
								continue;
							}
							bracketStack.Push('{');
							continue;

						case '[':
						case '(':
						case '{':
							if (bracketStack.Count > 0 && bracketStack.Peek() == c)
							{
								bracketStack.Pop();
								if (c == '(' && p == '!') // Skip template stuff
									i--;
							}
							else if (c == '{')
							{
								stopSeeking = true;
								IdentListStart++;
							}
							else
							{
								if (c == '(' && p == '!') // Skip template stuff
									i--;

								lastBraceOpenerOffset = IdentListStart;
								// e.g. foo>(< bar| )
								hadBraceOpener = true;
								identBuffer = "";
							}
							continue;
					}

				// whitespace check
				if (Char.IsWhiteSpace(c)) { if (hadDot) expectDot = false; else expectDot = true; continue; }

				if (c == '.')
				{
					hadBraceOpener = false;
					identBuffer = "";
					expectDot = false;
					hadDot = true;
					continue;
				}

				/*
				 * abc
				 * abc . abc
				 * T!().abc[]
				 * def abc.T
				 */
				if (Char.IsLetterOrDigit(c) || c == '_')
				{
					hadDot = false;

					if (!expectDot)
					{
						identBuffer += c;

						if (!hadBraceOpener)
							continue;
						else if (!preParenthesisBreakTokens.Contains(identBuffer))
							continue;
						else
							IdentListStart = lastBraceOpenerOffset;
					}
				}

				// Only re-increase our caret offset if we did not break because of a string..
				// otherwise, we'd return the offset after the initial string quote
				if (!hadString)
					IdentListStart++;
				stopSeeking = true;
			}

			return IdentListStart;
		}



		public static TokenContext GetTokenContext(string Text, int Offset)
		{
			int _u;
			return GetTokenContext(Text, Offset, out _u, out _u);
		}

		public static TokenContext GetTokenContext(string Text, int Offset, out int lastBeginOffset, out int lastEndOffset)
		{
			char cur = '\0', peekChar = '\0';
			int off = 0;
			bool IsInString = false;
			bool IsInLineComment = false;
			bool IsInBlockComment = false;
			bool IsInNestedBlockComment = false;
			bool IsChar = false;
			bool IsVerbatimString = false;
			bool IsAlternateVerbatimString = false;

			lastBeginOffset = -1;
			lastEndOffset = -1;

			/*
			 * Continue searching if
			 *	1) Caret offset hasn't been reached yet
			 *	2) An end of a context block is still expected
			 */
			bool isBeyondCaret = false; // Only reset bool states if NOT beyond target offset
			while (off < Offset - 1 ||
				(isBeyondCaret = (lastBeginOffset != -1 && lastEndOffset == -1 && off < Text.Length)))
			{
				cur = Text[off];
				if (off < Text.Length - 1)
					peekChar = Text[off + 1];

				// String check
				if (!IsInLineComment && !IsInBlockComment && !IsInNestedBlockComment)
				{
					if (!IsInString)
					{
						// Char handling
						if (!IsChar && cur == '\'')
						{
							lastBeginOffset = off;
							lastEndOffset = -1;
							IsChar = true;
						}
						else
						{
							// Single quote char escape
							if (cur == '\\' && peekChar == '\'')
							{
								off += 2;
								continue;
							}
							else if (cur == '\'')
							{
								IsChar = isBeyondCaret;
								lastEndOffset = off;
							}
						}

						// Verbatim string recognition
						if (cur == 'r' && peekChar == '\"')
						{
							lastBeginOffset = off;
							lastEndOffset = -1;
							off++;
							IsInString = IsVerbatimString = true;
						}
						else if (cur == '`')
						{
							lastBeginOffset = off;
							lastEndOffset = -1;
							IsInString = IsAlternateVerbatimString = true;
						}
						// if not, test for normal string literals
						else if (cur == '\"')
						{
							IsInString = true;
						}
					}
					else
					{
						// Verbatim double quote char escape
						if ((IsVerbatimString && cur == '\"' && peekChar == '\"') ||
							// Normal backslash escape
							(cur == '\\' && peekChar == '\\'))
						{
							off += 2;
							continue;
						}
						else if (IsAlternateVerbatimString && cur == '`')
						{
							IsInString = IsAlternateVerbatimString = isBeyondCaret;
							lastEndOffset = off;
						}
						else if (cur == '\"')
						{
							IsInString = IsVerbatimString = isBeyondCaret;
							lastEndOffset = off;
						}
					}
				}

				if (!IsInString && !IsChar)
				{
					// Line comment check
					if (!IsInBlockComment && !IsInNestedBlockComment)
					{
						if (cur == '/' && peekChar == '/')
						{
							IsInLineComment = true;
							lastBeginOffset = off;
							lastEndOffset = -1;
						}
						else if (IsInLineComment && cur == '\n')
						{
							IsInLineComment = isBeyondCaret;
							lastEndOffset = off;
						}
					}

					// Block comment check
					if (cur == '/' && peekChar == '*')
					{
						IsInBlockComment = true;
						lastBeginOffset = off;
						lastEndOffset = -1;
					}
					else if (IsInBlockComment && cur == '*' && peekChar == '/')
					{
						IsInBlockComment = isBeyondCaret;
						off++;
						lastEndOffset = off + 1;
					}

					// Nested comment check
					if (!IsInString && cur == '/' && peekChar == '+')
					{
						IsInNestedBlockComment = true;
						lastBeginOffset = off;
						lastEndOffset = -1;
					}
					else if (IsInNestedBlockComment && cur == '+' && peekChar == '/')
					{
						IsInNestedBlockComment = isBeyondCaret;
						off++;
						lastEndOffset = off + 1;
					}
				}

				off++;
			}

			var ret = TokenContext.None;

			if (IsChar)
				ret |= TokenContext.CharLiteral;
			if (IsInLineComment)
				ret |= TokenContext.LineComment;
			if (IsInBlockComment)
				ret |= TokenContext.BlockComment;
			else if (IsInNestedBlockComment)
				ret |= TokenContext.NestedComment;
			if (IsInString)
				ret |= TokenContext.String;
			if (IsVerbatimString || IsAlternateVerbatimString)
				ret |= TokenContext.VerbatimString;

			return ret;
		}

		public static bool IsInCommentAreaOrString(string Text, int Offset)
		{
			int _u;
			return GetTokenContext(Text, Offset, out _u, out _u) != TokenContext.None;
		}
	}
}
