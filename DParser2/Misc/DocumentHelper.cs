using D_Parser.Dom;

namespace D_Parser
{
	public class DocumentHelper
	{
		public static CodeLocation OffsetToLocation(string Text, int Offset)
		{
			int line = 1;
			int col = 1;

			char c = '\0';
			for (int i = 0; i < Offset; i++)
			{
				c = Text[i];

				col++;

				if (c == '\n')
				{
					line++;
					col = 1;
				}
			}

			return new CodeLocation(col, line);
		}

		public static int LocationToOffset(string Text, CodeLocation Location)
		{
			int line = 1;
			int col = 1;

			int i = 0;
			for (; i < Text.Length && !(line >= Location.Line && col >= Location.Column); i++)
			{
				col++;

				if (Text[i] == '\n')
				{
					line++;
					col = 1;
				}
			}

			return i;
		}

		public static int GetLineEndOffset(string Text, int line)
		{
			int curline = 1;
			
			int i = 0;
			for (; i < Text.Length && curline <= line; i++)
				if (Text[i] == '\n')
				{
					curline++;

					if (curline > line)
					{
						if (i > 0 && Text[i - 1] == '\r')
							return i-1;

						return i;
					}
				}

			return i;
		}
	}
}
