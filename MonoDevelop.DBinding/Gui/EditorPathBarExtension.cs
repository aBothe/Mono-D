using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Components;
using D_Parser.Dom;
using MonoDevelop.D.Completion;
using MonoDevelop.Ide;
using MonoDevelop.Core;
using Gtk;
using D_Parser.Dom.Statements;
using MonoDevelop.D.Parser;
using D_Parser.Resolver;

namespace MonoDevelop.D.Gui
{
	class EditorPathBarExtension : TextEditorExtension, IPathedDocument
	{
		public event EventHandler<DocumentPathChangedEventArgs> PathChanged;
		public MonoDevelop.Components.PathEntry[] CurrentPath
		{
			get;
			private set;
		}

		public override void Initialize()
		{
			UpdatePath(null, null);
			Document.Editor.Caret.PositionChanged += UpdatePath;
			Document.DocumentParsed += delegate { UpdatePath(null, null); };

			base.Initialize();
		}

		public Gtk.Widget CreatePathWidget(int index)
		{
			PathEntry[] path = CurrentPath;
			if (null == path || 0 > index || path.Length <= index)
			{
				return null;
			}

			object tag = path[index].Tag;
			DropDownBoxListWindow.IListDataProvider provider = null;
			if (!((tag is D_Parser.Dom.IBlockNode) || (tag is DEnumValue) || (tag is NoSelectionCustomNode)))
			{
				return null;
			}
			provider = new EditorPathbarProvider(Document, tag);

			var window = new DropDownBoxListWindow(provider);
			window.SelectItem(tag);
			return window;
		}

		protected virtual void OnPathChanged(DocumentPathChangedEventArgs args)
		{
			if (null != PathChanged)
			{
				PathChanged(this, args);
			}
		}

		private void UpdatePath(object sender, Mono.TextEditor.DocumentLocationEventArgs e)
		{
			var ast = Document.ParsedDocument as ParsedDModule;
			if (ast == null)
				return;

			var SyntaxTree = ast.DDom;

			if (SyntaxTree == null)
				return;

			// Resolve the hovered piece of code
			var loc = new CodeLocation(Document.Editor.Caret.Location.Column, Document.Editor.Caret.Location.Line);
			IStatement stmt = null;
			var currentblock = DResolver.SearchBlockAt(SyntaxTree, loc, out stmt) as IBlockNode;

			//could be an enum value, which is not IBlockNode
			if (currentblock is DEnum)
			{
				foreach (INode nd in (currentblock as DEnum).Children)
				{
					if ((nd is DEnumValue)
					&& ((nd.StartLocation <= loc) && (nd.EndLocation >= loc)))
					{
						currentblock = nd as IBlockNode;
						break;
					}
				}
			}

			List<PathEntry> result = new List<PathEntry>();
			INode node = currentblock;

			while ((node != null) && ((node is IBlockNode) || (node is DEnumValue)))
			{
				PathEntry entry;

				var icon = DCompletionData.GetNodeIcon(node as DNode);

				entry = new PathEntry(icon.IsNull?null: ImageService.GetPixbuf(icon.Name, IconSize.Menu), node.Name + DParameterDataProvider.GetNodeParamString(node));
				entry.Position = EntryPosition.Left;
				entry.Tag = node;
				//do not include the module in the path bar
				if ((node.Parent != null) && !((node is DNode) && (node as DNode).IsAnonymous))
					result.Insert(0, entry);
				node = node.Parent;
			}

			if (!((currentblock is DMethod) || (currentblock is DEnumValue)))
			{
				PathEntry noSelection = new PathEntry(GettextCatalog.GetString("No Selection")) { Tag = new NoSelectionCustomNode(currentblock) };
				result.Add(noSelection);
			}

			var prev = CurrentPath;
			CurrentPath = result.ToArray();
			OnPathChanged(new DocumentPathChangedEventArgs(prev));
		}
	}
}
