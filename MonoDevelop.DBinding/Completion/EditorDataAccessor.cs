using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;

namespace MonoDevelop.D.Completion
{
	public class EditorDataAccessor : IEditorData
	{
		public CodeLocation CaretLocation { get; set; }
		public int CaretOffset { get; set; }
		public IEnumerable<IAbstractSyntaxTree> ImportCache { get; set; }
		public string ModuleCode { get; set; }
		public IEnumerable<IAbstractSyntaxTree> ParseCache { get; set; }
		public DModule SyntaxTree { get; set; }
	}
}
