using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;
using System.IO;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;

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

		DToken t { get { return Lexer.CurrentToken; } }
		DToken la { get { return Lexer.LookAhead; } }
		CodeBlock block;

		CodeBlock lastLineIndent;
		bool HadCaseStatementBegin;

		bool IsSemicolonContainingStatement
		{
			get {
				return block.LastPreBlockIdentifier.Kind == DTokens.For ||
						block.LastPreBlockIdentifier.Kind == DTokens.Foreach ||
						block.LastPreBlockIdentifier.Kind == DTokens.Foreach_Reverse;
			}
		}

		int maxLine;
		Lexer Lexer;

		CodeBlock PushBlock(CodeBlock previousBlock=null)
		{
			return block = new CodeBlock { 
				previousBlock=previousBlock,
				LastPreBlockIdentifier=Lexer.LastToken ?? null,
				Parent=block,
				StartLocation = t.Location
			};
		}

		CodeBlock PopBlock()
		{
			if (block != null)
				return block = block.Parent;
			return null;
		}

		public CodeBlock CalculateIndentation(string code, int line)
		{
			var sr=new StringReader(code);
			var cb= CalculateIndentation(sr, line);

			sr.Close();

			return cb;
		}

		public bool IsEOF
		{
			get {
				return Lexer.IsEOF || (t != null && t.line > maxLine);
			}
		}

		public CodeBlock CalculateIndentation(TextReader code, int line)
		{
			block = null;

			Lexer = new Lexer(code);
			maxLine = line;

			Lexer.NextToken();
			DToken lastToken = null;

			while (!Lexer.IsEOF)
			{
				if (t != null && la.line > t.line && t.line < maxLine)
				{
					RemoveNextLineUnindentBlocks();
				}

				lastToken = t;
				Lexer.NextToken();

				if (IsEOF)
				{
					if (la.line > maxLine || Lexer.IsEOF)
						lastLineIndent = null;

					if (t.line>maxLine)
						break;
				}

				/*
				 * if(..)
				 *		for(...)
				 *			while(...)
				 *				foo();
				 *	// No indentation anymore!
				 */
				if (t.Kind == DTokens.Comma || t.Kind == DTokens.Semicolon && maxLine>t.line && la.line > t.line)
				{
					if (block == null)
						continue;

					if (block.Reason == CodeBlock.IndentReason.UnfinishedStatement)
						PopBlock();

					while (
						block != null &&
						block.Reason == CodeBlock.IndentReason.SingleLineStatement &&
						!IsSemicolonContainingStatement)
						PopBlock();
				}

				// (,[,{
				else if (t.Kind == DTokens.OpenParenthesis ||
					t.Kind == DTokens.OpenSquareBracket ||
					t.Kind == DTokens.OpenCurlyBrace)
				{
					var tBlock = block;

					if (block != null && (
						block.Reason == CodeBlock.IndentReason.SingleLineStatement ||
						block.Reason == CodeBlock.IndentReason.UnfinishedStatement))
					{
						PopBlock();
					}

					PushBlock(tBlock).BlockStartToken = t.Kind;
				}

				// ),],}
				else if (t.Kind == DTokens.CloseParenthesis ||
					t.Kind == DTokens.CloseSquareBracket ||
					t.Kind == DTokens.CloseCurlyBrace)
				{
					if (t.Kind == DTokens.CloseCurlyBrace)
					{
						while (block != null && !block.IsClampBlock)
							PopBlock();

						/*
						 * If the last token was on this line OR if it's eof but on the following line, 
						 * decrement indent on next line only.
						 */
						if (lastToken!=null && lastToken.line == t.line && block != null)
						{
							block.PopOnNextLine = true;
						}
						else
							PopBlock();
					}
					else
					{
						while (block != null && !block.IsClampBlock)
							PopBlock();

						if (lastLineIndent == null && (block == null || block.StartLocation.Line < t.line))
							lastLineIndent = block;

						if (t.Kind == DTokens.CloseParenthesis &&
							block != null &&
							block.BlockStartToken == DTokens.OpenParenthesis && 
							la.Kind!=DTokens.OpenCurlyBrace)
						{
							block=block.previousBlock;

							continue;
						}
						else
							PopBlock();

						if (t.Kind == DTokens.CloseParenthesis &&
							block != null &&
							block.BlockStartToken == DTokens.OpenParenthesis)
						{
							if (la.Kind == DTokens.OpenCurlyBrace && la.line > t.line)
								PopBlock();
							else if (block!=null && block.LastPreBlockIdentifier!=null && IsPreStatementToken(block.LastPreBlockIdentifier.Kind))
								block = block.previousBlock;
						}
					}
				}

				else if ((DParser.IsAttributeSpecifier(t.Kind, la.Kind) && la.Kind==DTokens.Colon) || t.Kind == DTokens.Case || t.Kind==DTokens.Default)
				{
					while (block != null && block.BlockStartToken!=DTokens.OpenCurlyBrace)
						PopBlock();

					PushBlock().Reason = CodeBlock.IndentReason.StatementLabel;

					HadCaseStatementBegin = true;
				}
				else if (t.Kind == DTokens.Colon)
				{
					if (HadCaseStatementBegin)
					{
						while (block != null && block.Reason != CodeBlock.IndentReason.StatementLabel)
							PopBlock();
						HadCaseStatementBegin = false;
					}
				}

				// Don't indent these in front of function bodies
				else if (t.Kind == DTokens.In || t.Kind == DTokens.Out || t.Kind == DTokens.Body)
				{
					if (block != null && block.Reason == CodeBlock.IndentReason.UnfinishedStatement)
						PopBlock();
				}


				else if (block == null ||
					block.Reason != CodeBlock.IndentReason.UnfinishedStatement &&
					block.Reason != CodeBlock.IndentReason.SingleLineStatement)
					PushBlock().Reason = CodeBlock.IndentReason.UnfinishedStatement;
			}

			if (t!=null && la.line > t.line)
				RemoveNextLineUnindentBlocks();

			return lastLineIndent ?? block;
		}

		void RemoveNextLineUnindentBlocks()
		{
			while (block != null && block.PopOnNextLine)
				block = block.Parent;

			var curBlock = block;

			while (curBlock != null)
			{
				if (curBlock.Parent != null && curBlock.Parent.PopOnNextLine)
					curBlock.Parent = curBlock.Parent.Parent;

				curBlock = curBlock.Parent;
			}
		}

		public static int CalculateIndentation() { return 0; }

		public static int CalculateRelativeIndentation(INode Scope, CodeLocation Caret)
		{
			return 0;
		}

		public static int CalculateRelativeIndentation(IStatement Statement, CodeLocation Caret)
		{
			return 0;
		}

		public static int CalculateRelativeIndentation(IExpression Expression, CodeLocation Caret)
		{
			return 0;
		}
	}

	public class CodeBlock
	{
		public enum IndentReason
		{
			Other,
			SingleLineStatement,
			UnfinishedStatement,
			StatementLabel, // x1: ; case 3: ; default: 
		}

		public IndentReason Reason= IndentReason.Other;

		public CodeBlock previousBlock;

		public CodeLocation StartLocation;
		//public CodeLocation EndLocation;

		public bool PopOnNextLine;

		public int BlockStartToken;

		public bool IsClampBlock { get {
			return BlockStartToken == DTokens.OpenCurlyBrace || BlockStartToken == DTokens.OpenParenthesis || BlockStartToken == DTokens.OpenSquareBracket;
		} }

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
