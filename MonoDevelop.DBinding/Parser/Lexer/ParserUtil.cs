/// <summary>
/// The following code was taken from SharpDevelop
/// </summary>

using System;
using MonoDevelop.Projects.Dom;
namespace MonoDevelop.D.Parser.Lexer
{
    public enum LiteralFormat : byte
    {
        None,
        Scalar,
        StringLiteral,
        VerbatimStringLiteral,
        CharLiteral,
    }

    public class DToken
    {
        internal readonly int col;
        internal readonly int line;

        internal readonly LiteralFormat literalFormat;
        internal readonly object literalValue;
        internal readonly string val;
        internal DToken next;
        readonly DomLocation endLocation;

        public readonly int Kind;

        public LiteralFormat LiteralFormat
        {
            get { return literalFormat; }
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

        public DomLocation EndLocation
        {
            get { return endLocation; }
        }

        public DomLocation Location
        {
            get
            {
                return new DomLocation(line,col);
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

        public DToken(int kind, int col, int line) : this(kind, col, line, null) { }
        public DToken(int kind, int col, int line, string val)
        {
            this.Kind = kind;
            this.col = col;
            this.line = line;
            this.val = val;
            this.endLocation = new DomLocation(line,col + (val == null ? 1 : val.Length));
        }
        public DToken(int kind, int column, int line, string val, object literalValue, LiteralFormat literalFormat)
            : this(kind, new DomLocation(line,column), new DomLocation(line,column + val.Length), val, literalValue, literalFormat)
        {
        }

        public DToken(int kind, DomLocation startLocation, DomLocation endLocation, string val, object literalValue, LiteralFormat literalFormat)
        {
            this.Kind = kind;
            this.col = startLocation.Column;
            this.line = startLocation.Line;
            this.endLocation = endLocation;
            this.val = val;
            this.literalValue = literalValue;
            this.literalFormat = literalFormat;
        }
    }
}