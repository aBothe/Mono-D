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
				t==DTokens.Switch ||
				//t == DTokens.Version ||	t == DTokens.Debug || // Don't regard them because they also can occur as an attribute
				t == DTokens.If ||
				t == DTokens.While ||
				t == DTokens.For ||
				t == DTokens.Foreach ||
				t == DTokens.Foreach_Reverse ||
				t == DTokens.With ||
				t == DTokens.Synchronized;
		}

		public static CodeBlock CalculateIndentation(string code, int offset)
		{
			if (offset >= code.Length)
				offset = code.Length - 1;

			CodeBlock block = null;

			var parserEndLocation = DocumentHelper.OffsetToLocation(code, offset);

			var lexer = new Lexer(new StringReader(code));
			
			lexer.NextToken();

			DToken t = null;
			DToken la = null;
			bool IsEOFInNextIteration= false;

			while (!lexer.IsEOF)
			{
				lexer.NextToken();

				t = lexer.CurrentToken;
				la = lexer.LookAhead;

				IsEOFInNextIteration = la.Location>=parserEndLocation;
				// Ensure one token after the caret offset becomes parsed
				if (t!=null && t.Location >= parserEndLocation)
					break;

				if (t != null && (
					t.Kind == DTokens.Case || 
					t.Kind == DTokens.Default))
				{
					if (block != null && block.IsNonClampBlock)
						block = block.Parent;

					// 'case myEnum.A:'
					if (la.Kind != DTokens.Colon)
					{
						// To prevent further issues, skip the expression
						var psr = new DParser(lexer);
						psr.AssignExpression();
						// FIXME: What if cursor is somewhere between case and ':'??
					}

					if(lexer.LookAhead.EndLocation >= parserEndLocation)
					{
						break;
					}
					else if(lexer.CurrentPeekToken.Kind!=DTokens.OpenCurlyBrace)
						block = new CodeBlock
						{
							InitialToken = DTokens.Case,
							StartLocation = t.EndLocation,

							Parent = block
						};

					continue;
				}

				/*
				 * if(..)
				 *		if(...)
				 *			if(...)
				 *				foo();
				 *	// No indentation anymore!
				 */
				else if(t!=null && 
					t.Kind == DTokens.Semicolon && 
					block!=null && 
					block.LastPreBlockIdentifier.Kind!=DTokens.For)
				{ 
					while(block != null && (block.IsSingleSubStatementIndentation || block.IsStatementIndentation))
						block = block.Parent;
				}

				// New block is opened by (,[,{
				if (!IsEOFInNextIteration &&
					( la.Kind == DTokens.OpenParenthesis || 
					la.Kind == DTokens.OpenSquareBracket || 
					la.Kind == DTokens.OpenCurlyBrace))
				{
					while (block != null && block.IsStatementIndentation)
						block = block.Parent;

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
					if (IsEOFInNextIteration && t.line==la.line)
					{
						return block;
					}

					// Statements that contain only one sub-statement -> indent by 1
					if (((block.InitialToken == DTokens.OpenParenthesis && la.Kind == DTokens.CloseParenthesis
						&& IsPreStatementToken(block.LastPreBlockIdentifier.Kind)) || 
						la.Kind==DTokens.Do) // 'Do'-Statements allow single statements inside

						&& lexer.Peek().Kind != DTokens.OpenCurlyBrace /* Ensure that no block statement follows */)
					{
						block = new CodeBlock
						{
							LastPreBlockIdentifier = t,
							IsSingleSubStatementIndentation = true,
							StartLocation = t.EndLocation,

							Parent = block.Parent
						};
					}

					else if (!IsEOFInNextIteration ||
						(la.Kind == DTokens.CloseCurlyBrace &&
						la.line == parserEndLocation.Line))
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
								block = block.Parent;

						if (block != null)
							block = block.Parent;
					}
				}

				else if (!IsEOFInNextIteration &&
					(t.Kind!=DTokens.Semicolon && t.Kind!=DTokens.OpenCurlyBrace) && 
					(block == null || (
					!block.IsStatementIndentation &&
					!block.IsSingleSubStatementIndentation &&
					(block.IsNonClampBlock || block.InitialToken==DTokens.OpenCurlyBrace)
					)))
				{
					block = new CodeBlock
					{
						IsStatementIndentation = true,
						LastPreBlockIdentifier = t,
						StartLocation = t.EndLocation,

						Parent = block
					};
				}
			}

			return block;
		}

		

		public static int ReadRawLineIndentation(string lineText)
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

		public bool IsSingleSubStatementIndentation = false;
		public bool IsStatementIndentation = false;

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

		public int GetLineIndentation(int line)
		{
			if (StartLocation.Line > line)
				return 0;

			int indentation = 0;

			if (Parent != null)
				indentation = Parent.GetLineIndentation(line);

			if (line > StartLocation.Line)
				indentation++;

			return indentation;
		}
	}

}
