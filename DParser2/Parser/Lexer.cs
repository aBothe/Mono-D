using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Parser
{
	/// <summary>
	/// Taken from SharpDevelop.NRefactory
	/// </summary>
	public abstract class AbstractLexer
	{
		TextReader reader;
		int col = 1;
		int line = 1;

		protected DToken prevToken = null;
		protected DToken curToken = null;
		protected DToken lookaheadToken = null;
		protected DToken peekToken = null;

		public readonly TrackerContainer TokenTracker;
		protected StringBuilder sb = new StringBuilder();

		/// <summary>
		/// used for the original value of strings (with escape sequences).
		/// </summary>
		protected StringBuilder originalValue = new StringBuilder();

		protected int Line
		{
			get
			{
				return line;
			}
		}
		protected int Col
		{
			get
			{
				return col;
			}
		}

		protected int ReaderRead()
		{
			int val = reader.Read();
			if ((val == '\r' && reader.Peek() != '\n') || val == '\n')
			{
				++line;
				col = 1;
				LineBreak();
			}
			else if (val >= 0)
			{
				col++;
			}
			return val;
		}
		protected int ReaderPeek()
		{
			return reader.Peek();
		}

		public void SetInitialLocation(CodeLocation location)
		{
			if (curToken != null || lookaheadToken != null || peekToken != null)
				throw new InvalidOperationException();
			this.line = location.Line;
			this.col = location.Column;
		}

		public DToken LastToken
		{
			get { return prevToken; }
		}

		/// <summary>
		/// Get the current DToken.
		/// </summary>
		public DToken CurrentToken
		{
			get
			{
				return curToken;
			}
			set
			{
				if (value == null) return;
				curToken = value;
				lookaheadToken = curToken.next;
				if (lookaheadToken != null)
					peekToken = lookaheadToken.next;
			}
		}

		/// <summary>
		/// The next DToken (The <see cref="CurrentToken"/> after <see cref="NextToken"/> call) .
		/// </summary>
		public DToken LookAhead
		{
			get
			{
				return lookaheadToken;
			}
			set
			{
				if (value == null) return;
				lookaheadToken = value;
				peekToken = lookaheadToken.next;
			}
		}

		public DToken CurrentPeekToken
		{
			get { return peekToken; }
		}

		/// <summary>
		/// Constructor for the abstract lexer class.
		/// </summary>
		protected AbstractLexer(TextReader reader)
		{
			this.reader = reader;
			TokenTracker = new TrackerContainer(this);
		}

		#region System.IDisposable interface implementation
		public virtual void Dispose()
		{
			reader.Close();
			reader = null;
			curToken = lookaheadToken = peekToken = null;
			sb = originalValue = null;
		}
		#endregion

		/// <summary>
		/// Must be called before a peek operation.
		/// </summary>
		public void StartPeek()
		{
			peekToken = lookaheadToken;
		}

		/// <summary>
		/// Gives back the next token. A second call to Peek() gives the next token after the last call for Peek() and so on.
		/// </summary>
		/// <returns>An <see cref="CurrentToken"/> object.</returns>
		public DToken Peek()
		{
			if (peekToken == null) StartPeek();
			if (peekToken.next == null)
				peekToken.next = Next();
			peekToken = peekToken.next;
			return peekToken;
		}

		/// <summary>
		/// Reads the next token and gives it back.
		/// </summary>
		/// <returns>An <see cref="CurrentToken"/> object.</returns>
		public virtual DToken NextToken()
		{
			if (lookaheadToken == null)
			{
				lookaheadToken = Next();
				TokenTracker.InformToken(lookaheadToken.Kind);
				return lookaheadToken;
			}

			prevToken = curToken;

			curToken = lookaheadToken;

			if (lookaheadToken.next == null)
			{
				lookaheadToken.next = Next();
				if (lookaheadToken.next != null)
					TokenTracker.InformToken(lookaheadToken.next.Kind);
			}

			lookaheadToken = lookaheadToken.next;
			StartPeek();

			return lookaheadToken;
		}

		protected abstract DToken Next();

		protected static bool IsIdentifierPart(int ch)
		{
			if (ch == 95) return true;  // 95 = '_'
			if (ch == -1) return false;
			return char.IsLetterOrDigit((char)ch); // accept unicode letters
		}

		public static bool IsOct(char digit)
		{
			return Char.IsDigit(digit) && digit != '9' && digit != '8';
		}

		public static bool IsHex(char digit)
		{
			return Char.IsDigit(digit) || ('A' <= digit && digit <= 'F') || ('a' <= digit && digit <= 'f');
		}

		public static bool IsBin(char digit)
		{
			return digit == '0' || digit == '1';
		}

		/// <summary>
		/// Tests if digit <para>d</para> is allowed in the specified numerical base.
		/// If <para>NumBase</para> is 10, only digits from 0 to 9 would be allowed.
		/// If NumBase=2, 0 and 1 are legal.
		/// If NumBase=8, 0 to 7 are legal.
		/// If NumBase=16, 0 to 9 and a to f are allowed.
		/// Note: Underscores ('_') are legal everytime!
		/// </summary>
		/// <param name="d"></param>
		/// <param name="NumBase"></param>
		/// <returns></returns>
		public static bool IsLegalDigit(char d, int NumBase)
		{
			return (NumBase == 10 && Char.IsDigit(d)) || (NumBase == 2 && IsBin(d)) /* (NumBase == 8 && IsOct(d)) || */|| (NumBase == 16 && IsHex(d)) || d == '_';
		}

		public static int GetHexNumber(char digit)
		{
			if (Char.IsDigit(digit))
			{
				return digit - '0';
			}
			if ('A' <= digit && digit <= 'F')
			{
				return digit - 'A' + 0xA;
			}
			if ('a' <= digit && digit <= 'f')
			{
				return digit - 'a' + 0xA;
			}
			//errors.Error(line, col, String.Format("Invalid hex number '" + digit + "'"));
			return 0;
		}

		public static double ParseFloatValue(string digit, int NumBase)
		{
			double ret = 0;

			int commaPos = digit.IndexOf('.');
			int k = digit.Length - 1;
			if (commaPos >= 0)
				k = commaPos - 1;

			for (int i = 0; i < digit.Length; i++)
			{
				if (i == commaPos) { i++; k++; }

				// Check if digit string contains some digits after the comma
				if (i >= digit.Length) break;

				int n = GetHexNumber(digit[i]);
				ret += n * Math.Pow(NumBase, k - i);
			}

			return ret;
		}

		protected CodeLocation lastLineEnd = new CodeLocation(1, 1);
		protected CodeLocation curLineEnd = new CodeLocation(1, 1);
		protected void LineBreak()
		{
			lastLineEnd = curLineEnd;
			curLineEnd = new CodeLocation(col - 1, line);
		}
		protected bool HandleLineEnd(char ch)
		{
			// Handle MS-DOS or MacOS line ends.
			if (ch == '\r')
			{
				if (reader.Peek() == '\n')
				{ // MS-DOS line end '\r\n'
					ReaderRead(); // LineBreak (); called by ReaderRead ();
					return true;
				}
				else
				{ // assume MacOS line end which is '\r'
					LineBreak();
					return true;
				}
			}
			if (ch == '\n')
			{
				LineBreak();
				return true;
			}
			return false;
		}

		protected void SkipToEndOfLine()
		{
			int nextChar;
			while ((nextChar = reader.Read()) != -1)
			{
				if (nextChar == '\r')
				{
					if (reader.Peek() == '\n')
						reader.Read();
					nextChar = '\n';
				}
				if (nextChar == '\n')
				{
					++line;
					col = 1;
					break;
				}
			}
		}

		protected string ReadToEndOfLine()
		{
			sb.Length = 0;
			int nextChar;
			while ((nextChar = reader.Read()) != -1)
			{
				char ch = (char)nextChar;

				if (nextChar == '\r')
				{
					if (reader.Peek() == '\n')
						reader.Read();
					nextChar = '\n';
				}
				// Return read string, if EOL is reached
				if (nextChar == '\n')
				{
					++line;
					col = 1;
					return sb.ToString();
				}

				sb.Append(ch);
			}

			// Got EOF before EOL
			string retStr = sb.ToString();
			col += retStr.Length;
			return retStr;
		}

		/// <summary>
		/// Skips to the end of the current code block.
		/// For this, the lexer must have read the next token AFTER the token opening the
		/// block (so that Lexer.DToken is the block-opening token, not Lexer.LookAhead).
		/// After the call, Lexer.LookAhead will be the block-closing token.
		/// </summary>
		public abstract void SkipCurrentBlock();
	}

	public class Lexer : AbstractLexer
	{
		public Lexer(TextReader reader)
			: base(reader)
		{
			if ((char)reader.Peek() == '#')
			{
				reader.ReadLine();
				HandleLineEnd('\n');
			}
		}

		public bool IsEOF
		{
			get { return lookaheadToken == null || lookaheadToken.Kind == DTokens.EOF || lookaheadToken.Kind == DTokens.__EOF__; }
		}

		#region Abstract Lexer Props & Methods
		public IList<ParserError> LexerErrors = new List<ParserError>();


		/// <summary>
		/// Set to false if normal block comments shall be logged, too.
		/// </summary>
		public bool OnlyEnlistDDocComments = true;

		/// <summary>
		/// A temporary storage for DDoc comments
		/// </summary>
		public List<Comment> Comments = new List<Comment>();
		void OnError(int line, int col, string message)
		{
			if (LexerErrors != null)
				LexerErrors.Add(new ParserError(false, message, CurrentToken != null ? CurrentToken.Kind : -1, new CodeLocation(col, line)));
		}
		#endregion

		protected override DToken Next()
		{
			int nextChar;
			char ch;
			bool hadLineEnd = false;
			if (Line == 1 && Col == 1) hadLineEnd = true; // beginning of document

			while ((nextChar = ReaderRead()) != -1)
			{
				DToken token;

				switch (nextChar)
				{
					case ' ':
					case '\t':
						continue;
					case '\r':
					case '\n':
						if (hadLineEnd)
						{
							// second line end before getting to a token
							// -> here was a blank line
							//specialTracker.AddEndOfLine(new Location(Col, Line));
						}
						HandleLineEnd((char)nextChar);
						hadLineEnd = true;
						continue;
					case '/':
						int peek = ReaderPeek();
						if (peek == '/' || peek == '*' || peek == '+')
						{
							ReadComment();
							continue;
						}
						else
						{
							token = ReadOperator('/');
						}
						break;
					case 'r':
						peek = ReaderPeek();
						if (peek == '"')
						{
							ReaderRead();
							token = ReadVerbatimString(peek);
							break;
						}
						else
							goto default;
					case '`':
						token = ReadVerbatimString(nextChar);
						break;
					case '"':
						token = ReadString(nextChar);
						break;
					case '\'':
						token = ReadChar();
						break;
					case '@':
						int next = ReaderRead();
						if (next == -1)
						{
							OnError(Line, Col, String.Format("EOF after @"));
							continue;
						}
						else
						{
							int x = Col - 1;
							int y = Line;
							ch = (char)next;
							if (Char.IsLetterOrDigit(ch) || ch == '_')
							{
								bool canBeKeyword;
								string ident = ReadIdent(ch, out canBeKeyword);

								token = new DToken(DTokens.PropertyAttribute, x - 1, y, ident);
							}
							else
							{
								OnError(y, x, String.Format("Unexpected char in Lexer.Next() : {0}", ch));
								continue;
							}
						}
						break;
					default:
						ch = (char)nextChar;

						if (ch == 'x')
						{
							peek = ReaderPeek();
							if (peek == '"') // HexString
							{
								ReaderRead(); // Skip the "

								string numString = "";

								while ((next = ReaderRead()) != -1)
								{
									ch = (char)next;

									if (IsHex(ch))
										numString += ch;
									else if (!Char.IsWhiteSpace(ch))
										break;
								}

								return new DToken(DTokens.Literal, Col - 1, Line, numString, ParseFloatValue(numString, 16), LiteralFormat.Scalar);
							}
						}
						else if (ch == 'q') // Token strings
						{
							peek = ReaderPeek();
							if (peek == '{'/*q{ ... }*/ || peek == '"'/* q"{{ ...}}   }}"*/)
							{
								int x = Col - 1;
								int y = Line;
								string initDelim = "";
								string endDelim = "";
								string tokenString = "";
								initDelim += (char)ReaderRead();
								bool IsQuoted = false;
								int BracketLevel = 0; // Only needed if IsQuoted is false

								// Read out initializer
								if (initDelim == "\"")
								{
									IsQuoted = true;
									initDelim = "";

									int pk = ReaderPeek();
									ch = (char)pk;
									if (Char.IsLetterOrDigit(ch)) // q"EOS EOS"
										while ((next = ReaderRead()) != -1)
										{
											ch = (char)next;
											if (!Char.IsWhiteSpace(ch))
												initDelim += ch;
											else
												break;
										}
									else if (ch == '(' || ch == '<' || ch == '[' || ch == '{')
									{
										var firstBracket = ch;
										while ((next = ReaderRead()) != -1)
										{
											ch = (char)next;
											if (ch == firstBracket)
												initDelim += ch;
											else
												break;
										}
									}
								}
								else if (initDelim == "{")
									BracketLevel = 1;

								// Build end delimiter
								endDelim = initDelim.Replace('{', '}').Replace('[', ']').Replace('(', ')').Replace('<', '>');
								if (IsQuoted) endDelim += "\"";

								// Read tokens
								bool inSuperComment = false,
									 inNestedComment = false;

								while ((next = ReaderRead()) != -1)
								{
									ch = (char)next;

									tokenString += ch;

									// comments are treated as part of the string inside of tokenized string. curly braces inside the comments are ignored. WEIRD!
									if (!inSuperComment && tokenString.EndsWith("/+")) inSuperComment = true;
									else if (inSuperComment && tokenString.EndsWith("+/")) inSuperComment = false;
									if (!inSuperComment)
									{
										if (!inNestedComment && tokenString.EndsWith("/*")) inNestedComment = true;
										else if (inNestedComment && tokenString.EndsWith("*/")) inNestedComment = false;
									}

									if (!inNestedComment && !inSuperComment)
									{
										if (!IsQuoted && ch == '{')
											BracketLevel++;
										if (!IsQuoted && ch == '}')
											BracketLevel--;
									}

									if (tokenString.EndsWith(endDelim) && (IsQuoted || BracketLevel < 1))
									{
										tokenString = tokenString.Remove(tokenString.Length - endDelim.Length);
										break;
									}
								}

								return new DToken(DTokens.Literal, x, y, tokenString, tokenString, LiteralFormat.VerbatimStringLiteral);
							}
						}

						if (Char.IsLetter(ch) || ch == '_' || ch == '\\')
						{
							int x = Col - 1; // Col was incremented above, but we want the start of the identifier
							int y = Line;
							bool canBeKeyword;
							string s = ReadIdent(ch, out canBeKeyword);
							if (canBeKeyword && DTokens.Keywords.ContainsValue(s))
							{
								foreach (var kv in DTokens.Keywords)
									if (s == kv.Value)
										return new DToken(kv.Key, x, y, s.Length);
							}
							return new DToken(DTokens.Identifier, x, y, s);
						}
						else if (Char.IsDigit(ch))
							token = ReadDigit(ch, Col - 1);
						else
							token = ReadOperator(ch);
						break;
				}

				// try error recovery (token = null -> continue with next char)
				if (token != null)
				{
					//token.prev = base.curToken;
					return token;
				}
			}

			return new DToken(DTokens.EOF, Col, Line, String.Empty);
		}

		// The C# compiler has a fixed size length therefore we'll use a fixed size char array for identifiers
		// it's also faster than using a string builder.
		const int MAX_IDENTIFIER_LENGTH = 512;
		char[] identBuffer = new char[MAX_IDENTIFIER_LENGTH];

		string ReadIdent(char ch, out bool canBeKeyword)
		{
			int peek;
			int curPos = 0;
			canBeKeyword = true;
			while (true)
			{
				if (ch == '\\')
				{
					peek = ReaderPeek();
					if (peek != 'u' && peek != 'U')
					{
						OnError(Line, Col, "Identifiers can only contain unicode escape sequences");
					}
					canBeKeyword = false;
					string surrogatePair;
					ReadEscapeSequence(out ch, out surrogatePair);
					if (surrogatePair != null)
					{
						if (!char.IsLetterOrDigit(surrogatePair, 0))
						{
							OnError(Line, Col, "Unicode escape sequences in identifiers cannot be used to represent characters that are invalid in identifiers");
						}
						for (int i = 0; i < surrogatePair.Length - 1; i++)
						{
							if (curPos < MAX_IDENTIFIER_LENGTH)
							{
								identBuffer[curPos++] = surrogatePair[i];
							}
						}
						ch = surrogatePair[surrogatePair.Length - 1];
					}
					else
					{
						if (!IsIdentifierPart(ch))
						{
							OnError(Line, Col, "Unicode escape sequences in identifiers cannot be used to represent characters that are invalid in identifiers");
						}
					}
				}

				if (curPos < MAX_IDENTIFIER_LENGTH)
				{
					identBuffer[curPos++] = ch;
				}
				else
				{
					OnError(Line, Col, String.Format("Identifier too long"));
					while (IsIdentifierPart(ReaderPeek()))
					{
						ReaderRead();
					}
					break;
				}
				peek = ReaderPeek();
				if (IsIdentifierPart(peek) || peek == '\\')
				{
					ch = (char)ReaderRead();
				}
				else
				{
					break;
				}
			}
			return new String(identBuffer, 0, curPos);
		}

		DToken ReadDigit(char ch, int x)
		{
			if (!Char.IsDigit(ch) && ch != '.')
			{
				OnError(Line, x, "Digit literals can only start with a digit (0-9) or a dot ('.')!");
				return null;
			}

			unchecked
			{ // prevent exception when ReaderPeek() = -1 is cast to char
				int y = Line;
				sb.Length = 0;
				sb.Append(ch);
				string prefix = null;
				string expSuffix = "";
				string suffix = null;
				int exponent = 1;

				bool HasDot = false;
				bool isunsigned = false;
				bool isfloat = false;
				bool islong = false;
				int NumBase = 0; // Set it to 0 initially - it'll be set to another value later for sure

				char peek = (char)ReaderPeek();

				// At first, check pre-comma values
				if (ch == '0')
				{
					if (peek == 'x' || peek == 'X') // Hex values
					{
						prefix = "0x";
						ReaderRead(); // skip 'x'
						sb.Length = 0; // Remove '0' from 0x prefix from the stringvalue
						NumBase = 16;

						peek = (char)ReaderPeek();
						while (IsHex(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}
					else if (peek == 'b' || peek == 'B') // Bin values
					{
						prefix = "0b";
						ReaderRead(); // skip 'b'
						sb.Length = 0;
						NumBase = 2;

						peek = (char)ReaderPeek();
						while (IsBin(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}
					// Oct values have been removed in dmd 2.053
					/*else if (IsOct(peek) || peek == '_') // Oct values
					{
						NumBase = 8;
						prefix = "0";
						sb.Length = 0;

						while (IsOct(peek) || peek == '_')
						{
							if (peek != '_')
								sb.Append((char)ReaderRead());
							else ReaderRead();
							peek = (char)ReaderPeek();
						}
					}*/
					else
						NumBase = 10; // Enables pre-comma parsing .. in this case we'd 000 literals or something like that
				}

				if (NumBase == 10 || (ch != '.' && NumBase == 0)) // Only allow further digits for 10-based integers, not for binary or hex values
				{
					NumBase = 10;
					while (Char.IsDigit(peek) || peek == '_')
					{
						if (peek != '_')
							sb.Append((char)ReaderRead());
						else ReaderRead();
						peek = (char)ReaderPeek();
					}
				}

				#region Read digits that occur after a comma
				DToken nextToken = null; // if we accidently read a 'dot'
				if ((NumBase == 0 && ch == '.') || peek == '.')
				{
					if (ch != '.') ReaderRead();
					else
					{
						NumBase = 10;
						sb.Length = 0;
						sb.Append('0');
					}
					peek = (char)ReaderPeek();
					if (!IsLegalDigit(peek, NumBase))
					{
						if (peek == '.')
						{
							ReaderRead();
							nextToken = new DToken(DTokens.DoubleDot, Col - 1, Line);
						}
					}
					else
					{
						HasDot = true;
						sb.Append('.');

						while (IsLegalDigit(peek, NumBase))
						{
							if (peek == '_')
								ReaderRead();
							else
								sb.Append((char)ReaderRead());
							peek = (char)ReaderPeek();
						}
					}
				}
				#endregion

				#region Exponents
				if ((NumBase == 16) ? (peek == 'p' || peek == 'P') : (peek == 'e' || peek == 'E'))
				{ // read exponent
					string suff = "e";
					ReaderRead();
					peek = (char)ReaderPeek();

					if (peek == '-' || peek == '+')
						expSuffix += (char)ReaderRead();
					peek = (char)ReaderPeek();
					while (Char.IsDigit(peek) || peek == '_')
					{ // read exponent value
						if (peek == '_')
							ReaderRead();
						else
							expSuffix += (char)ReaderRead();
						peek = (char)ReaderPeek();
					}

					// Exponents just can be decimal integers
					exponent = int.Parse(expSuffix);
					expSuffix = suff + expSuffix;
					peek = (char)ReaderPeek();
				}
				#endregion

				#region Suffixes
				if (!HasDot)
				{
				unsigned:
					if (peek == 'u' || peek == 'U')
					{
						ReaderRead();
						suffix += "u";
						isunsigned = true;
						peek = (char)ReaderPeek();
					}

					if (peek == 'L')
					{
						islong = true;
						ReaderRead();
						suffix += "L";
						//islong = true;
						peek = (char)ReaderPeek();
						if (!isunsigned && (peek == 'u' || peek == 'U'))
							goto unsigned;
					}
				}


				if (peek == 'f' || peek == 'F')
				{ // float value
					ReaderRead();
					suffix += "f";
					isfloat = true;
					peek = (char)ReaderPeek();
				}
				else if (peek == 'L')
				{ // real value
					ReaderRead();
					suffix += 'L';
					//isreal = true;
					islong = true;
					peek = (char)ReaderPeek();
				}

				if (peek == 'i')
				{ // imaginary value
					ReaderRead();
					suffix += "i";
					isfloat = true;
					//isimaginary = true;
				}
				#endregion

				string digit = sb.ToString();
				string stringValue = prefix + digit + expSuffix + suffix;

				DToken token = null;

				#region Parse the digit string

				var num = ParseFloatValue(digit, NumBase);

				if (exponent != 1)
					num = Math.Pow(num, exponent);

				object val = null;

				if (HasDot)
				{
					if (isfloat)
						val = (float)num;
					else
						val = (double)num;
				}
				else
				{
					if (isunsigned)
					{
						if (islong)
							val = (ulong)num;
						else
							val = (uint)num;
					}
					else
					{
						if (islong)
							val = (long)num;
						else
							val = (int)num;
					}
				}

				#endregion

				token = new DToken(DTokens.Literal, new CodeLocation(x, y), new CodeLocation(x + stringValue.Length, y), stringValue, val, isfloat || HasDot ? (LiteralFormat.FloatingPoint | LiteralFormat.Scalar) : LiteralFormat.Scalar);

				if (token != null) token.next = nextToken;
				return token;
			}
		}

		DToken ReadString(int initialChar)
		{
			int x = Col - 1;
			int y = Line;

			sb.Length = 0;
			originalValue.Length = 0;
			originalValue.Append((char)initialChar);
			bool doneNormally = false;
			int nextChar;
			while ((nextChar = ReaderRead()) != -1)
			{
				char ch = (char)nextChar;

				if (nextChar == initialChar)
				{
					doneNormally = true;
					originalValue.Append((char)nextChar);
					// Skip string literals
					ch = (char)this.ReaderPeek();
					if (ch == 'c' || ch == 'w' || ch == 'd') ReaderRead();
					break;
				}
				HandleLineEnd(ch);
				if (ch == '\\')
				{
					originalValue.Append('\\');
					string surrogatePair;

					originalValue.Append(ReadEscapeSequence(out ch, out surrogatePair));
					if (surrogatePair != null)
					{
						sb.Append(surrogatePair);
					}
					else
					{
						sb.Append(ch);
					}
				}
				else
				{
					originalValue.Append(ch);
					sb.Append(ch);
				}
			}

			if (!doneNormally)
			{
				OnError(y, x, String.Format("End of file reached inside string literal"));
			}

			return new DToken(DTokens.Literal, new CodeLocation(x, y), new CodeLocation(x + originalValue.Length, y), originalValue.ToString(), sb.ToString(), LiteralFormat.StringLiteral);
		}

		DToken ReadVerbatimString(int EndingChar)
		{
			sb.Length = 0;
			originalValue.Length = 0;
			int x = Col - 2; // r and " already read
			int y = Line;
			int nextChar;

			if (EndingChar == (int)'"')
			{
				originalValue.Append("r\"");
			}
			else
			{
				originalValue.Append((char)EndingChar);
				x = Col - 1;
			}
			while ((nextChar = ReaderRead()) != -1)
			{
				char ch = (char)nextChar;

				if (nextChar == EndingChar)
				{
					if (ReaderPeek() != (char)EndingChar)
					{
						originalValue.Append((char)EndingChar);
						break;
					}
					originalValue.Append((char)EndingChar);
					originalValue.Append((char)EndingChar);
					sb.Append((char)EndingChar);
					ReaderRead();
				}
				else if (HandleLineEnd(ch))
				{
					sb.Append("\r\n");
					originalValue.Append("\r\n");
				}
				else
				{
					sb.Append(ch);
					originalValue.Append(ch);
				}
			}

			if (nextChar == -1)
			{
				OnError(y, x, String.Format("End of file reached inside verbatim string literal"));
			}

			// Suffix literal check
			int pk = ReaderPeek();
			if (pk != -1)
			{
				nextChar = (char)pk;
				if (nextChar == 'c' || nextChar == 'w' || nextChar == 'd')
					ReaderRead();
			}

			return new DToken(DTokens.Literal, new CodeLocation(x, y), new CodeLocation(x + originalValue.Length, y), originalValue.ToString(), sb.ToString(), LiteralFormat.VerbatimStringLiteral);
		}

		char[] escapeSequenceBuffer = new char[12];

		/// <summary>
		/// reads an escape sequence
		/// </summary>
		/// <param name="ch">The character represented by the escape sequence,
		/// or '\0' if there was an error or the escape sequence represents a character that
		/// can be represented only be a suggorate pair</param>
		/// <param name="surrogatePair">Null, except when the character represented
		/// by the escape sequence can only be represented by a surrogate pair (then the string
		/// contains the surrogate pair)</param>
		/// <returns>The escape sequence</returns>
		string ReadEscapeSequence(out char ch, out string surrogatePair)
		{
			surrogatePair = null;

			int nextChar = ReaderRead();
			if (nextChar == -1)
			{
				OnError(Line, Col, String.Format("End of file reached inside escape sequence"));
				ch = '\0';
				return String.Empty;
			}
			int number;
			char c = (char)nextChar;
			int curPos = 1;
			escapeSequenceBuffer[0] = c;
			switch (c)
			{
				case '\'':
					ch = '\'';
					break;
				case '\"':
					ch = '\"';
					break;
				case '\\':
					ch = '\\';
					break;
				/*case '0':
					ch = '\0';
					break;*/
				case 'a':
					ch = '\a';
					break;
				case 'b':
					ch = '\b';
					break;
				case 'f':
					ch = '\f';
					break;
				case 'n':
					ch = '\n';
					break;
				case 'r':
					ch = '\r';
					break;
				case 't':
					ch = '\t';
					break;
				case 'v':
					ch = '\v';
					break;
				case 'u':
				case 'x':
					// 16 bit unicode character
					c = (char)ReaderRead();
					number = GetHexNumber(c);
					escapeSequenceBuffer[curPos++] = c;

					if (number < 0)
					{
						OnError(Line, Col - 1, String.Format("Invalid char in literal : {0}", c));
					}
					for (int i = 0; i < 3; ++i)
					{
						if (IsHex((char)ReaderPeek()))
						{
							c = (char)ReaderRead();
							int idx = GetHexNumber(c);
							escapeSequenceBuffer[curPos++] = c;
							number = 16 * number + idx;
						}
						else
						{
							break;
						}
					}
					ch = (char)number;
					break;
				case 'U':
					// 32 bit unicode character
					number = 0;
					for (int i = 0; i < 8; ++i)
					{
						if (IsHex((char)ReaderPeek()))
						{
							c = (char)ReaderRead();
							int idx = GetHexNumber(c);
							escapeSequenceBuffer[curPos++] = c;
							number = 16 * number + idx;
						}
						else
						{
							OnError(Line, Col - 1, String.Format("Invalid char in literal : {0}", (char)ReaderPeek()));
							break;
						}
					}
					if (number > 0xffff)
					{
						ch = '\0';
						surrogatePair = char.ConvertFromUtf32(number);
					}
					else
					{
						ch = (char)number;
					}
					break;

				// NamedCharacterEntities
				case '&':
					string charEntity = "";

					while (true)
					{
						nextChar = ReaderRead();

						if (nextChar < 0)
						{
							OnError(Line, Col - 1, "EOF reached within named char entity");
							ch = '\0';
							return string.Empty;
						}

						c = (char)nextChar;

						if (c == ';')
							break;

						if (char.IsLetter(c))
							charEntity += c;
						else
						{
							OnError(Line, Col - 1, "Unexpected character found in named char entity: " + c);
							ch = '\0';
							return string.Empty;
						}
					}

					if (string.IsNullOrEmpty(charEntity))
					{
						OnError(Line, Col - 1, "Empty named character entities not allowed");
						ch = '\0';
						return string.Empty;
					}

					//TODO: Performance improvement
					//var ret=System.Web.HttpUtility.HtmlDecode("&"+charEntity+";");

					ch = '#';//ret[0];

					return "&" + charEntity + ";";
				default:

					// Max 3 following octal digits
					if (IsOct(c))
					{
						// Parse+Convert oct to dec integer
						int oct = GetHexNumber(c);

						for (int i = 0; i < 2; ++i)
						{
							if (IsOct((char)ReaderPeek()))
							{
								c = (char)ReaderRead();
								escapeSequenceBuffer[curPos++] = c;

								int idx = GetHexNumber(c);
								oct = 8 * oct + idx;
							}
							else
								break;
						}

						// Convert integer to character
						if (oct > 0xffff)
						{
							ch = '\0';
							surrogatePair = char.ConvertFromUtf32(oct);
						}
						else
						{
							ch = (char)oct;
						}

					}
					else
					{
						OnError(Line, Col, String.Format("Unexpected escape sequence : {0}", c));
						ch = '\0';
					}
					break;
			}
			return new String(escapeSequenceBuffer, 0, curPos);
		}

		DToken ReadChar()
		{
			int x = Col - 1;
			int y = Line;
			int nextChar = ReaderRead();
			if (nextChar == -1)
			{
				OnError(y, x, String.Format("End of file reached inside character literal"));
				return null;
			}
			char ch = (char)nextChar;
			char chValue = ch;
			string escapeSequence = String.Empty;
			string surrogatePair = null;
			if (ch == '\\')
			{
				escapeSequence = ReadEscapeSequence(out chValue, out surrogatePair);
				if (surrogatePair != null)
				{
					// Although we'll pass back a string as literal value, it's originally handled as char literal!
				}
			}

			unchecked
			{
				if ((char)ReaderRead() != '\'')
				{
					OnError(y, x, String.Format("Char not terminated"));
				}
			}
			return new DToken(DTokens.Literal, new CodeLocation(x, y), new CodeLocation(x + 1, y), "'" + ch + escapeSequence + "'", string.IsNullOrEmpty(surrogatePair) ? (object)chValue : surrogatePair, LiteralFormat.CharLiteral);
		}

		DToken ReadOperator(char ch)
		{
			int x = Col - 1;
			int y = Line;
			switch (ch)
			{
				case '+':
					switch (ReaderPeek())
					{
						case '+':
							ReaderRead();
							return new DToken(DTokens.Increment, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.PlusAssign, x, y);
					}
					return new DToken(DTokens.Plus, x, y);
				case '-':
					switch (ReaderPeek())
					{
						case '-':
							ReaderRead();
							return new DToken(DTokens.Decrement, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.MinusAssign, x, y);
						case '>':
							ReaderRead();
							return new DToken(DTokens.TildeAssign, x, y);
					}
					return new DToken(DTokens.Minus, x, y);
				case '*':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.TimesAssign, x, y);
						default:
							break;
					}
					return new DToken(DTokens.Times, x, y);
				case '/':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.DivAssign, x, y);
					}
					return new DToken(DTokens.Div, x, y);
				case '%':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.ModAssign, x, y);
					}
					return new DToken(DTokens.Mod, x, y);
				case '&':
					switch (ReaderPeek())
					{
						case '&':
							ReaderRead();
							return new DToken(DTokens.LogicalAnd, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.BitwiseAndAssign, x, y);
					}
					return new DToken(DTokens.BitwiseAnd, x, y);
				case '|':
					switch (ReaderPeek())
					{
						case '|':
							ReaderRead();
							return new DToken(DTokens.LogicalOr, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.BitwiseOrAssign, x, y);
					}
					return new DToken(DTokens.BitwiseOr, x, y);
				case '^':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.XorAssign, x, y);
						case '^':
							ReaderRead();
							if (ReaderPeek() == '=')
							{
								ReaderRead();
								return new DToken(DTokens.PowAssign, x, y);
							}
							return new DToken(DTokens.Pow, x, y);
					}
					return new DToken(DTokens.Xor, x, y);
				case '!':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.NotEqual, x, y);

						case '<':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return new DToken(DTokens.NotLessThanAssign, x, y);
								case '>':
									ReaderRead();
									switch (ReaderPeek())
									{
										case '=':
											ReaderRead();
											return new DToken(DTokens.NotUnequalAssign, x, y); // !<>=
									}
									return new DToken(DTokens.NotUnequal, x, y); // !<>
							}
							return new DToken(DTokens.NotLessThan, x, y);

						case '>':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return new DToken(DTokens.NotGreaterThanAssign, x, y); // !>=
								default:
									break;
							}
							return new DToken(DTokens.NotGreaterThan, x, y); // !>

					}
					return new DToken(DTokens.Not, x, y);
				case '~':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.TildeAssign, x, y);
					}
					return new DToken(DTokens.Tilde, x, y);
				case '=':
					switch (ReaderPeek())
					{
						case '=':
							ReaderRead();
							return new DToken(DTokens.Equal, x, y);
					}
					return new DToken(DTokens.Assign, x, y);
				case '<':
					switch (ReaderPeek())
					{
						case '<':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return new DToken(DTokens.ShiftLeftAssign, x, y);
								default:
									break;
							}
							return new DToken(DTokens.ShiftLeft, x, y);
						case '>':
							ReaderRead();
							switch (ReaderPeek())
							{
								case '=':
									ReaderRead();
									return new DToken(DTokens.UnequalAssign, x, y);
								default:
									break;
							}
							return new DToken(DTokens.Unequal, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.LessEqual, x, y);
					}
					return new DToken(DTokens.LessThan, x, y);
				case '>':
					switch (ReaderPeek())
					{
						case '>':
							ReaderRead();
							if (ReaderPeek() != -1)
							{
								switch ((char)ReaderPeek())
								{
									case '=':
										ReaderRead();
										return new DToken(DTokens.ShiftRightAssign, x, y);
									case '>':
										ReaderRead();
										if (ReaderPeek() != -1)
										{
											switch ((char)ReaderPeek())
											{
												case '=':
													ReaderRead();
													return new DToken(DTokens.TripleRightShiftAssign, x, y);
											}
											return new DToken(DTokens.ShiftRightUnsigned, x, y); // >>>
										}
										break;
								}
							}
							return new DToken(DTokens.ShiftRight, x, y);
						case '=':
							ReaderRead();
							return new DToken(DTokens.GreaterEqual, x, y);
					}
					return new DToken(DTokens.GreaterThan, x, y);
				case '?':
					return new DToken(DTokens.Question, x, y);
				case '$':
					return new DToken(DTokens.Dollar, x, y);
				case ';':
					return new DToken(DTokens.Semicolon, x, y);
				case ':':
					return new DToken(DTokens.Colon, x, y);
				case ',':
					return new DToken(DTokens.Comma, x, y);
				case '.':
					// Prevent OverflowException when ReaderPeek returns -1
					int tmp = ReaderPeek();
					if (tmp > 0 && Char.IsDigit((char)tmp))
						return ReadDigit('.', Col - 1);
					else if (tmp == (int)'.')
					{
						ReaderRead();
						if ((char)ReaderPeek() == '.') // Triple dot
						{
							ReaderRead();
							return new DToken(DTokens.TripleDot, x, y);
						}
						return new DToken(DTokens.DoubleDot, x, y);
					}
					return new DToken(DTokens.Dot, x, y);
				case ')':
					return new DToken(DTokens.CloseParenthesis, x, y);
				case '(':
					return new DToken(DTokens.OpenParenthesis, x, y);
				case ']':
					return new DToken(DTokens.CloseSquareBracket, x, y);
				case '[':
					return new DToken(DTokens.OpenSquareBracket, x, y);
				case '}':
					return new DToken(DTokens.CloseCurlyBrace, x, y);
				case '{':
					return new DToken(DTokens.OpenCurlyBrace, x, y);
				default:
					return null;
			}
		}

		void ReadComment()
		{
			switch (ReaderRead())
			{
				case '+':
					if (ReaderPeek() == '+')// DDoc
						ReadMultiLineComment(Comment.Type.Documentation | Comment.Type.Block, true);
					else
						ReadMultiLineComment(Comment.Type.Block, true);
					break;
				case '*':
					if (ReaderPeek() == '*')// DDoc
						ReadMultiLineComment(Comment.Type.Documentation | Comment.Type.Block, false);
					else
						ReadMultiLineComment(Comment.Type.Block, false);
					break;
				case '/':
					if (ReaderPeek() == '/')// DDoc
						ReadSingleLineComment(Comment.Type.Documentation | Comment.Type.SingleLine);
					else
						ReadSingleLineComment(Comment.Type.SingleLine);
					break;
				default:
					OnError(Line, Col, String.Format("Error while reading comment"));
					break;
			}
		}

		void ReadSingleLineComment(Comment.Type commentType)
		{
			var st = new CodeLocation(Col, Line);
			string comm = ReadToEndOfLine().TrimStart('/');
			var end = new CodeLocation(Col, Line);

			if (commentType == Comment.Type.Documentation || !OnlyEnlistDDocComments)
				Comments.Add(new Comment(commentType, comm.Trim(), st.Column < 2, st, end));
		}

		void ReadMultiLineComment(Comment.Type commentType, bool isNestingComment)
		{
			int nestedCommentDepth = 1;
			int nextChar;
			CodeLocation st = new CodeLocation(Col, Line);
			StringBuilder scCurWord = new StringBuilder(); // current word, (scTag == null) or comment (when scTag != null)
			bool hadLineEnd = false;

			while ((nextChar = ReaderRead()) != -1)
			{
				
				char ch = (char)nextChar;

				// Catch deeper-nesting comments
				if (isNestingComment && ch == '/' && ReaderPeek() == '+')
				{
					nestedCommentDepth++;
					ReaderRead();
				}

				// End of multiline comment reached ?
				if ((isNestingComment ? ch == '+' : ch == '*') && ReaderPeek() == '/')
				{
					ReaderRead(); // Skip "*" or "+"

					if (nestedCommentDepth > 1)
						nestedCommentDepth--;
					else
					{
						if (commentType == Comment.Type.Documentation || !OnlyEnlistDDocComments)
							Comments.Add( new Comment(commentType, scCurWord.ToString().Trim(ch, ' ', '\t', '\r', '\n', isNestingComment ? '+' : '*'), st.Column < 2, st, new CodeLocation(Col, Line)));
						return;
					}
				}

				if (HandleLineEnd(ch))
				{
					scCurWord.AppendLine();
					hadLineEnd = true;
				}

				// Skip intial white spaces, leading + as well as *
				else if (hadLineEnd)
				{
					if (char.IsWhiteSpace(ch) || (isNestingComment ? ch == '+' : ch == '*'))
					{ }
					else
					{
						scCurWord.Append(ch);
						hadLineEnd = false;
					}
				}
				else
					scCurWord.Append(ch);
			}

			// Reached EOF before end of multiline comment.
			if (commentType == Comment.Type.Documentation || !OnlyEnlistDDocComments)
				Comments.Add(new Comment(commentType, scCurWord.ToString().Trim(), st.Column < 2, st, new CodeLocation(Col, Line)));

			OnError(Line, Col, String.Format("Reached EOF before the end of a multiline comment"));
		}

		/// <summary>
		/// Rawly skip the current code block
		/// </summary>
		public override void SkipCurrentBlock()
		{
			int braceCount = 0;
			// Scan already parsed tokens
			var tok = lookaheadToken;
			while (tok != null)
			{
				if (tok.Kind == DTokens.OpenCurlyBrace)
					braceCount++;
				else if (tok.Kind == DTokens.CloseCurlyBrace)
				{
					braceCount--;
					if (braceCount < 0)
					{
						lookaheadToken = tok;
						return;
					}
				}
				tok = tok.next;
			}

			// Scan/proceed tokens rawly (skip them only until braceCount<0)
			prevToken = LookAhead;
			int nextChar;
			while ((nextChar = ReaderRead()) != -1)
			{
				switch (nextChar)
				{
					// Handle line ends
					case '\r':
					case '\n':
						HandleLineEnd((char)nextChar);
						break;

					// Handle comments
					case '/':
						int peek = ReaderPeek();
						if (peek == '/' || peek == '*' || peek == '+')
						{
							ReadComment();
							continue;
						}
						break;

					// handle string literals
					case 'r':
						int pk = ReaderPeek();
						if (pk == '"')
						{
							ReaderRead();
							ReadVerbatimString('"');
						}
						break;
					case '`':
						ReadVerbatimString(nextChar);
						break;
					case '"':
						ReadString(nextChar);
						break;
					case '\'':
						ReadChar();
						break;

					case '{':
						braceCount++;
						continue;
					case '}':
						braceCount--;
						if (braceCount < 0)
						{
							lookaheadToken = new DToken(DTokens.CloseCurlyBrace, Col - 1, Line);
							StartPeek();
							Peek();
							return;
						}
						break;
				}
			}
		}
	}
}
