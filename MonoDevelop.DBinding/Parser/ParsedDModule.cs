using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.D.Projects;
using MonoDevelop.Ide.TypeSystem;
using System.Collections.Generic;
using D_Parser.Misc;
using MonoDevelop.D.Refactoring;

namespace MonoDevelop.D.Parser
{
	public class ParsedDModule : ParsedDocument
	{
		public ParsedDModule(string fileName) : base(fileName) { 
			Flags = ParsedDocumentFlags.NonSerializable; 
		
			CreateRefactoringContext += (MonoDevelop.Ide.Gui.Document arg1, System.Threading.CancellationToken arg2) => new DRefactoringContext (arg1);
		}

		DModule _ddom;
		public DModule DDom {
			get { 
				return GlobalParseCache.GetModule (FileName) ?? _ddom;
			}
			set
			{
				GlobalParseCache.AddOrUpdateModule (value);
				_ddom=value;
			}
		}

		public List<Error> ErrorList = new List<Error>();
		public override IList<Error> Errors
		{
			get { return ErrorList; }
		}

		#region Folding management
		public override IEnumerable<FoldingRegion> Foldings
		{
			get
			{
				var l = new List<FoldingRegion>();

				// Add primary node folds
				GenerateFoldsInternal(l, DDom);

				// Get member block regions
				var memberRegions = new List<FoldingRegion>();
				for (int i = l.Count-1; i >= 0; i--)
					if (l[i].Type == FoldType.Member)
						memberRegions.Add(l[i]);
				
				// Add multiline comment folds
				for(int i = 0; i < Comments.Count; i++)
				{
					var c = Comments[i];

					bool IsMemberComment = false;
					for (int k = memberRegions.Count-1; k >= 0; k--)
						if (memberRegions[k].Region.IsInside(c.Region.Begin))
						{
							IsMemberComment = true;
							break;
						}

					if (c.CommentType == CommentType.SingleLine)
					{
						int nextIndex = i + 1;
						Comment lastComment = null;
						for (int j=i+1; j < Comments.Count; j++)
						{
							lastComment = Comments[j];
							if (lastComment.CommentType != c.CommentType || 
								lastComment.Region.BeginColumn != c.Region.BeginColumn ||
								lastComment.Region.BeginLine != Comments[j-1].Region.BeginLine + 1)
							{
								lastComment = j == nextIndex ? null : Comments[j - 1];
								break;
							}
							i++;
						}

						if (lastComment == null)
							continue;

						l.Add(new FoldingRegion(new DomRegion(c.Region.BeginLine, c.Region.BeginColumn, lastComment.Region.BeginLine, lastComment.Region.EndColumn+1), 
							IsMemberComment ? FoldType.CommentInsideMember : FoldType.Comment));
					}
					else
					l.Add(new FoldingRegion(c.Region, IsMemberComment ? FoldType.CommentInsideMember : FoldType.Comment));
				}

				return l;
			}
		}

		void GenerateFoldsInternal(List<FoldingRegion> l,IBlockNode block)
		{
			if (block == null)
				return;

			if (!(block is DModule) && !block.Location.IsEmpty && block.EndLocation > block.Location)
			{
				if (block is DMethod)
				{
					var dm = block as DMethod;

					if (dm.In != null)
						GenerateFoldsInternal(l, dm.In);
					if (dm.Out != null)
						GenerateFoldsInternal(l, dm.Out);
					if (dm.Body != null)
						GenerateFoldsInternal(l, dm.Body);
				}
				else
					l.Add(new FoldingRegion(GetBlockBodyRegion(block),FoldType.Type));
			}

			if (block.Count > 0)
				foreach (var n in block)
					GenerateFoldsInternal(l,n as IBlockNode);

			if (block is DBlockNode)
			{
				var dbn = block as DBlockNode;
				if (dbn.MetaBlocks != null)
				{
					for (int i = dbn.MetaBlocks.Count - 1; i >= 0; i--)
					{
						var mdb = dbn.MetaBlocks[i] as IMetaDeclarationBlock;
						if (mdb != null)
						{
							l.Add(new FoldingRegion(
								new DomRegion(mdb.BlockStartLocation.Line, mdb.BlockStartLocation.Column, mdb.EndLocation.Line, mdb.EndLocation.Column),
								FoldType.Undefined));
						}
					}
				}
			}
		}

		public static DomRegion GetBlockBodyRegion(IBlockNode n)
		{
			return new DomRegion(n.BlockStartLocation.Line, n.BlockStartLocation.Column, n.EndLocation.Line, n.EndLocation.Column + 1);
		}

		void GenerateFoldsInternal(List<FoldingRegion> l, StatementContainingStatement statement)
		{
			// Only let block statements (like { SomeStatement(); SomeOtherStatement++; }) be foldable
			if(statement is BlockStatement)
				l.Add(new FoldingRegion(
					new DomRegion(
						statement.Location.Line,
						statement.Location.Column,
						statement.EndLocation.Line,
						statement.EndLocation.Column+1),
					FoldType.Undefined));

			// Do a deep-scan
			foreach (var s in statement.SubStatements)
				if (s is StatementContainingStatement)
					GenerateFoldsInternal(l, s as StatementContainingStatement);
		}
		#endregion
	}

}
