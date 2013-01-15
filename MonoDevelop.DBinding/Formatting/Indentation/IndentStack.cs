using System;
using System.Text;
using D_Parser.Parser;

namespace MonoDevelop.D.Formatting.Indentation
{
	public enum Inside {
		Empty              = 0,
		
		PreProcessor       = (1 << 0),
		
		BlockComment   = (1 << 1),
		NestedComment      = (1 << 13),
		LineComment        = (1 << 2),
		DocComment         = (1 << 11),
		Comment            = (BlockComment | NestedComment | LineComment | DocComment),
		
		VerbatimString     = (1 << 3),
		StringLiteral      = (1 << 4),
		CharLiteral        = (1 << 5),
		String             = (VerbatimString | StringLiteral),
		StringOrChar       = (String | CharLiteral),
		
		Attribute          = (1 << 6),
		ParenList          = (1 << 7),
		
		FoldedStatement    = (1 << 8),
		Block              = (1 << 9),
		Case               = (1 << 10),
		
		FoldedOrBlock      = (FoldedStatement | Block),
		FoldedBlockOrCase  = (FoldedStatement | Block | Case)
	}
	
	/// <summary>
	/// Description of IndentStack.
	/// </summary>
	public class IndentStack: ICloneable {
		readonly static int INITIAL_CAPACITY = 16;
		
		struct Node {
			public Inside inside;
			public byte keyword;
			public string indent;
			public int nSpaces;
			public int lineNr;
			
			public override string ToString ()
			{
				return string.Format ("[Node: inside={0}, keyword={1}, indent={2}, nSpaces={3}, lineNr={4}]", inside, DTokens.GetTokenString(keyword), indent, nSpaces, lineNr);
			}
		};
		
		Node[] stack;
		int size;
		DIndentEngine engine;
		
		public IndentStack (DIndentEngine engine) : this (engine, INITIAL_CAPACITY)
		{
		}
		
		public IndentStack (DIndentEngine engine, int capacity)
		{
			this.engine = engine;
			if (capacity < INITIAL_CAPACITY)
				capacity = INITIAL_CAPACITY;
			
			this.stack = new Node [capacity];
			this.size = 0;
		}
		
		public bool IsEmpty {
			get { return size == 0; }
		}
		
		public int Count {
			get { return size; }
		}
		
		public object Clone ()
		{
			var clone = new IndentStack (engine, stack.Length);
			
			clone.stack = (Node[]) stack.Clone ();
			clone.size = size;
			
			return clone;
		}
		
		public void Reset ()
		{
			for (int i = 0; i < size; i++) {
				stack[i].keyword = DTokens.INVALID;
				stack[i].indent = null;
			}
			
			size = 0;
		}
		
		public void Push (Inside inside, byte keyword, int lineNr, int nSpaces)
		{
			int sp = size - 1;
			Node node;
			int n = 0;
			
			var indentBuilder = new StringBuilder ();
			if ((inside & (Inside.Attribute | Inside.ParenList)) != 0) {
				if (size > 0 && stack[sp].inside == inside) {
					while (sp >= 0) {
						if ((stack[sp].inside & Inside.FoldedOrBlock) != 0)
							break;
						sp--;
					}
					if (sp >= 0) {
						indentBuilder.Append (stack[sp].indent);
						if (stack[sp].lineNr == lineNr)
							n = stack[sp].nSpaces;
					}
				} else {
					while (sp >= 0) {
						if ((stack[sp].inside & Inside.FoldedBlockOrCase) != 0) {
							indentBuilder.Append (stack[sp].indent);
							break;
						}
						
						sp--;
					}
				}
				if (nSpaces - n <= 0) {
					indentBuilder.Append ('\t');
				} else {
					indentBuilder.Append (' ', nSpaces - n);
				}
			} else if ((inside & (Inside.NestedComment | Inside.BlockComment)) != 0) {
				if (size > 0) {
					indentBuilder.Append (stack[sp].indent);
					if (stack[sp].lineNr == lineNr)
						n = stack[sp].nSpaces;
				}
				
				indentBuilder.Append (' ', nSpaces - n);
			} else if (inside == Inside.Case) {
				while (sp >= 0) {
					if ((stack[sp].inside & Inside.FoldedOrBlock) != 0) {
						indentBuilder.Append (stack[sp].indent);
						break;
					}
					
					sp--;
				}
				
				if (engine.Policy.IndentSwitchBody)
					indentBuilder.Append ('\t');
				
				nSpaces = 0;
			} else if ((inside & (Inside.FoldedOrBlock)) != 0) {
				while (sp >= 0) {
					if ((stack[sp].inside & Inside.FoldedBlockOrCase) != 0) {
						indentBuilder.Append (stack[sp].indent);
						break;
					}
					
					sp--;
				}
				
				Inside parent = size > 0 ? stack[size - 1].inside : Inside.Empty;
				
				// This is a workaround to make anonymous methods indent nicely
				if (parent == Inside.ParenList)
					stack[size - 1].indent = indentBuilder.ToString ();
				
				if (inside == Inside.FoldedStatement) {
					indentBuilder.Append ('\t');
				} else if (inside == Inside.Block) {
					if (parent != Inside.Case || nSpaces != -1)
						indentBuilder.Append ('\t');
				}
				
				nSpaces = 0;
			} else if ((inside & (Inside.PreProcessor | Inside.StringOrChar)) != 0) {
				// if these fold, do not indent
				nSpaces = 0;
				
				//pop regions back out
				/*if (keyword == "region" || keyword == "endregion") {
					for (; sp >= 0; sp--) {
						if ((stack[sp].inside & Inside.FoldedBlockOrCase) != 0) {
							indentBuilder.Append (stack[sp].indent);
							break;
						}
					}
				}*/
			} else if (inside == Inside.LineComment || inside == Inside.DocComment) {
				// can't actually fold, but we still want to push it onto the stack
				nSpaces = 0;
			} else {
				// not a valid argument?
				throw new ArgumentOutOfRangeException ();
			}
			
			node.indent = indentBuilder.ToString ();
			node.keyword = keyword;
			node.nSpaces = nSpaces;
			node.lineNr = lineNr;
			node.inside = inside;
			
			if (size == stack.Length)
				Array.Resize <Node> (ref stack, 2 * size);
			
			stack[size++] = node;
		}
		
		public void Push (Inside inside, byte keyword, int lineNr, int nSpaces, string indent)
		{
			Node node;
			
			node.indent = indent;
			node.keyword = keyword;
			node.nSpaces = nSpaces;
			node.lineNr = lineNr;
			node.inside = inside;
			
			if (size == stack.Length)
				Array.Resize <Node> (ref stack, 2 * size);
			
			stack[size++] = node;
		}
		
		public void Pop ()
		{
			if (size == 0)
				throw new InvalidOperationException ();
			
			int sp = size - 1;
			stack[sp].keyword = DTokens.INVALID;
			stack[sp].indent = null;
			size = sp;
		}
		
		public Inside PeekInside (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return Inside.Empty;
			
			return stack[size - up - 1].inside;
		}
		
		public byte PeekKeyword (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return DTokens.INVALID;
			
			return stack[size - up - 1].keyword;
		}
		
		public string PeekIndent (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return String.Empty;
			
			return stack[size - up - 1].indent;
		}
		
		public int PeekLineNr (int up)
		{
			if (up < 0)
				throw new ArgumentOutOfRangeException ();
			
			if (up >= size)
				return -1;
			
			return stack[size - up - 1].lineNr;
		}
	}
}
