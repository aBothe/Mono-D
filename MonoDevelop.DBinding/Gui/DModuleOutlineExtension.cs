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
		IAbstractSyntaxTree SyntaxTree = null;
		MonoDevelop.Ide.Gui.Components.PadTreeView outlineTreeView;
		TreeStore outlineTreeStore;
		TreeModelSort outlineTreeModelSort;
		Widget[] toolbarWidgets;

		bool refreshingOutline;
		bool disposed;
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
				Document.DocumentParsed += UpdateDocumentOutline;
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
			if (SyntaxTree!=null)
			{
				BuildTreeChildren(outlineTreeStore, TreeIter.Zero, SyntaxTree);
				TreeIter it;
				if (outlineTreeStore.GetIterFirst(out it))
					outlineTreeView.Selection.SelectIter(it);
				outlineTreeView.ExpandAll();
			}
			outlineReady = true;

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
				JumpToDeclaration(false);
			};

			outlineTreeView.RowActivated += delegate
			{
				JumpToDeclaration(true);
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
				if(!icon.Equals(null))
					pixRenderer.Pixbuf = ImageService.GetPixbuf(icon.Name, IconSize.Menu);
			}
			else if (o is D_Parser.Dom.Statements.StatementContainingStatement)
			{
				pixRenderer.Pixbuf = ImageService.GetPixbuf("gtk-add", IconSize.Menu);
			}
		}

		void OutlineTreeTextFunc(TreeViewColumn column, CellRenderer cell, TreeModel model, TreeIter iter)
		{
			CellRendererText txtRenderer = (CellRendererText)cell;
			object o = model.GetValue(iter, 0);
			if (o is INode)
				txtRenderer.Text = (o as INode).Name;
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
				if (!DCodeCompletionSupport.CanItemBeShownGenerally(n as DNode))
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
