using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.DesignerSupport;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.Gui;
using D_Parser.Dom;
using MonoDevelop.D.Parser;
using Gtk;
using MonoDevelop.Ide;
using MonoDevelop.Components;
using MonoDevelop.D.Completion;
using MonoDevelop.Core;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using MonoDevelop.D.Refactoring;

namespace MonoDevelop.D.Gui
{
	/// <summary>
	/// 'Overrides' MonoDevelop's own document outline.
	/// In the class itself, a tree widget is generated.
	/// The active document's D Syntax Tree will be scanned, whereas all its child items become added to the tree.
	/// </summary>
	public class DModuleOutlineExtension : TextEditorExtension, IOutlinedDocument
	{
		#region Properties
		IAbstractSyntaxTree SyntaxTree;
		MonoDevelop.Ide.Gui.Components.PadTreeView TreeView;
		TreeStore TreeStore;
		Widget[] toolbarWidgets;

		bool refreshingOutline;
		bool clickedOnOutlineItem;
		bool dontJumpToDeclaration;
		bool outlineReady;
		#endregion

		#region ctor & dtor stuff

		public override bool ExtendsEditor(Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}

		public override void Initialize()
		{
			base.Initialize();
			if (Document != null)
			{
				Document.DocumentParsed += UpdateDocumentOutline;
				Document.Editor.Caret.PositionChanged += UpdateOutlineSelection;
			}
		}

		void UpdateOutlineSelection(object sender, Mono.TextEditor.DocumentLocationEventArgs e)
		{
			if (clickedOnOutlineItem || SyntaxTree==null || TreeStore==null)
				return;

			IStatement stmt = null;
			var caretLocation=Document.Editor.Caret.Location;
			var caretLocationD=new CodeLocation(caretLocation.Column, caretLocation.Line);

			var currentblock = DResolver.SearchBlockAt(SyntaxTree, caretLocationD, out stmt);

			INode selectedASTNode = null;

			if (currentblock == null)
				return;

			foreach (var n in currentblock)
				if (caretLocationD >= n.StartLocation && caretLocationD <= n.EndLocation)
				{
					selectedASTNode = n;
					break;
				}

			if(selectedASTNode==null)
				selectedASTNode = stmt != null ? stmt.ParentNode : currentblock;

			if (selectedASTNode == null)
				return;

			TreeStore.Foreach((TreeModel model, TreePath path, TreeIter iter) =>
			{
				var n=model.GetValue(iter, 0);
				if (n == selectedASTNode)
				{
					dontJumpToDeclaration = true;
					TreeView.Selection.SelectIter(iter);
					TreeView.ScrollToCell(path, TreeView.GetColumn(0), true, 0, 0);
					dontJumpToDeclaration = false;

					return true;
				}

				return false;
			});
		}

		void MonoDevelop.DesignerSupport.IOutlinedDocument.ReleaseOutlineWidget()
		{
			if (TreeView == null)
				return;
			var w = (ScrolledWindow)TreeView.Parent;
			w.Destroy();

			TreeStore.Dispose();
			TreeStore = null;
			TreeView = null;
			//settings = null;
			if(toolbarWidgets!=null)
				foreach (var tw in toolbarWidgets)
					w.Destroy();
			toolbarWidgets = null;
			//comparer = null;
		}

		uint refillOutlineStoreId;
		void UpdateDocumentOutline(object sender, EventArgs args)
		{
			if (!(Document.ParsedDocument is ParsedDModule))
				return;

			RefillOutlineStore();
		}

		void RemoveRefillOutlineStoreTimeout()
		{
			if (refillOutlineStoreId == 0)
				return;
			GLib.Source.Remove(refillOutlineStoreId);
			refillOutlineStoreId = 0;
		}

		bool RefillOutlineStore()
		{
			DispatchService.AssertGuiThread();
			Gdk.Threads.Enter();

			refreshingOutline = false;
			if (TreeStore == null || !TreeView.IsRealized)
			{
				refillOutlineStoreId = 0;
				return false;
			}

			outlineReady = false;
			TreeStore.Clear();
			try
			{
				if (Document.ParsedDocument is ParsedDModule)
				{
					SyntaxTree = (Document.ParsedDocument as ParsedDModule).DDom;

					if (SyntaxTree != null)
					{
						var caretLocation = Document.Editor.Caret.Location;
						BuildTreeChildren(TreeIter.Zero, SyntaxTree, new CodeLocation(caretLocation.Column, caretLocation.Line));

						TreeView.ExpandAll();
					}
				}
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error while updating document outline panel", ex);
			}
			finally
			{
				outlineReady = true;
			}
			Gdk.Threads.Leave();

			//stop timeout handler
			refillOutlineStoreId = 0;
			return false;
		}

		#endregion

