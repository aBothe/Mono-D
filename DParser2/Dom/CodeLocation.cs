using System;

namespace D_Parser.Dom
{
	/// <summary>
	/// A line/column position.
	/// NRefactory lines/columns are counting from one.
	/// </summary>
	public struct CodeLocation : IComparable<CodeLocation>, IEquatable<CodeLocation>
	{
		public static readonly CodeLocation Empty = new CodeLocation(-1, -1);

		public readonly int Column, Line;

		public CodeLocation(int column, int line)
		{
			Column = column;
			Line = line;
		}

		public bool IsEmpty
		{
			get
			{
				return Column < 0 && Line < 0;
			}
		}

		public override string ToString()
		{
			return string.Format("(Line {1}, Col {0})", Column, Line);
		}

		public override int GetHashCode()
		{
			return unchecked(87 * Column.GetHashCode() ^ Line.GetHashCode());
		}

		public override bool Equals(object obj)
		{
			if (!(obj is CodeLocation)) return false;
			return (CodeLocation)obj == this;
		}

		public bool Equals(CodeLocation other)
		{
			return this == other;
		}

		public static bool operator ==(CodeLocation a, CodeLocation b)
		{
			return a.Column == b.Column && a.Line == b.Line;
		}

		public static bool operator !=(CodeLocation a, CodeLocation b)
		{
			return a.Column != b.Column || a.Line != b.Line;
		}

		public static bool operator <(CodeLocation a, CodeLocation b)
		{
			if (a.Line < b.Line)
				return true;
			else if (a.Line == b.Line)
				return a.Column < b.Column;
			else
				return false;
		}

		public static bool operator >(CodeLocation a, CodeLocation b)
		{
			if (a.Line > b.Line)
				return true;
			else if (a.Line == b.Line)
				return a.Column > b.Column;
			else
				return false;
		}

		public static bool operator <=(CodeLocation a, CodeLocation b)
		{
			return !(a > b);
		}

		public static bool operator >=(CodeLocation a, CodeLocation b)
		{
			return !(a < b);
		}

		public int CompareTo(CodeLocation other)
		{
			if (this == other)
				return 0;
			if (this < other)
				return -1;
			else
				return 1;
		}
	}
}
