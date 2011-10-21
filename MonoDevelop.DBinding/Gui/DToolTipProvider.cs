using System;
using Mono.TextEditor;
using MonoDevelop.Components;
using MonoDevelop.D.Completion;
using D_Parser.Dom;
using MonoDevelop.Ide;
using D_Parser.Resolver;
using Gtk;
using System.Text;
using D_Parser.Completion;
using Pango;

namespace MonoDevelop.D.Gui
{
	/// <summary>
	/// Description of DToolTipProvider.
	/// </summary>
	public class DToolTipProvider:ITooltipProvider
	{
		public DToolTipProvider()
		{
		}
		
		public TooltipItem GetItem(TextEditor editor, int offset)
		{
			// Note: Normally, the document already should be open
			var doc=IdeApp.Workbench.OpenDocument(editor.Document.FileName);

			if (doc == null)
				return null;
			
			// Due the first note, the AST already should exist
			var ast = doc.ParsedDocument.LanguageAST as IAbstractSyntaxTree;

			if (ast == null)
				return null;

			// Get code cache
			var codeCache = DCodeCompletionSupport.EnumAvailableModules(doc);

			// Create editor context
			var line=editor.GetLineByOffset(offset);

			var EditorContext = new EditorData {
				CaretOffset=offset,
				CaretLocation = new CodeLocation(offset - line.Offset, editor.OffsetToLineNumber(offset)),
				ModuleCode = editor.Text,
				ParseCache = codeCache,
				SyntaxTree=ast as DModule,
				ImportCache = DResolver.ResolveImports(ast as DModule, codeCache)
			};

			// Let the engine build all contents
			var ttContents= DCodeCompletionSupport.BuildToolTip(EditorContext);

			// Create tool tip item
			if (ttContents != null)
				return new TooltipItem(ttContents, offset, 1);
			return null;
		}
		
		public Window CreateTooltipWindow(TextEditor editor, int offset, Gdk.ModifierType modifierState, TooltipItem item)
		{
			//create a message string from all the results
			var results = item.Item as AbstractTooltipContent[];

			var win = new DToolTipWindow();
			
			// Set white background
			win.ModifyBg(StateType.Normal,new Gdk.Color(0xff,0xff,0xff));

			var pack = new Gtk.VBox();
			
			foreach (var r in results)
			{
				var titleLabel = new Label(r.Title);

				// Make left-bound
				titleLabel.SetAlignment(0, 0);

				// Set bold font
				titleLabel.ModifyFont(new Pango.FontDescription() {Weight=Weight.Bold, AbsoluteSize=12*(int)Pango.Scale.PangoScale});

				pack.Add(titleLabel);

				if (!string.IsNullOrEmpty( r.Description))
				{
					var descLabel = new Label(r.Description);

					descLabel.ModifyFont(new Pango.FontDescription() { AbsoluteSize = 10 * (int)Pango.Scale.PangoScale });
					descLabel.SetAlignment(0, 0);

					pack.Add(descLabel);
				}
			}

			win.Add(pack);

			return win;
		}
		
		public void GetRequiredPosition(TextEditor editor, Gtk.Window tipWindow, out int requiredWidth, out double xalign)
		{
			var win = (TooltipWindow)tipWindow;

			// Code taken from LanugageItemWindow (public int SetMaxWidth (int maxWidth))
			var label = win.Child as VBox;
			if (label == null)
				requiredWidth= win.Allocation.Width;

			requiredWidth = label.WidthRequest;
			xalign = 0.5;
		}
		
		public bool IsInteractive(TextEditor editor, Gtk.Window tipWindow)
		{
			return false;
		}
	}
}
