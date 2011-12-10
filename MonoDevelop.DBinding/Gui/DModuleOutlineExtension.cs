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
		MonoDevelop.Ide.Gui.Components.PadTreeView outlineTreeView;
		TreeStore outlineTreeStore;
		TreeModelSort outlineTreeModelSort;
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
			if (clickedOnOutlineItem || SyntaxTree==null || outlineTreeStore==null)
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

			outlineTreeStore.Foreach((TreeModel model, TreePath path, TreeIter iter) =>
			{
				var n=model.GetValue(iter, 0);
				if (n == selectedASTNode)
				{
					dontJumpToDeclaration = true;
					outlineTreeView.Selection.SelectIter(iter);
					outlineTreeView.ScrollToCell(path, outlineTreeView.GetColumn(0), true, 0, 0);
					dontJumpToDeclaration = false;

					return true;
				}

				return false;
			});
		}

		void MonoDevelop.DesignerSupport.IOutlinedDocument.ReleaseOutlineWidget()
		{
			if (outlineTreeView == null)
				return;
			var w = (ScrolledWindow)outlineTreeView.Parent;
			w.Destroy();
			outlineTreeModelSort.Dispose();
			outlineTreeModelSort = null;
			outlineTreeStore.Dispose();
			outlineTreeStore = null;
			outlineTreeView = null;
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

			SyntaxTree = (Document.ParsedDocument as ParsedDModule).DDom;

			//limit update rate to 3s
			if (!refreshingOutline)
			{
				refreshingOutline = true;
				refillOutlineStoreId = GLib.Timeout.Add(3000, RefillOutlineStore);
			}
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
			if (outlineTreeStore == null || !outlineTreeView.IsRealized)
			{
				refillOutlineStoreId = 0;
				return false;
			}

			outlineReady = false;
			outlineTreeStore.Clear();
			try
			{
				if (SyntaxTree != null)
				{
					BuildTreeChildren(outlineTreeStore, TreeIter.Zero, SyntaxTree);
					TreeIter it;
					if (outlineTreeStore.GetIterFirst(out it))
						outlineTreeView.Selection.SelectIter(it);
					outlineTreeView.ExpandAll();
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
			if (outlineTreeView != null)
				return outlineTreeView;

			outlineTreeStore = new TreeStore(typeof(object));
			outlineTreeModelSort = new TreeModelSort(outlineTreeStore);
			/*
			settings = ClassOutlineSettings.Load();
			comparer = new ClassOutlineNodeComparer(GetAmbience(), settings, outlineTreeModelSort);

			outlineTreeModelSort.SetSortFunc(0, comparer.CompareNodes);
			outlineTreeModelSort.SetSortColumnId(0, SortType.Ascending);
			*/
			outlineTreeView = new MonoDevelop.Ide.Gui.Components.PadTreeView(outlineTreeStore);

			var pixRenderer = new CellRendererPixbuf();
			pixRenderer.Xpad = 0;
			pixRenderer.Ypad = 0;

			outlineTreeView.TextRenderer.Xpad = 0;
			outlineTreeView.TextRenderer.Ypad = 0;

			TreeViewColumn treeCol = new TreeViewColumn();
			treeCol.PackStart(pixRenderer, false);

			treeCol.SetCellDataFunc(pixRenderer, new TreeCellDataFunc(OutlineTreeIconFunc));
			treeCol.PackStart(outlineTreeView.TextRenderer, true);

			treeCol.SetCellDataFunc(outlineTreeView.TextRenderer, new TreeCellDataFunc(OutlineTreeTextFunc));
			outlineTreeView.AppendColumn(treeCol);

			outlineTreeView.HeadersVisible = false;

			outlineTreeView.Selection.Changed += delegate
			{
				if (dontJumpToDeclaration)
					return;

				clickedOnOutlineItem = true;
				JumpToDeclaration(false);
				clickedOnOutlineItem = false;
			};

			if(Document.ParsedDocument is ParsedDModule)
				SyntaxTree = (Document.ParsedDocument as ParsedDModule).DDom;

			outlineTreeView.Realized += delegate { RefillOutlineStore(); };
			//UpdateSorting();

			var sw = new CompactScrolledWindow();
			sw.Add(outlineTreeView);
			sw.ShowAll();
			return sw;
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
			if (n!=null)
				(cell as CellRendererText).Text = n.Name;
		}

		void JumpToDeclaration(bool focusEditor)
		{
			if (!outlineReady)
				return;
			TreeIter iter;
			if (!outlineTreeView.Selection.GetSelected(out iter))
				return;

			var n = outlineTreeStore.GetValue(iter, 0) as INode;

			if(n==null)
				return;

			IdeApp.Workbench.OpenDocument(SyntaxTree.FileName, n.StartLocation.Line, n.StartLocation.Column);
			if (focusEditor)
				IdeApp.Workbench.ActiveDocument.Select();
		}

		#endregion

		#region Tree building

		static void BuildTreeChildren(TreeStore Tree, TreeIter ParentTreeNode, IBlockNode ParentAstNode)
		{
			if (ParentAstNode == null)
				return;

			foreach (var n in ParentAstNode)
			{
				if (n is DEnum && (n as DEnum).IsAnonymous)
				{
					BuildTreeChildren(Tree, ParentTreeNode, n as IBlockNode);
					continue;
				}

				if (!DCodeCompletionSupport.CanItemBeShownGenerally(n))
					continue;

				TreeIter childIter;
				if (!ParentTreeNode.Equals(TreeIter.Zero))
					childIter = Tree.AppendValues(ParentTreeNode,n);
				else
					childIter = Tree.AppendValues(n);
				
				BuildTreeChildren(Tree, childIter, n as IBlockNode);
			}
		}

		#endregion
	}
}
