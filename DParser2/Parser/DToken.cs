using System;
using D_Parser.Dom;

namespace D_Parser.Parser
{
	[FlagsAttribute]
	public enum LiteralFormat
	{
		None = 0,
		Scalar = 1,
		FloatingPoint = 2,
		StringLiteral = 4,
		VerbatimStringLiteral = 8,
		CharLiteral = 16,
	}

	[Flags]
	public enum LiteralSubformat
	{
		None = 0,

		Integer = 1,
		Unsigned = 2,
		Long = 4,

		Double = 8,
		Float = 16,
		Real = 32,
		Imaginary = 64,

		Utf8=128,
		Utf16=256,
		Utf32=512,
	}

    public class DToken
	{
		#region Properties
		public readonly int Line;
		public readonly CodeLocation Location;
		readonly CodeLocation explicitEndLocation;
		public readonly int TokenLength;

		public readonly int Kind;
        public readonly LiteralFormat LiteralFormat;
		/// <summary>
		/// Used for scalar, floating and string literals.
		/// Marks special formats such as explicit unsigned-ness, wide char or dchar-based strings etc.
		/// </summary>
		public readonly LiteralSubformat Subformat;
        public readonly object LiteralValue;
        public readonly string Value;
        internal DToken next;

		public DToken Next
		{
			get { return next; }
		}

		public CodeLocation EndLocation
		{
			get
			{
				return TokenLength == 0 ? explicitEndLocation : new CodeLocation(Location.Column + TokenLength, Line);
			}
		}
		#endregion

		#region Constructors
		public DToken(int kind, int startLocation_Col, int startLocation_Line, int tokenLength,
			object literalValue, string value, LiteralFormat literalFormat = 0, LiteralSubformat literalSubFormat = 0)
		{
			Line = startLocation_Line;
			Location = new CodeLocation(startLocation_Col, startLocation_Line);
			TokenLength = tokenLength;

			Kind = kind;
			LiteralFormat = literalFormat;
			Subformat = literalSubFormat;
			LiteralValue = literalValue;
			Value = value;
		}

		public DToken(int kind, int startLocation_Col, int startLocation_Line, CodeLocation endLocation,
			object literalValue, string value, LiteralFormat literalFormat = 0, LiteralSubformat literalSubFormat = 0)
		{
			Line = startLocation_Line;
			Location = new CodeLocation(startLocation_Col, startLocation_Line);
			explicitEndLocation = endLocation;

			Kind = kind;
			LiteralFormat = literalFormat;
			Subformat = literalSubFormat;
			LiteralValue = literalValue;
			Value = value;
		}

		public DToken(int kind, int startLocation_Col, int startLocation_Line, int tokenLength)
		{
			Kind = kind;

			Line = startLocation_Line;
			Location = new CodeLocation(startLocation_Col, startLocation_Line);
			TokenLength = tokenLength;
		}

		/// <summary>
		/// Assumes a token length of 1
		/// </summary>
		public DToken(int kind, int startLocation_Col, int startLocation_Line)
		{
			Kind = kind;

			Line = startLocation_Line;
			Location = new CodeLocation(startLocation_Col, startLocation_Line);
			TokenLength = 1;
		}

		public DToken(int kind, int col, int line, string val)
		{
			Kind = kind;

			Line = line;
			Location = new CodeLocation(col,line);
			TokenLength = val == null ? 1 : val.Length;
			Value = val;
		}
		#endregion

		public override string ToString()
        {
            if (Kind == DTokens.Identifier || Kind == DTokens.Literal)
                return Value;
            return DTokens.GetTokenString(Kind);
        }
    }

    public struct Comment
    {
		[Flags]
        public enum Type
        {
            Block=1,
            SingleLine=2,
            Documentation=4
        }

        public readonly Type CommentType;
        public readonly string CommentText;
        public readonly CodeLocation StartPosition;
        public readonly CodeLocation EndPosition;

        /// <value>
        /// Is true, when the comment is at line start or only whitespaces
        /// between line and comment start.
        /// </value>
        public readonly bool CommentStartsLine;

        public Comment(Type commentType, string comment, bool commentStartsLine, CodeLocation startPosition, CodeLocation endPosition)
        {
            this.CommentType = commentType;
            this.CommentText = comment;
            this.CommentStartsLine = commentStartsLine;
            this.StartPosition = startPosition;
            this.EndPosition = endPosition;
        }
    }
}