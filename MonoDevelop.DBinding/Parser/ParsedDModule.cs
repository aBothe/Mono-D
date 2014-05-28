using D_Parser.Dom;
using D_Parser.Dom.Statements;
using ICSharpCode.NRefactory.TypeSystem;
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

			CreateRefactoringContext += (arg1, arg2) => new DRefactoringContext (arg1, this);
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
						Comment lastComment;

						// Customly foldable code regions
						if (c.Text.Trim().StartsWith("region"))
						{
							bool cont = false;
							for (int j = i + 1; j < Comments.Count; j++)
							{
								lastComment = Comments[j];
								if (lastComment.CommentType == CommentType.SingleLine &&
									lastComment.Text.Trim() == "endregion")
								{
									//TODO: Inhibit fold-processing the endregion comment in other cases.
									var text = c.Text.Trim().Substring(6).Trim();
									if(text == string.Empty)
										text = "//region";
									l.Add(new FoldingRegion(text,new DomRegion(c.Region.BeginLine, c.Region.BeginColumn, lastComment.Region.EndLine, lastComment.Region.EndColumn), FoldType.UserRegion));
									cont = true;
									break;
								}
							}
							if (cont)
								continue;
						}

						lastComment = null;

						
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

		class FoldingVisitor : DefaultDepthFirstVisitor
		{
			readonly List<FoldingRegion> l;

			public FoldingVisitor(List<FoldingRegion> l)
			{
				this.l = l;
			}

			public override void Visit(BlockStatement s)
			{
				l.Add(new FoldingRegion(
								new DomRegion(s.Location.Line, s.Location.Column, s.EndLocation.Line, s.EndLocation.Column),
								s.Parent == null ? FoldType.Member : FoldType.Undefined));

				base.Visit(s);
			}

			public override void Visit(D_Parser.Dom.Expressions.StructInitializer x)
			{
				l.Add(new FoldingRegion(
								new DomRegion(x.Location.Line, x.Location.Column, x.EndLocation.Line, x.EndLocation.Column),
								FoldType.Undefined));

				base.Visit(x);
			}

			public override void Visit(DClassLike n)
			{
				l.Add(new FoldingRegion(GetBlockBodyRegion(n), FoldType.Type));
				base.Visit(n);
			}

			public override void Visit(DEnum n)
			{
				l.Add(new FoldingRegion(GetBlockBodyRegion(n), FoldType.Type));

				base.Visit(n);
			}

			public override void VisitIMetaBlock(IMetaDeclarationBlock mdb)
			{
				l.Add(new FoldingRegion(
								new DomRegion(mdb.BlockStartLocation.Line, mdb.BlockStartLocation.Column, mdb.EndLocation.Line, mdb.EndLocation.Column),
								FoldType.ConditionalDefine));
			}
		}

		void GenerateFoldsInternal(List<FoldingRegion> l,IBlockNode block)
		{
			if (block != null)
				block.Accept(new FoldingVisitor(l));
		}

		public static DomRegion GetBlockBodyRegion(IBlockNode n)
		{
			return new DomRegion(n.BlockStartLocation.Line, n.BlockStartLocation.Column, n.EndLocation.Line, n.EndLocation.Column + 1);
		}
		#endregion
	}

}
