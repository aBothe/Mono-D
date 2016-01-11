using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL
{
	public class SdlParser
	{
		public readonly List<Error> ParseErrors = new List<Error>();
		readonly SdlLexer Lexer;

		public class Error
		{
			public readonly int Line;
			public readonly int Column;
			public readonly string Message;

			public Error(int line, int col, string msg)
			{
				Line = line;
				Column = col;
				Message = msg;
			}
		}

		private SdlParser(SdlLexer lex)
		{
			Lexer = lex;
		}

		private SdlParser(TextReader r) : this(new SdlLexer(r))
		{
		}

		public static SDLObject Parse(TextReader reader)
		{
			return new SdlParser(reader).ParseRoot();
		}

		public SDLObject ParseRoot()
		{
			return new SDLObject(string.Empty, new Tuple<string, string>[0], ParseChildren(false));
		}

		SDLDeclaration[] ParseChildren(bool braceForExit)
		{
			var l = new List<SDLDeclaration>();
			while (!Lexer.IsEOF)
			{
				Step();
				switch (Current.Kind)
				{
					case SdlLexer.Tokens.Identifier:
						l.Add(ParseDeclaration());
						break;
					case SdlLexer.Tokens.CloseBrace:
						if (braceForExit)
						{
							Step();
							return l.ToArray();
						}
						else
							goto default;
					default:
						ParseErrors.Add(new Error(Current.Line, Current.Column, "Invalid token: " + Current.Kind.ToString()));
						break;
					case SdlLexer.Tokens.EOF:
						if (braceForExit)
						{
							goto default;
						}
						break;
					case SdlLexer.Tokens.Invalid:
					case SdlLexer.Tokens.EOL:
						break;
				}
			}

			return l.ToArray();
		}

		public SDLDeclaration ParseDeclaration()
		{
			if (Expect(SdlLexer.Tokens.Identifier))
			{
				var name = Current.Value;
				var attributes = new List<Tuple<string, string>>();

				Step();
				TryDeclarationParseAttributes(attributes);

				if (Current.Kind == SdlLexer.Tokens.OpenBrace)
				{
					return new SDLObject(name, attributes, ParseChildren(true));
				}
				else
					return new SDLDeclaration(name, attributes);
			}
			else
				return null;
		}

		void TryDeclarationParseAttributes(List<Tuple<string, string>> attributes)
		{
			while (!Lexer.IsEOF)
			{
				var tk = Current;
				switch (tk.Kind)
				{
					case SdlLexer.Tokens.String:
						attributes.Add(new Tuple<string, string>(null, tk.Value));
						Step();
						break;
					case SdlLexer.Tokens.Identifier:
						var attrId = tk.Value;
						Lexer.Mark();
						Step();
						if (Current.Kind == SdlLexer.Tokens.Equals)
						{
							Step();
							if (Expect(SdlLexer.Tokens.String))
							{
								attributes.Add(new Tuple<string, string>(attrId, Current.Value));
								Step();
							}
						}
						else
							Lexer.Reset();
						break;
					default:
						return;
				}
			}
		}

		#region Lexer IO

		void Step()
		{
			Lexer.Step();
		}

		SdlLexer.Token Current
		{ get { return Lexer.CurrentToken; } }

		bool Expect(SdlLexer.Tokens kind)
		{
			var tk = Current;
			if (tk.Kind != kind)
			{
				ParseErrors.Add(new Error(tk.Line, tk.Column, kind.ToString() + " expected, " + tk.Kind.ToString() + " found"));
				return false;
			}
			return true;
		}

		#endregion


	}
}