		#region GUI low level
		public Gtk.Widget GetOutlineWidget()
		{
			if (TreeView != null)
				return TreeView;

			TreeStore = new TreeStore(typeof(object));
			/*
			settings = ClassOutlineSettings.Load();
			comparer = new ClassOutlineNodeComparer(GetAmbience(), settings, outlineTreeModelSort);

			outlineTreeModelSort.SetSortFunc(0, comparer.CompareNodes);
			outlineTreeModelSort.SetSortColumnId(0, SortType.Ascending);
			*/
			TreeView = new MonoDevelop.Ide.Gui.Components.PadTreeView(TreeStore);

			var pixRenderer = new CellRendererPixbuf();
			pixRenderer.Xpad = 0;
			pixRenderer.Ypad = 0;

			TreeView.TextRenderer.Xpad = 0;
			TreeView.TextRenderer.Ypad = 0;

			TreeViewColumn treeCol = new TreeViewColumn();
			treeCol.PackStart(pixRenderer, false);
			
			treeCol.SetCellDataFunc(pixRenderer, new TreeCellDataFunc(OutlineTreeIconFunc));
			treeCol.PackStart(TreeView.TextRenderer, true);

			treeCol.SetCellDataFunc(TreeView.TextRenderer, new TreeCellDataFunc(OutlineTreeTextFunc));

			TreeView.AppendColumn(treeCol);

			TreeView.TextRenderer.Editable = true;
			TreeView.TextRenderer.Edited += new EditedHandler(nameCell_Edited);
			
			TreeView.HeadersVisible = false;

			TreeView.Selection.Changed += delegate
			{
				if (dontJumpToDeclaration || !outlineReady)
					return;

				clickedOnOutlineItem = true;
				JumpToDeclaration(true);
				clickedOnOutlineItem = false;
			};

			TreeView.Realized += delegate { RefillOutlineStore(); };
			//UpdateSorting();

			var sw = new CompactScrolledWindow();
			sw.Add(TreeView);
			sw.ShowAll();
			return sw;
		}

		void nameCell_Edited(object o, EditedArgs args)
		{
			TreeIter iter;
			TreeStore.GetIter(out iter, new Gtk.TreePath(args.Path));

			var n=TreeStore.GetValue(iter, 0) as INode;

			if (n != null && args.NewText!=n.Name)
			{
				new RenamingRefactoring().Run(IdeApp.Workbench.ActiveDocument.HasProject ?
					IdeApp.Workbench.ActiveDocument.Project as DProject : null, n, args.NewText);

				TreeView.Selection.SelectIter(iter);
				TreeView.GrabFocus();
			}
		}

		public IEnumerable<Gtk.Widget> GetToolbarWidgets()
		{
			return null;
		}

		void OutlineTreeIconFunc(TreeViewColumn column, CellRenderer cell, TreeModel model, TreeIter iter)
		{
			var pixRenderer = (CellRendererPixbuf)cell;
			object o = model.GetValue(iter, 0);
			if (o is DNode)
			{
				var icon=DCompletionData.GetNodeIcon(o as DNode);
				if(icon!=(Core.IconId)null)
					pixRenderer.Pixbuf = ImageService.GetPixbuf(icon.Name, IconSize.Menu);
			}
			else if (o is D_Parser.Dom.Statements.StatementContainingStatement)
			{
				pixRenderer.Pixbuf = ImageService.GetPixbuf("gtk-add", IconSize.Menu);
			}
		}

		void OutlineTreeTextFunc(TreeViewColumn column, CellRenderer cell, TreeModel model, TreeIter iter)
		{
			var n = model.GetValue(iter, 0) as INode;

			string label=null;

			if (n is DMethod && (n as DMethod).SpecialType == DMethod.MethodType.Unittest)
				label = "(Unit test)";
			else if (n != null)
				label = n.Name;

			(cell as CellRendererText).Text = label;
		}

		void JumpToDeclaration(bool focusEditor)
		{
			if (!outlineReady)
				return;

			TreeIter iter;
			if (!TreeView.Selection.GetSelected(out iter))
				return;

			var n = TreeStore.GetValue(iter, 0) as INode;

			if(n==null)
				return;

			var openedDoc=IdeApp.Workbench.GetDocument(SyntaxTree.FileName);

			if (openedDoc == null)
				return;

			openedDoc.Editor.SetCaretTo(n.StartLocation.Line, n.StartLocation.Column);
			openedDoc.Editor.ScrollToCaret();

			if (focusEditor)
			{
				IdeApp.Workbench.ActiveDocument.Select();
			}

			openedDoc.Editor.Document.EnsureOffsetIsUnfolded(
				openedDoc.Editor.LocationToOffset(
					n.StartLocation.Line,
					n.StartLocation.Column
			));
		}

		#endregion

		#region Tree building

		void BuildTreeChildren(TreeIter ParentTreeNode, IBlockNode ParentAstNode, CodeLocation editorSelectionLocation)
		{
			if (ParentAstNode == null)
				return;

			if (ParentAstNode is DMethod)
			{
				var dm=ParentAstNode as DMethod;

				if (dm.Parameters != null)
					foreach (var p in dm.Parameters)
						if(p.Name!="")
						{
							TreeIter childIter;
							if (!ParentTreeNode.Equals(TreeIter.Zero))
								childIter = TreeStore.AppendValues(ParentTreeNode, p);
							else
								childIter = TreeStore.AppendValues(p);

							if (editorSelectionLocation >= p.StartLocation && 
								editorSelectionLocation < p.EndLocation)
								TreeView.Selection.SelectIter(childIter);
						}
			}

			foreach (var n in ParentAstNode)
			{
				if (n is DEnum && (n as DEnum).IsAnonymous)
				{
					BuildTreeChildren(ParentTreeNode, n as IBlockNode,editorSelectionLocation);
					continue;
				}

				TreeIter childIter;
				if (!ParentTreeNode.Equals(TreeIter.Zero))
					childIter = TreeStore.AppendValues(ParentTreeNode,n);
				else
					childIter = TreeStore.AppendValues(n);

				if (editorSelectionLocation >= n.StartLocation && 
					editorSelectionLocation < n.EndLocation)
					TreeView.Selection.SelectIter(childIter);
				
				BuildTreeChildren(childIter, n as IBlockNode,editorSelectionLocation);
			}
		}

		#endregion
	}
}
