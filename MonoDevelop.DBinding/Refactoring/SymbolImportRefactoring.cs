using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Misc;
using MonoDevelop.Ide;
using MonoDevelop.D.Building;
using MonoDevelop.D.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Completion;

namespace MonoDevelop.D.Refactoring
{
	public class SymbolImportRefactoring
	{
		public static void CreateImportStatementForCurrentCodeContext()
		{
			/*
			 * 1) Get currently selected symbol
			 * 2) Enum through all parse caches to find first occurrence of this symbol
			 * 3) Find the current module's first import statement (or take a location after the module statement / take (0,0) as the statement location)
			 * 4) Create import statement
			 */

			// 1)
			var doc = IdeApp.Workbench.ActiveDocument;
			var edData = DResolverWrapper.GetEditorData(doc);

			var sr = new SymbolImportRefactoring();

			var name = SymbolImportRefactoring.GetSelectedSymbolRoot(edData);

			if (string.IsNullOrEmpty(name))
			{
				MessageService.ShowError("No text symbol selected.");
				return;
			}

			// 2)
			var possibleNodes=SearchInCache(edData.ParseCache, name).ToList();

			if (possibleNodes.Count == 0)
			{
				MessageService.ShowError("Symbol could not be found in global module range.");
				return;
			}

			//TODO: Choice dialog

			var chosenNode = possibleNodes[0];

			if (chosenNode == null)
				return;

			var chosenImportModule = chosenNode.NodeRoot as IAbstractSyntaxTree;

			if (chosenImportModule == null)
				return;

			// 3)
			var insertLocation=CodeLocation.Empty; // At this location, a line break + the new import statement will be inserted

			foreach (var stmt in edData.SyntaxTree.StaticStatements)
				if (stmt is ImportStatement)
					insertLocation = stmt.EndLocation;

			if (insertLocation == CodeLocation.Empty && edData.SyntaxTree.OptionalModuleStatement != null)
				insertLocation = edData.SyntaxTree.OptionalModuleStatement.EndLocation;

			// 4)
			var importCode = "import " + chosenImportModule.ModuleName + ";\n";

			doc.Editor.Insert(doc.Editor.GetLine(insertLocation.Line).EndOffset,importCode);
		}

		static string GetSelectedSymbolRoot(IEditorData edData)
		{
			var o = DResolver.GetScopedCodeObject(edData,null, DResolver.AstReparseOptions.AlsoParseBeyondCaret);

			if (o is ITypeDeclaration)
			{
				var rootType = ((ITypeDeclaration)o).InnerMost;

				if (rootType is IdentifierDeclaration)
					return ((IdentifierDeclaration)rootType).Id;
				else if (rootType is TemplateInstanceExpression)
					return ((TemplateInstanceExpression)rootType).TemplateIdentifier.Id;
			}
			else if (o is IExpression)
			{
				var curEx = (IExpression)o;

				while (curEx is PostfixExpression)
					curEx = ((PostfixExpression)curEx).PostfixForeExpression;

				if (curEx is IdentifierExpression)
					return ((IdentifierExpression)curEx).Value as string;
				else if (curEx is TemplateInstanceExpression)
					return ((TemplateInstanceExpression)curEx).TemplateIdentifier.Id;
			}
			return null;
		}

		static IEnumerable<INode> SearchInCache(ParseCacheList parseCache, string name)
		{
			foreach (var pc in parseCache)
			{
				foreach (IAbstractSyntaxTree mod in pc)
				{
					foreach (var n in mod)
						if (n != null && n.Name == name && n is DNode &&
							((DNode)n).IsPublic)
							yield return n;
				}
			}
		}
	}
}
