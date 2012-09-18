using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Formatting
{
	public class IndentationCalculator
	{
		public static int CalculateForward(IBlockNode module, CodeLocation caret)
		{
			return CalculateBackward(DResolver.SearchBlockAt(module, caret), caret);
		}

		/// <summary>
		/// Calculates the indentation from the inner-most to the outer-most block/section.
		/// </summary>
		public static int CalculateBackward(IBlockNode n, CodeLocation caret)
		{
			int i = 0;
			var line = caret.Line;

			while (n != null)
			{
				var db = n as DBlockNode;
				if (db != null)
				{
					if(!n.BlockStartLocation.IsEmpty)
						i += GetBlockindent(n, n.BlockStartLocation, GetLastBlockAstChild(db), caret);

					var metaStack = db.GetMetaBlockStack(caret, true, true);
					for (int k = metaStack.Length; k != 0; k--)
					{
						var mb = metaStack[k];
						var mbb = mb as IMetaDeclarationBlock;
						if (mbb != null)
							i += GetBlockindent(metaStack[i], mbb.BlockStartLocation, k == 0 ? null : metaStack[k - 1], caret);
						else if (line > mb.Location.Line)
						{
							/*
							 * pragma(lib,
							 *		"kernel32.lib");
							 * private:
							 *		int a;
							 */
							i++;
						}
					}
				}
				else if(n is DMethod)
				{
					var dm = (DMethod)n;

					/*
					 * Xvoid foo() Y{ }Z // Do not indent because 1) the method is one line large and 2) the caret is at the first line
					 * 
					 * Xint foo()
					 *		@trusted
					 *		@safe nothrow // 5)
					 * in Y{ // 3) do not indent because of the brace
					 *		a + b;
					 * }Z
					 * body
					 * { | } // 4) do not indent if it's a one-lined block statement
					 * out(r) // 5) do not indent after the first definition part, if we're not inside a block
					 * {
					 *		assert(r>3456);
					 *		if(r > 10000)
					 *			a + b; } | // 6) Though we're outside the block, it's needed to indent dependent on the block's contents
					 * 
					 * void foo()
					 * { } | // 7) same as 4)
					 */

					if (line > dm.Location.Line // 1), 2)
						/* No check for dm.EndLocation.Line > dm.Location.Line required due to the first check
						 * -- if the method was single-line but the line > the location line, it wasn't handled right now ;) */)
					{
						var s = dm.GetSubBlockAt(caret) as IStatement;

						if (s != null)
						{
							if (s.EndLocation.Line > s.Location.Line && // 4)
								line > s.Location.Line) // 3)
								i += Calculate(ref s, caret);
						}
						else
						{
							// Get the block that is the most nearest to the caret
							foreach (var b in new[] { dm.In, dm.Body, dm.Out })
								if (b != null && caret > b.EndLocation && line == b.EndLocation.Line && // 6)
									(s == null || b.Location > s.EndLocation))
									s = b;

							if (s != null && s.EndLocation.Line > s.Location.Line) // 7)
								i += Calculate(ref s, caret);
						}
					}
				}

				// Handle non-IBlockNode nodes because they were not regarded during SeachBlockAt()
				// TODO: Are anonymous classes handled by SearchBlockAt()?
				if(n.Count != 0)
					foreach (var subNode in n.Children)
						if (subNode is DVariable)
						{
							var dv = (DVariable)subNode;

							/*
							 * // 1) The variable definition must take at least two lines
							 * Xint a=
							 *		1234 // 2) indent after the first line 
							 *		;Z // 3) though it ends here, indent anyway.
							 *	
							 * Xint a
							 *		= 1234;Z // 4)
							 */

							if (dv.EndLocation.Line > dv.Location.Line) // 1)
							{
								if (line > dv.Location.Line && // 2)
									line <= dv.EndLocation.Line) // 3)
									i++;

								var x = dv.Initializer;
								if (x != null && line > x.Location.Line && caret < x.EndLocation) // 4)
									i += Calculate(ref x, caret);
							}
						}

				if (n.Parent != null)
				{
					i++;
					n = n.Parent as IBlockNode;
				}
			}

			return i;
		}

		static int GetBlockindent(ISyntaxRegion item, 
			CodeLocation blockStartLocation, 
			ISyntaxRegion lastChild,
			CodeLocation caret)
		{
			var line = caret.Line;

			// X= n.Location
			// Y= n.BlockStartLocation
			// Z= n.Endlocation

			/*
			 * Xclass A(T)
			 *		if(isSomeSpecialType!T) // 1) Indent this line
			 * Y{
			 *		private
			 *		{
			 *			int a; // And indent all items
			 *			int b; } // 2) But also this one
			 * }Z
			 * 
			 * Xclass A(T)
			 * Y{ int a; // 3) Do not indent this one
			 * }Z
			 * 
			 * Xclass A() Y{ // still 3)
			 *		int a; // 4) the usual case
			 * }Z // 5) don't indent the final line, if it's not case 2)
			 */
			if (line > item.Location.Line && line < blockStartLocation.Line) // 1)
				return 1;
			else if (line <= blockStartLocation.Line) // 3)
				return 0;
			else if (lastChild == null ||
				line != item.EndLocation.Line || // 4)
				lastChild.EndLocation.Line == item.EndLocation.Line) // 2)
				return 1;
			else
				return 0; // 5)
		}

		static ISyntaxRegion GetLastBlockAstChild(DBlockNode n)
		{
			ISyntaxRegion lastChild = null;
			ISyntaxRegion t = null;

			if (n.Count != 0)
				lastChild = n.Children[n.Count - 1];
	
			if(n.MetaBlocks.Count != 0)
			{
				t = n.MetaBlocks[n.MetaBlocks.Count - 1];

				if (lastChild == null || t.EndLocation > lastChild.EndLocation)
					lastChild = t;
			}

			if (n.StaticStatements.Count != 0)
			{
				t = n.StaticStatements[n.StaticStatements.Count - 1];

				if (lastChild == null || t.EndLocation > lastChild.EndLocation)
					lastChild = t;
			}

			return lastChild;
		}

		static int Calculate(ref IStatement s, CodeLocation caret)
		{
			int i = 0;

			return i;
		}

		static int Calculate(ref IExpression x, CodeLocation caret)
		{
			int i = 0;

			return i;
		}
	}
}
