using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.D.Parser;
using D_Parser.Completion;
using MonoDevelop.D.Building;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace MonoDevelop.D.Resolver
{
	public class DResolverWrapper
	{
		public static ResolveResult[] ResolveHoveredCode(
			out ResolverContext ResolverContext, 
			MonoDevelop.Ide.Gui.Document doc=null)
		{
			ResolverContext = null;

			// Editor property preparations
			if(doc==null)
				doc=IdeApp.Workbench.ActiveDocument;

			if (doc == null || doc.FileName == FilePath.Null || IdeApp.ProjectOperations.CurrentSelectedSolution == null)
				return null;

			var editor = doc.GetContent<ITextBuffer>();
			if (editor == null)
				return null;

			int line, column;
			editor.GetLineColumnFromPosition(editor.CursorPosition, out line, out column);


			var ast = doc.ParsedDocument as ParsedDModule;

			var Project = doc.Project as DProject;
			var SyntaxTree = ast.DDom;

			if (SyntaxTree == null)
				return null;

			// Encapsule editor data for resolving
			var parseCache = Project != null ? Project.ParseCache : DCompiler.Instance.GetDefaultCompiler().GlobalParseCache.ParseCache;
			var edData = new EditorData
			{
				CaretLocation = new CodeLocation(column, line),
				CaretOffset = editor.CursorPosition,
				ModuleCode = editor.Text,
				SyntaxTree = SyntaxTree as DModule,
				ParseCache = parseCache,
				ImportCache = DResolver.ResolveImports(SyntaxTree as DModule, parseCache)
			};

			// Resolve the hovered piece of code
			IStatement stmt = null;
			var results= DResolver.ResolveType(edData,
				ResolverContext = new ResolverContext
				{
					ParseCache = edData.ParseCache,
					ImportCache = edData.ImportCache,
					ScopedBlock = DResolver.SearchBlockAt(SyntaxTree, edData.CaretLocation, out stmt),
					ScopedStatement = stmt
				},
				true, true);

			return results;
		}
	}
}
