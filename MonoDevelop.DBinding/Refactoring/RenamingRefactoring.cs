using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver;
using MonoDevelop.D.Building;
using MonoDevelop.Core;
using Mono.TextEditor;
using Mono.TextEditor.PopupWindow;
using MonoDevelop.Ide;
using D_Parser.Misc;
using D_Parser.Dom.Expressions;

namespace MonoDevelop.D.Refactoring
{
	public class RenamingRefactoring
	{
		INode n;
		Dictionary<string, List<CodeLocation>> foundReferences;

		public static bool CanRename(INode n)
		{
			return n!=null && !(n is IAbstractSyntaxTree) && !string.IsNullOrEmpty(n.Name);
		}

		class DescIntComparer : Comparer<int>
		{
			public override int Compare(int x, int y)
			{
				return x >= y ? 0 : 1;
			}
		}

		public bool Run(DProject project,INode targetMember, string newName=null)
		{
			if(!CanRename(targetMember) || Ide.IdeApp.Workbench.ActiveDocument ==null)
				return false;

			n = targetMember;

			// Request new name
			if (newName == null)
				newName = MessageService.GetTextResponse("Enter a new name", "Symbol rename", n.Name);

			if (newName == null || newName==n.Name)
				return false;

			// Validate new name
			if (string.IsNullOrWhiteSpace(newName))
			{
				MessageService.ShowError("Symbol name must not be empty!");
				return false;
			}

			foreach (var c in newName)
				if (!D_Parser.Completion.CtrlSpaceCompletionProvider.IsIdentifierChar(c))
				{
					MessageService.ShowError("Character '" + c + "' in " + newName + " not allowed as identifier character!");
					return false;
				}






			// Setup locals
			var parseCache = project != null ? 
				project.ParseCache :
				ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);

			var modules = project == null ?
				(IEnumerable<IAbstractSyntaxTree>) new[] { (Ide.IdeApp.Workbench.ActiveDocument.ParsedDocument as MonoDevelop.D.Parser.ParsedDModule).DDom } :
				project.LocalFileCache;

			foundReferences = new Dictionary<string, List<CodeLocation>>();






			var ctxt = new ResolverContextStack(parseCache, new ResolverContext());

			// Enumerate references
			foreach (var mod in modules)
			{
				if (mod == null)
					continue;

				var references = D_Parser.Refactoring.ReferencesFinder.Scan(mod, n, ctxt).ToList();

				if ((n.NodeRoot as IAbstractSyntaxTree).FileName == mod.FileName)
					references.Insert(0, new IdentifierDeclaration(n.Name) { Location = n.NameLocation });

				if (references.Count < 1)
					continue;

				references.Sort(new ReferenceFinding.IdLocationComparer(true));

				if (!foundReferences.ContainsKey(mod.FileName))
					foundReferences.Add(mod.FileName, new List<CodeLocation>());

				var moduleRefList = foundReferences[mod.FileName];
				foreach (var reference in references)
				{
					moduleRefList.Add(reference.Location);
				}
			}

			if (foundReferences.Count < 1)
				return false;










			// Replace occurences
			foreach (var kv1 in foundReferences)
			{
				var file = new FilePath(kv1.Key);
				bool isOpen = false;
				
				var tfd = TextFileProvider.Instance.GetTextEditorData(file, out isOpen);
				var doc = TextFileProvider.Instance.GetEditableTextFile(file);

				if (doc != null)
				{
					var offsets = new List<int>(kv1.Value.Count);

					foreach (var kv2 in kv1.Value)
						offsets.Add(doc.GetPositionFromLineColumn(kv2.Line, kv2.Column));

					/*
					 * Important: The names have to be replaced from the last to the first identifier offset
					 * because only this ensures the consistency of the actual offsets.
					 * Replacing from the first to the last occurrency would obstruct huge parts of the document.
					 */
					offsets.Sort(new DescIntComparer());

					IDisposable undoGrp = null;
					if(isOpen)
						undoGrp = tfd.OpenUndoGroup(); // Put all replacement into one huge undo group -- it'll be much more easier to rewind done changes
					foreach(var offset in offsets)
					{
						if(isOpen)
							tfd.Replace(offset, n.Name.Length, newName);
						else
						{
							doc.DeleteText(offset, n.Name.Length);
							doc.InsertText(offset, newName);
						}
					}
					if (undoGrp != null)
						undoGrp.Dispose();
					
					// If project file not open for editing, reparse it
					if (project != null && 
						!IdeApp.Workbench.Documents.Any((Ide.Gui.Document d) => 
						{ 
							if(d.IsFile && d.FileName == kv1.Key)
							{
								// Important: Save file contents to ensure updated AST contents
								d.Save();
								return true;
							}				
							return false;
						}))
						project.ReparseModule(kv1.Key);
				}
			}

			// Assign new name to the node
			n.Name = newName;

			return true;
		}
	}
}
