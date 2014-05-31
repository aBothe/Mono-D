using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using Gtk;
using MonoDevelop.Components;
using MonoDevelop.Core;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.D.Gui
{
	class EditorPathBarExtension : TextEditorExtension, IPathedDocument
	{
		public event EventHandler<DocumentPathChangedEventArgs> PathChanged;
		public PathEntry[] CurrentPath
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

		class NoSelectionCustomNode : DNode
		{
			public NoSelectionCustomNode(INode parent)
			{
				this.Parent = parent;
			}

			public override void Accept(NodeVisitor vis)
			{
				throw new NotImplementedException();
			}

			public override R Accept<R>(NodeVisitor<R> vis)
			{
				throw new NotImplementedException();
			}
		}	

		public Widget CreatePathWidget(int index)
		{
			var path = CurrentPath;
			if (null == path || index < 0 || path.Length <= index)
				return null;

			var tag = path[index].Tag;
			var window = new DropDownBoxListWindow(tag == null ? (MonoDevelop.Components.DropDownBoxListWindow.IListDataProvider)new CompilationUnitDataProvider(Document) : new EditorPathbarProvider(Document, tag));
			
			window.SelectItem(tag);

			return window;
		}


		static PathEntry GetRegionEntry(ParsedDocument unit, Mono.TextEditor.DocumentLocation loc)
		{
			PathEntry entry;
			if (!unit.UserRegions.Any())
				return null;
			var reg = unit.UserRegions.LastOrDefault(r => r.Region.IsInside(loc));
			if (reg == null)
			{
				entry = new PathEntry(GettextCatalog.GetString("No region"));
			}
			else
			{
				entry = new PathEntry(CompilationUnitDataProvider.Pixbuf,
									   GLib.Markup.EscapeText(reg.Name));
			}
			entry.Position = EntryPosition.Right;
			return entry;
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
			var currentblock = DResolver.SearchBlockAt (SyntaxTree, loc);

			//could be an enum value, which is not IBlockNode
			if (currentblock is DEnum)
			{
				foreach (INode nd in (currentblock as DEnum).Children)
				{
					if ((nd is DEnumValue)
					&& ((nd.Location <= loc) && (nd.EndLocation >= loc)))
					{
						currentblock = nd as IBlockNode;
						break;
					}
				}
			}

			List<PathEntry> result = new List<PathEntry>();
			INode node = currentblock;
			PathEntry entry;

			while ((node != null) && ((node is IBlockNode) || (node is DEnumValue)))
			{
				var icon = DIcons.GetNodeIcon(node as DNode);

				entry = new PathEntry(icon.IsNull?null: ImageService.GetIcon(icon.Name, IconSize.Menu), node.Name + DParameterDataProvider.GetNodeParamString(node));
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

			entry = GetRegionEntry(Document.ParsedDocument, Document.Editor.Caret.Location);
			if (entry != null)
				result.Add(entry);

			var prev = CurrentPath;
			CurrentPath = result.ToArray();
			OnPathChanged(new DocumentPathChangedEventArgs(prev));
		}


		#region Region dropdown

		class CompilationUnitDataProvider : DropDownBoxListWindow.IListDataProvider
		{
			Document Document
			{
				get;
				set;
			}

			public CompilationUnitDataProvider(Document document)
			{
				this.Document = document;
			}

			#region IListDataProvider implementation

			public void Reset()
			{
			}

			public string GetMarkup(int n)
			{
				return GLib.Markup.EscapeText(Document.ParsedDocument.UserRegions.ElementAt(n).Name);
			}

			internal static Xwt.Drawing.Image Pixbuf
			{
				get
				{
					return ImageService.GetIcon(Gtk.Stock.Add, Gtk.IconSize.Menu);
				}
			}

			public Xwt.Drawing.Image GetIcon(int n)
			{
				return Pixbuf;
			}

			public object GetTag(int n)
			{
				return Document.ParsedDocument.UserRegions.ElementAt(n);
			}

			public void ActivateItem(int n)
			{
				var reg = Document.ParsedDocument.UserRegions.ElementAt(n);
				var extEditor = Document.GetContent<MonoDevelop.Ide.Gui.Content.IExtensibleTextEditor>();
				if (extEditor != null)
					extEditor.SetCaretTo(Math.Max(1, reg.Region.BeginLine), reg.Region.BeginColumn);
			}

			public int IconCount
			{
				get
				{
					if (Document.ParsedDocument == null)
						return 0;
					return Document.ParsedDocument.UserRegions.Count();
				}
			}

			#endregion

		}

		#endregion
	}
}
