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

		public CodeLocation(int column, int line)
		{
			x = column;
			y = line;
		}

		int x, y;

		public int X
		{
			get { return x; }
			set { x = value; }
		}

		public int Y
		{
			get { return y; }
			set { y = value; }
		}

		public int Line
		{
			get { return y; }
			set { y = value; }
		}

		public int Column
		{
			get { return x; }
			set { x = value; }
		}

		public bool IsEmpty
		{
			get
			{
				return x <= 0 && y <= 0;
			}
		}

		public override string ToString()
		{
			return string.Format("(Line {1}, Col {0})", this.x, this.y);
		}

		public override int GetHashCode()
		{
			return unchecked(87 * x.GetHashCode() ^ y.GetHashCode());
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
			return a.x == b.x && a.y == b.y;
		}

		public static bool operator !=(CodeLocation a, CodeLocation b)
		{
			return a.x != b.x || a.y != b.y;
		}

		public static bool operator <(CodeLocation a, CodeLocation b)
		{
			if (a.y < b.y)
				return true;
			else if (a.y == b.y)
				return a.x < b.x;
			else
				return false;
		}

		public static bool operator >(CodeLocation a, CodeLocation b)
		{
			if (a.y > b.y)
				return true;
			else if (a.y == b.y)
				return a.x > b.x;
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
