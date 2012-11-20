using System;
using System.Linq;
using MonoDevelop.Refactoring.Rename;
using MonoDevelop.Refactoring;
using D_Parser.Resolver;
using System.Collections.Generic;
using MonoDevelop.D.Parser;
using D_Parser.Misc;
using MonoDevelop.D.Building;
using D_Parser.Dom;
using MonoDevelop.Core;
using MonoDevelop.Ide;

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

			var ddoc = doc.ParsedDocument as ParsedDModule;
			if (ddoc == null)	return null;

			var n = options.SelectedItem as INode;
			if (n == null) return null;
			
			var project = doc.HasProject ? doc.Project as DProject : null;

			var parseCache = project != null ?
				project.ParseCache :
				ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);

			var modules = project == null ?
				(IEnumerable<IAbstractSyntaxTree>)new[] { (Ide.IdeApp.Workbench.ActiveDocument.ParsedDocument as ParsedDModule).DDom } :
				project.LocalFileCache;

			var ctxt = ResolutionContext.Create(parseCache, null,null);
			#endregion

			// Enumerate references
			foreach (var mod in modules)
			{
				if (mod == null)
					continue;
				
				var references = D_Parser.Refactoring.ReferencesFinder.Scan(mod, n, ctxt).ToList();

				if (((IAbstractSyntaxTree)n.NodeRoot).FileName == mod.FileName)
					references.Insert(0, new IdentifierDeclaration(n.Name) { Location = n.NameLocation });

				if (references.Count < 1)
					continue;

				var txt = TextFileProvider.Instance.GetEditableTextFile(new FilePath(mod.FileName));
				foreach (ISyntaxRegion reference in references)
				{
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
				if (!D_Parser.Completion.CtrlSpaceCompletionProvider.IsIdentifierChar(c))
					return false;

			// New identifier might be a keyword..
			return !D_Parser.Parser.DTokens.Keywords_Lookup.ContainsKey(id);
		}
	}
}

