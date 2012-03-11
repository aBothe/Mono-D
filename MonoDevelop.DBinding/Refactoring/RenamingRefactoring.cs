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

		public bool Run(DProject project,INode targetMember, string newName=null)
		{
			if(targetMember==null || Ide.IdeApp.Workbench.ActiveDocument ==null)
				return false;

			n = targetMember;



			// Request new name
			if (newName == null)
				newName = MessageService.GetTextResponse("Enter a new name", "Symbol rename", n.Name);

			if (newName == null || newName==n.Name)
				return false;

			// Validate new name
			if (newName == "")
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








			// Enumerate references
			foreach (var mod in modules)
			{
				if (mod == null)
					continue;

				var references = DReferenceFinder.ScanNodeReferencesInModule(mod, parseCache,n);

				if ((n.NodeRoot as IAbstractSyntaxTree).FileName == mod.FileName)
					references.Insert(0, new IdentifierDeclaration(n.Name) { Location = n.NameLocation });

				if (references.Count < 1)
					continue;

				references.Sort(new DReferenceFinder.IdLocationComparer(true));

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
				var doc = TextFileProvider.Instance.GetEditableTextFile(new FilePath(kv1.Key));

				if (doc != null)
				{
					foreach (var kv2 in kv1.Value)
					{
						int offset = doc.GetPositionFromLineColumn(kv2.Line, kv2.Column);

						doc.DeleteText(offset, n.Name.Length);
						doc.InsertText(offset, newName);
					}

					// If project file not open for editing, reparse it
					if (project != null && !IdeApp.Workbench.Documents.Any((Ide.Gui.Document d) => {
						if (d.IsFile && d.FileName == kv1.Key)
							return true;
						return false;
					}))
						project.ReparseModule(kv1.Key);
				}
			}

			// Assign new name to the node
			n.Name = newName;






			/*
			// Prepare current editor (setup textlinks and anchors)
			var doc = Ide.IdeApp.Workbench.ActiveDocument;

			if (doc == null || !doc.IsFile || !foundReferences.ContainsKey(doc.FileName))
				return false;

			var editor = doc.Editor;
			var localReferences = foundReferences[doc.FileName];

			List<TextLink> links = new List<TextLink>();
			TextLink link = new TextLink("name");
			int baseOffset = Int32.MaxValue;


			foreach (var r in localReferences)
			{
				baseOffset = Math.Min(baseOffset, editor.Document.LocationToOffset(r.Line, r.Column));
			}
			foreach (var r in localReferences)
			{
				var segment = new Segment(editor.Document.LocationToOffset(r.Line, r.Column) - baseOffset, n.Name.Length);
				if (segment.Offset <= editor.Caret.Offset - baseOffset && editor.Caret.Offset - baseOffset <= segment.EndOffset)
				{
					link.Links.Insert(0, segment);
				}
				else
				{
					link.AddLink(segment);
				}
			}

			links.Add(link);
			if (editor.CurrentMode is TextLinkEditMode)
				((TextLinkEditMode)editor.CurrentMode).ExitTextLinkMode();
			var tle = new TextLinkEditMode(editor.Parent, baseOffset, links);
			tle.SetCaretPosition = false;
			tle.SelectPrimaryLink = true;
			
			// Show rename helper popup
			if (tle.ShouldStartTextLinkMode)
			{
				var helpWindow = new ModeHelpWindow();
				helpWindow.TransientFor = IdeApp.Workbench.RootWindow;
				helpWindow.TitleText = "<b>Renaming " + (n as AbstractNode).ToString(false) + "</b>";
				helpWindow.Items.Add(new KeyValuePair<string, string>(GettextCatalog.GetString("<b>Key</b>"), GettextCatalog.GetString("<b>Behavior</b>")));
				helpWindow.Items.Add(new KeyValuePair<string, string>(GettextCatalog.GetString("<b>Return</b>"), GettextCatalog.GetString("<b>Accept</b> this refactoring.")));
				helpWindow.Items.Add(new KeyValuePair<string, string>(GettextCatalog.GetString("<b>Esc</b>"), GettextCatalog.GetString("<b>Cancel</b> this refactoring.")));
				tle.HelpWindow = helpWindow;
				tle.Cancel += delegate
				{
					if (tle.HasChangedText)
						editor.Document.Undo();
				};
				helpWindow.Destroyed += (object o, EventArgs e) =>
				{
					if (tle.HasChangedText)
					{

					}
				};
				tle.OldMode = editor.CurrentMode;
				tle.StartMode();
				editor.CurrentMode = tle;
			}
			else
				return false;
			*/

			return true;
		}
	}
}
