using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL
{
	class SdlLexer
	{
		readonly TextReader reader;
		int currentLine = 1;
		int currentCol = 1;

		public bool IsEOF { get; private set; }

		public Token CurrentToken
		{
			get;
			private set;
		}

		public Token PeekToken
		{
			get;
			private set;
		}

		public SdlLexer(TextReader reader)
		{
			this.reader = reader;
		}

		public void Step()
		{
			if (CurrentToken == null || CurrentToken.next == null)
				CurrentToken = ReadToken();
			else
				CurrentToken = CurrentToken.next;

			ResetPeek();
		}

		public void ResetPeek()
		{
			PeekToken = CurrentToken;
		}

		public void Peek()
		{
			if(PeekToken == null)
			{
				if(CurrentToken == null)
					Step();
				ResetPeek();
				return;
			}

			if (PeekToken.next != null) {
				PeekToken = PeekToken.next;
				return;
			}

			PeekToken.next = ReadToken();
			
			PeekToken = PeekToken.next;
			// CurrentToken still keeps references to prior tokens
		}


		public enum Tokens
		{
			Identifier,
			String,
			Equals,
			Colon,
			OpenBrace,
			CloseBrace,
			EOL,
			EOF,
			Invalid,
		}

		public class Token
		{
			public readonly Tokens Kind;
			public readonly string Value;
			public readonly int Line;
			public readonly int Column;
			internal Token next;

			public Token(Tokens kind, int line, int col) : this(kind, null, line, col) { }

			public Token(Tokens kind, string value, int line, int col)
			{
				Kind = kind;
				Value = value;
				Line = line;
				Column = col;
			}
		}

		Token ReadToken()
		{
			for (int cur = reader.Read(); cur >= 0; cur = reader.Read())
			{
				int x = currentCol;
				int y = currentLine;
				currentCol++;

				switch (cur)
				{
					case '{':
						return new Token(Tokens.OpenBrace, y, x);
					case '}':
						return new Token(Tokens.CloseBrace, y, x);
					case '=':
						return new Token(Tokens.Equals, y, x);
					case ':':
						return new Token(Tokens.Colon, y, x);
					case '"':
						return ReadStringLiteral();
					case ' ':
					case '\t':
					case '\r':
						continue;
					case '\n':
						currentCol = 1;
						currentLine++;
						return new Token(Tokens.EOL, y, x);
					case '/':
						if (reader.Peek() == '/') // Comment
						{
							SkipToPeekEOL();
							continue;
						}
						else
							goto default;
					default:
						char curChar = (char)cur;
						if (IsDigit(curChar))
							return ReadNumeral(curChar);
						if (IsLetterOrDigit(curChar))							
							return new Token(Tokens.Identifier, ReadIdentifier(curChar), y, x);

						if (char.IsWhiteSpace(curChar))
							continue;

						return new Token(Tokens.Invalid, curChar.ToString(), y, x);
				}
			}

			IsEOF = true;
			return new Token(Tokens.EOF, currentLine, currentCol);
		}

		string ReadIdentifier(char firstChar)
		{
			var sb = new StringBuilder();
			sb.Append(firstChar);

			for (int cur = reader.Peek(); IsLetterOrDigit(cur); cur = reader.Peek())
				sb.Append((char)reader.Read());
			currentCol += sb.Length;

			return sb.ToString();
		}

		Token ReadNumeral(char firstChar)
		{
			int x = currentCol - 1;
			var sb = new StringBuilder();

			for (int cur = reader.Peek(); IsDigit(cur) || cur == '.'; cur = reader.Peek())
				sb.Append((char)reader.Read());
			currentCol += sb.Length;

			return new Token(Tokens.String, currentLine, x);
		}

		Token ReadStringLiteral()
		{
			// opening " has been read already
			int x = currentCol - 1;
			var sb = new StringBuilder();

			for (int cur = reader.Peek(); cur >= 0; cur = reader.Peek())
			{
				switch (cur)
				{
					case '\r':
					case '\n':
						return new Token(Tokens.Invalid, sb.ToString(), currentLine, x);
					case '"':
						reader.Read();
						currentCol++;
						return new Token(Tokens.String, sb.ToString(), currentLine, x);
					default:
						reader.Read();
						currentCol++;
						sb.Append((char)cur);
						break;
				}
			}

			return new Token(Tokens.Invalid, sb.ToString(), currentLine, x);
		}

		void SkipToPeekEOL()
		{
			for (int cur = reader.Peek(); cur != -1 && cur != '\n'; cur = reader.Peek())
			{
				reader.Read();
				currentCol++;
			}
		}

		public static bool IsDigit(int ch)
		{
			switch (ch)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return true;
				default:
					return false;
			}
		}

		public static bool IsLetterOrDigit(int ch)
		{
			switch (ch)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':

				case 'a':
				case 'A':
				case 'b':
				case 'B':
				case 'c':
				case 'C':
				case 'd':
				case 'D':
				case 'e':
				case 'E':
				case 'f':
				case 'F':
				case 'g':
				case 'G':
				case 'h':
				case 'H':
				case 'i':
				case 'I':
				case 'j':
				case 'J':
				case 'k':
				case 'K':
				case 'l':
				case 'L':
				case 'm':
				case 'M':
				case 'n':
				case 'N':
				case 'o':
				case 'O':
				case 'p':
				case 'P':
				case 'q':
				case 'Q':
				case 'r':
				case 'R':
				case 's':
				case 'S':
				case 't':
				case 'T':
				case 'u':
				case 'U':
				case 'v':
				case 'V':
				case 'w':
				case 'W':
				case 'x':
				case 'X':
				case 'y':
				case 'Y':
				case 'z':
				case 'Z':
				case '_':
					return true;
				case ' ':
				case '@':
				case '/':
				case '(':
				case ')':
				case '[':
				case ']':
				case '{':
				case '}':
				case '=':
				case '\"':
				case '\'':
				case -1:
					return false;
				default:
					return char.IsLetter((char)ch);
			}
		}
	}
}
