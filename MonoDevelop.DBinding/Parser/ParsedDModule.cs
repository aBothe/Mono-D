using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.Ide.TypeSystem;
using D_Parser.Resolver;

namespace MonoDevelop.D.Parser
{
	public class ParsedDModule : ParsedDocument
	{
		public ParsedDModule(string fileName) : base(fileName) { }

		IAbstractSyntaxTree _ddom;
		public IAbstractSyntaxTree DDom {
			get { 
				var sln=Ide.IdeApp.ProjectOperations.CurrentSelectedSolution;
				if(sln!=null)
					foreach (var prj in sln.GetAllProjects())
						if (prj is DProject && prj.IsFileInProject(FileName))
						{
							return ((DProject)prj).LocalFileCache.GetModuleByFileName(FileName, prj.BaseDirectory);
						}
				
				return _ddom;
			}
			set
			{
				var sln = Ide.IdeApp.ProjectOperations.CurrentSelectedSolution;
				if (sln != null)
					foreach(var prj in sln.GetAllProjects())
						if (prj is DProject && prj.IsFileInProject(FileName))
						{
							var dprj = (DProject)prj;

							var oldAst = DDom;
							if (oldAst != null)
							{
								dprj.LocalFileCache.UfcsCache.RemoveModuleItems(oldAst);
								dprj.LocalFileCache.Remove(oldAst);
								oldAst = null;
							}

							if (value != null)
							{
								dprj.LocalFileCache.AddOrUpdate(value);//TODO: Make it apply the current global version/debug conditions
								dprj.LocalFileCache.UfcsCache.CacheModuleMethods(value, ResolutionContext.Create(dprj.ParseCache, null, null));
							}
						}
				
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
				var memberRegions = new List<DomRegion>();
				foreach (var i in l)
					if (i.Type == FoldType.Member)
						memberRegions.Add(i.Region);

				// Add multiline comment folds
				foreach (var c in Comments)
				{
					if (c.CommentType == CommentType.SingleLine)
						continue;

					bool IsMemberComment = false;

					foreach (var i in memberRegions)
						if (i.IsInside(c.Region.Begin))
						{
							IsMemberComment = true;
							break;
						}

					l.Add(new FoldingRegion(c.Region, IsMemberComment ? FoldType.CommentInsideMember : FoldType.Comment));
				}

				return l;
			}
		}

		void GenerateFoldsInternal(List<FoldingRegion> l,IBlockNode block)
		{
			if (block == null)
				return;

			if (!(block is IAbstractSyntaxTree) && !block.Location.IsEmpty && block.EndLocation > block.Location)
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
