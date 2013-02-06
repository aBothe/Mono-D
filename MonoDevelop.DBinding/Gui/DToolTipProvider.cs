using D_Parser.Completion;
using D_Parser.Dom;
using Gtk;
using Mono.TextEditor;
using MonoDevelop.Components;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide;
using Pango;

namespace MonoDevelop.D.Gui
{
	/// <summary>
	/// Description of DToolTipProvider.
	/// </summary>
	public class DToolTipProvider:ITooltipProvider
	{
		public TooltipItem GetItem(TextEditor editor, int offset)
		{
			// Note: Normally, the document already should be open
			var doc=IdeApp.Workbench.GetDocument(editor.Document.FileName);

			if (doc == null || !(doc.ParsedDocument is ParsedDModule))
				return null;
			
			// Due the first note, the AST already should exist
			var ast = (doc.ParsedDocument as ParsedDModule).DDom;

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
				SyntaxTree=ast as DModule
			};

			// Let the engine build all contents
			var ttContents= AbstractTooltipProvider.BuildToolTip(EditorContext);

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

			var pack = new Gtk.VBox();
			
			foreach (var r in results)
			{
				var titleLabel = new Label(r.Title);

				// Make left-bound
				titleLabel.SetAlignment(0, 0);

				// Make the title bold
				titleLabel.ModifyFont(new Pango.FontDescription() {Weight=Weight.Bold, AbsoluteSize=12*(int)Pango.Scale.PangoScale});
				
				pack.Add(titleLabel);

				if (!string.IsNullOrEmpty( r.Description))
				{
					const int maximumDescriptionLength = 300;
					var descLabel = new Label(r.Description.Length>maximumDescriptionLength ? (r.Description.Substring(0,maximumDescriptionLength)+"...") : r.Description);

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
