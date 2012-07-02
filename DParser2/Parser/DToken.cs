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
        internal readonly int col;
        internal readonly int line;

        internal LiteralFormat literalFormat;
        internal object literalValue;
        internal string val;
        internal DToken next;
        readonly CodeLocation endLocation;

        public readonly int Kind;

        public LiteralFormat LiteralFormat
        {
            get { return literalFormat; }
        }

		/// <summary>
		/// Used for scalar, floating and string literals.
		/// Marks special formats such as explicit unsigned-ness, wide char or dchar-based strings etc.
		/// </summary>
		public LiteralSubformat Subformat
		{
			get;
			internal set;
		}

        public object LiteralValue
        {
            get { return literalValue; }
        }

        public string Value
        {
            get { return val; }
        }

        public DToken Next
        {
            get { return next; }
        }

        public CodeLocation EndLocation
        {
            get { return endLocation; }
        }

        public CodeLocation Location
        {
            get
            {
                return new CodeLocation(col, line);
            }
        }

        public override string ToString()
        {
            if (Kind == DTokens.Identifier || Kind == DTokens.Literal)
                return val;
            return DTokens.GetTokenString(Kind);
        }

        public DToken(DToken t)
            : this(t.Kind, t.col, t.line, t.val, t.literalValue, t.LiteralFormat)
        {
            next = t.next;
        }

        public DToken(int kind, int col, int line, string val)
        {
            this.Kind = kind;
            this.col = col;
            this.line = line;
            this.val = val;
            this.endLocation = new CodeLocation(col + (val == null ? 1 : val.Length), line);
        }

		public DToken(int kind, int col, int line, int TokenLength=0)
		{
			this.Kind = kind;
			this.col = col;
			this.line = line;
			this.endLocation = new CodeLocation(col+TokenLength,line);
		}

		public DToken(int kind, int col, int line, int TokenLength, object literalValue)
		{
			this.Kind = kind;
			this.col = col;
			this.line = line;
			this.endLocation = new CodeLocation(col + TokenLength, line);

			this.literalValue = literalValue;
			this.val = literalValue is string ? literalValue as string : literalValue.ToString();
		}

        public DToken(int kind, int x, int y, string val, object literalValue, LiteralFormat literalFormat, LiteralSubformat Subformat=0)
            : this(kind, new CodeLocation(x, y), new CodeLocation(x + val.Length, y), val, literalValue, literalFormat, Subformat)
        {
        }

        public DToken(int kind, CodeLocation startLocation, CodeLocation endLocation, string val, object literalValue, 
			LiteralFormat literalFormat,
			LiteralSubformat Subformat=0)
        {
            this.Kind = kind;
            this.col = startLocation.Column;
            this.line = startLocation.Line;
            this.endLocation = endLocation;
            this.val = val;
            this.literalValue = literalValue;
            this.literalFormat = literalFormat;
			this.Subformat = Subformat;
        }
    }

    public class Comment
    {
		[Flags]
        public enum Type
        {
            Block=1,
            SingleLine=2,
            Documentation=4
        }

        public Type CommentType;
        public string CommentText;
        public CodeLocation StartPosition;
        public CodeLocation EndPosition;

        /// <value>
        /// Is true, when the comment is at line start or only whitespaces
        /// between line and comment start.
        /// </value>
        public bool CommentStartsLine;

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