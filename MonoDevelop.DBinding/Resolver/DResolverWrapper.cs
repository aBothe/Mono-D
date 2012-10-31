using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Resolver
{
	public class DResolverWrapper
	{
		public static EditorData GetEditorData(MonoDevelop.Ide.Gui.Document doc = null)
		{
			var ed = new EditorData();

			if (doc == null)
				doc = IdeApp.Workbench.ActiveDocument;

			if (doc == null || 
				doc.FileName == FilePath.Null)
				return null;

			var editor = doc.GetContent<ITextBuffer>();
			if (editor == null)
				return null;

			int line, column;
			editor.GetLineColumnFromPosition(editor.CursorPosition, out line, out column);
			ed.CaretLocation = new CodeLocation(column, line);
			ed.CaretOffset = editor.CursorPosition;

			var ast = doc.ParsedDocument as ParsedDModule;

			var Project = doc.Project as DProject;
			ed.SyntaxTree = ast.DDom as DModule;
			ed.ModuleCode = editor.Text;

			if (ed.SyntaxTree == null)
				return null;

			// Encapsule editor data for resolving
			ed.ParseCache = Project != null ?
				Project.ParseCache :
				ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);

			return ed;
		}

		public static AbstractType[] ResolveHoveredCode(
			out ResolutionContext ResolverContext, 
			MonoDevelop.Ide.Gui.Document doc=null)
		{
			var edData = GetEditorData(doc);

			ResolverContext = ResolutionContext.Create(edData);
			ResolverContext.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			// Resolve the hovered piece of code
			return DResolver.ResolveType(edData, ResolverContext, DResolver.AstReparseOptions.AlsoParseBeyondCaret | DResolver.AstReparseOptions.OnlyAssumeIdentifierList);
		}
	}
}
