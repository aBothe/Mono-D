using System;
using System.Linq;
using MonoDevelop.Refactoring.Rename;
using MonoDevelop.Refactoring;
using D_Parser.Resolver;
using System.Collections.Generic;
using MonoDevelop.D.Parser;
using D_Parser.Misc;
using D_Parser.Dom;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.D.Projects;
using MonoDevelop.D.Resolver;

namespace MonoDevelop.D.Refactoring
{
	public class DRenameRefactoring : RenameRefactoring
	{
		public override bool IsValid (RefactoringOptions options)
		{
			if (options == null)
				return false;

			var n = options.SelectedItem as INode;
			//TODO: Any further node types that cannot be renamed?
			return n != null && CanRenameNode(n);
		}

		public static bool CanRenameNode(INode n)
		{
			return !(n is DMethod) || ((DMethod)n).SpecialType == DMethod.MethodType.Normal;
		}

		public override List<Change> PerformChanges (RefactoringOptions options, object prop)
		{
			#region Init
			var renameProperties = prop as RenameProperties;
			if (renameProperties == null) return null;

			var changes = new List<Change>();

			var doc = options.Document;
			if (doc == null)	return null;

			var ast = doc.GetDAst();
			if (ast == null)	return null;

			var n = options.SelectedItem as INode;
			if (n == null) return null;
			
			var project = doc.HasProject ? doc.Project as AbstractDProject : null;

			var parseCache = DResolverWrapper.CreateParseCacheView(project);

			var modules = new List<DModule>();
			if(project == null)
				modules.Add(ast);
			else
				foreach(var p in project.GetSourcePaths())
					modules.AddRange(GlobalParseCache.EnumModulesRecursively(p));

			var ctxt = ResolutionContext.Create(parseCache, null,null);
			#endregion

			// Enumerate references
			foreach (var mod in modules)
			{
				if (mod == null)
					continue;
				
				var references = D_Parser.Refactoring.ReferencesFinder.SearchModuleForASTNodeReferences(mod, n, ctxt).ToList();

				if (((DModule)n.NodeRoot).FileName == mod.FileName)
					references.Insert(0, new IdentifierDeclaration(n.NameHash) { Location = n.NameLocation });

				if (references.Count < 1)
					continue;

				var txt = TextFileProvider.Instance.GetEditableTextFile(new FilePath(mod.FileName));
				var prevReplacement = CodeLocation.Empty;
				foreach (ISyntaxRegion reference in references)
				{
					if (prevReplacement == reference.Location)
						continue;
					
					prevReplacement = reference.Location;
					changes.Add(new TextReplaceChange { 
						FileName = mod.FileName,
						InsertedText = renameProperties.NewName,
						RemovedChars = n.Name.Length,
						Description = string.Format (GettextCatalog.GetString ("Replace '{0}' with '{1}'"), n.Name, renameProperties.NewName),
						Offset = txt.GetPositionFromLineColumn(reference.Location.Line, reference.Location.Column)
					});
				}
			}

			return changes;
		}

		public override void Run (RefactoringOptions options)
		{
			MessageService.ShowCustomDialog(new DRenameNameDialog(options, this));
		}

		public static bool IsValidIdentifier(string id)
		{
			// Prohibit empty identifiers
			if (string.IsNullOrWhiteSpace(id))
				return false;

			// All id chars must be identifier chars
			foreach (var c in id)
				if (!D_Parser.Parser.Lexer.IsIdentifierPart(c))
					return false;

			// New identifier might be a keyword..
			return !D_Parser.Parser.DTokens.Keywords_Lookup.ContainsKey(id);
		}
	}
}

