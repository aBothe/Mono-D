using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using Mono.TextEditor;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide.Gui;
namespace MonoDevelop.D.Completion
{
	class AstUpdater
	{
		#region Properties
		public readonly Document Document;
		public readonly TextEditorData Editor;

		public DModule Ast
		{
			get { 
				return Document.GetDAst (); 
			}
		}

		public AstUpdater(Document doc, TextEditorData ed) { Document = doc; Editor = ed; }


		bool hasBegun;

		IStatement currentStmt;
		int currentCol, currentLine;
		IBlockNode currentBlock;
		bool isBeforeBlockStart;
		bool isAtStmtStart;
		#endregion

		public void BeginUpdate()
		{
			if (hasBegun)
				return;

			hasBegun = true;
			currentCol = Document.Editor.Caret.Column;
			currentLine = Document.Editor.Caret.Line;
			var caret = new CodeLocation(currentCol, currentLine);
			currentBlock = DResolver.SearchBlockAt(Ast, caret);
			currentStmt = DResolver.SearchStatementDeeplyAt(currentBlock, caret);

			isBeforeBlockStart = currentBlock != null && caret < currentBlock.BlockStartLocation;
			isAtStmtStart = currentStmt != null && caret == currentStmt.Location;
		}

		public void FinishUpdate()
		{
			if (!hasBegun)
				return;

			hasBegun = false;

			int lineDiff = Editor.Caret.Line - currentLine;
			int colDiff = Editor.Caret.Column - currentCol;

			while (currentBlock != null)
			{
				if (isBeforeBlockStart)
				{
					currentBlock.BlockStartLocation = new CodeLocation(
						currentBlock.BlockStartLocation.Column + (currentBlock.BlockStartLocation.Line == Editor.Caret.Line ? colDiff : 0),
						currentBlock.BlockStartLocation.Line + lineDiff);
					isBeforeBlockStart = false;
				}

				currentBlock.EndLocation = new CodeLocation(
						currentBlock.EndLocation.Column + (currentBlock.EndLocation.Line == Editor.Caret.Line ? colDiff : 0),
						currentBlock.EndLocation.Line + lineDiff);
				currentBlock = currentBlock.Parent as IBlockNode;
			}

			while (currentStmt != null)
			{
				if (isAtStmtStart)
				{
					isAtStmtStart = currentStmt.Parent != null && currentStmt.Location == currentStmt.Parent.Location;
					currentStmt.Location = new CodeLocation(
						currentStmt.Location.Column + colDiff,
						currentStmt.Location.Line + lineDiff);
				}

				currentStmt.EndLocation = new CodeLocation(
						currentStmt.EndLocation.Column + (currentStmt.EndLocation.Line == Editor.Caret.Line ? colDiff : 0),
						currentStmt.EndLocation.Line + lineDiff);
				currentStmt = currentStmt.Parent;
			}
		}
	}
}
