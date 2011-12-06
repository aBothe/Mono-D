using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;
using System.IO;
using D_Parser.Resolver;

namespace D_Parser.Formatting
{
	public class DFormatter
	{
		public static bool IsPreStatementToken(int t)
		{
			return
				//t == DTokens.Version ||	t == DTokens.Debug || // Don't regard them because they also can occur as an attribute
				t == DTokens.If ||
				t == DTokens.While ||
				t == DTokens.For ||
				t == DTokens.Foreach ||
				t == DTokens.Foreach_Reverse ||
				t == DTokens.Synchronized;
		}

		public static CodeBlock CalculateIndentation(string code, int offset)
		{
			if (offset >= code.Length)
				offset = code.Length - 1;

			CodeBlock block = null;

			var parserEndLocation = DocumentHelper.OffsetToLocation(code, offset);

			var lex = new Lexer(new StringReader(code));
			
			lex.NextToken();

			DToken t = null;
			DToken la = null;
			bool isTheoreticEOF = false;

			while (!lex.IsEOF)
			{
				lex.NextToken();

				t = lex.CurrentToken;
				la = lex.LookAhead;

				if (la.line == 4) { }

				isTheoreticEOF = la.Location>=parserEndLocation;
				// Ensure one token after the caret offset becomes parsed
				if (t!=null && t.Location >= parserEndLocation)
					break;

				// Handle case: or default: occurences
				if (t != null && (t.Kind == DTokens.Case || t.Kind == DTokens.Default))
				{
					if (block != null && block.IsNonClampBlock)
						block = block.Parent;

					// On e.g. case myEnum.A:
					if (la.Kind != DTokens.Colon)
					{
						// To prevent further issues, skip the expression
						var psr = new DParser(lex);
						psr.AssignExpression();
						// FIXME: What if cursor is somewhere between case and ':'??
					}

					// lex.LookAhead should be ':' now
					if(lex.LookAhead.EndLocation >= parserEndLocation)
					{
						break;
					}
					else block = new CodeBlock
					{
						InitialToken = DTokens.Case,
						StartLocation = t.EndLocation,

						Parent = block
					};
				}

				// If in a single-statement scope, unindent by 1 if semicolon found
				/*
				 * Note: On multiple single-sub-statemented statements: for instance
				 * if(..)
				 *		if(...)
				 *			if(...)
				 *				foo();
				 *	// No indentation anymore!
				 */
				else if (block != null && (block.IsSingleLineIndentation) && la.Kind == DTokens.Semicolon)
					block = block.Parent;

				// New block is opened by (,[,{
				else if (
					!isTheoreticEOF &&
					( la.Kind == DTokens.OpenParenthesis || 
					la.Kind == DTokens.OpenSquareBracket || 
					la.Kind == DTokens.OpenCurlyBrace))
				{
					block = new CodeBlock
					{
						LastPreBlockIdentifier = t,
						InitialToken = la.Kind,
						StartLocation = la.Location,

						Parent = block
					};
				}

				// Open block is closed by ),],}
				else if (block != null && (
					la.Kind == DTokens.CloseParenthesis ||
					la.Kind == DTokens.CloseSquareBracket ||
					la.Kind == DTokens.CloseCurlyBrace))
				{
					// If EOF reached, only 'decrement' indentation if code line consists of the closing bracket only
					// --> Return immediately if there's been another token on the same line
					if (isTheoreticEOF && t.line==la.line)
					{
						return block;
					}

					// Statements that contain only one sub-statement -> indent by 1
					if (((block.InitialToken == DTokens.OpenParenthesis && la.Kind == DTokens.CloseParenthesis
						&& IsPreStatementToken(block.LastPreBlockIdentifier.Kind)) || 
						la.Kind==DTokens.Do) // 'Do'-Statements allow single statements inside

						&& lex.Peek().Kind != DTokens.OpenCurlyBrace /* Ensure that no block statement follows */)
					{
						block = new CodeBlock
						{
							LastPreBlockIdentifier = t,
							IsSingleLineIndentation = true,
							StartLocation = t.Location,

							Parent = block.Parent
						};
					}

					/* 
					 * Do unindent if the watched code is NOT about to end OR if
					 * the next token is a '}' (which normally means the end of a class body/block statement etc.)
					 * AND if no line-break was entered (so unindent the finalizing '}' but not the block's statements)
					 */
					else if(!isTheoreticEOF || 
						(la.Kind==DTokens.CloseCurlyBrace && 
						la.line==parserEndLocation.Line))
					{
						/*
						 * On "case:" or "default:" blocks (so mostly in switch blocks),
						 * skip back to the 'switch' scope.
						 * 
						 * switch(..)
						 * {
						 *		default:
						 *		case ..:
						 *			// There is indentation now!
						 *			// (On following lines, case blocks are still active)
						 * } // Decreased Indentation; case blocks discarded
						 */
						if (la.Kind == DTokens.CloseCurlyBrace)
							while (block != null && block.IsNonClampBlock)
								block=block.Parent;

						if(block!=null)
							block = block.Parent;
					}
				}
			}

			return block;
		}

		/// <summary>
		/// Counts the initial white-spaces in lineText
		/// </summary>
		public static int GetLineTabIndentation(string lineText)
		{
			int ret = 0;

			foreach (var c in lineText)
			{
				if (c == ' ' || c == '\t')
					ret++;
				else
					break;
			}

			return ret;
		}
	}

	public class CodeBlock
	{
		public int InitialToken;
		public CodeLocation StartLocation;
		//public CodeLocation EndLocation;

		public bool IsSingleLineIndentation = false;
		//public bool IsWaitingForSemiColon = false;

		public bool IsNonClampBlock
		{
			get { return InitialToken != DTokens.OpenParenthesis && InitialToken != DTokens.OpenSquareBracket && InitialToken != DTokens.OpenCurlyBrace; }
		}

		/// <summary>
		/// The last token before this CodeBlock started.
		/// </summary>
		public DToken LastPreBlockIdentifier;

		/// <summary>
		/// Last token found within current block.
		/// </summary>
		public DToken LastToken;

		public CodeBlock Parent;

		public int OuterIndentation
		{
			get
			{
				if (Parent != null)
					return Parent.OuterIndentation + 1;

				return 0;
			}
		}

		public int InnerIndentation
		{
			get
			{
				return OuterIndentation + 1;
			}
		}
	}

}
