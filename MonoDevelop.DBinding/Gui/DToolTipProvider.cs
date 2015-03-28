using D_Parser.Completion;
using D_Parser.Dom;
using Gtk;
using Mono.TextEditor;
using MonoDevelop.Components;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using System;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
namespace MonoDevelop.D.Gui
{
	/// <summary>
	/// Description of DToolTipProvider.
	/// </summary>
	public class DToolTipProvider:TooltipProvider, IDisposable
	{
		#region Properties
		ISemantic lastNode;
		static TooltipInformationWindow lastWindow = null;
		//TooltipItem lastResult;
		#endregion

		#region Lowlevel
		static void DestroyLastTooltipWindow ()
		{
			if (lastWindow != null) {
				lastWindow.Destroy ();
				lastWindow = null;
			}
		}

		#region IDisposable implementation

		public void Dispose ()
		{
			DestroyLastTooltipWindow ();
			lastNode = null;
			//lastResult = null;
		}

		#endregion

		class TTI{
			public AbstractType t;
			public ISyntaxRegion sr;
		}

		protected override Window CreateTooltipWindow (TextEditor editor, int offset, Gdk.ModifierType modifierState, TooltipItem item)
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return null;

			var titem = item.Item as TTI;

			if (titem == null)
				return null;

			var result = new TooltipInformationWindow ();
			result.ShowArrow = true;

			foreach(var i in AmbiguousType.TryDissolve(titem.t))
			{
				if (i == null)
					continue;
				var tooltipInformation = TooltipInfoGen.Create(i, editor.ColorStyle);
				if (tooltipInformation != null && !string.IsNullOrEmpty(tooltipInformation.SignatureMarkup))
					result.AddOverload(tooltipInformation);
			}

			if (result.Overloads < 1) {
				result.Dispose ();
				return null;
			}

			result.RepositionWindow ();
			return result;
		}

		public override Window ShowTooltipWindow (TextEditor editor, int offset, Gdk.ModifierType modifierState, int mouseX, int mouseY, TooltipItem item)
		{
			var titem = (item.Item as TTI).sr;
			DestroyLastTooltipWindow ();

			var tipWindow = CreateTooltipWindow (editor, offset, modifierState, item) as TooltipInformationWindow;
			if (tipWindow == null)
				return null;

			var positionWidget = editor.TextArea;

			Cairo.Point p1, p2;

			var dn = titem as INode;
			if (dn != null)
			{
				if (dn.NameLocation.IsEmpty)
					p1 = p2 = editor.LocationToPoint(dn.Location.Line, dn.Location.Column);
				else
				{
					p1 = editor.LocationToPoint(dn.NameLocation.Line, dn.NameLocation.Column);
					p2 = editor.LocationToPoint(dn.NameLocation.Line, dn.NameLocation.Column + (dn.Name ?? "").Length);
				}
			}
			else {
				p1 = editor.LocationToPoint (editor.OffsetToLocation(item.ItemSegment.Offset));
				p2 = editor.LocationToPoint (editor.OffsetToLocation(item.ItemSegment.EndOffset));
			}

			var caret = new Gdk.Rectangle (p1.X - positionWidget.Allocation.X, p2.Y - positionWidget.Allocation.Y, (p2.X - p1.X), (int)editor.LineHeight);
			tipWindow.ShowPopup (positionWidget, caret, PopupPosition.Top);
			

			lastWindow = tipWindow;

			tipWindow.EnterNotifyEvent += delegate {
				editor.HideTooltip (false);
			};

			//lastNode = titem.Result;
			return tipWindow;
		}

		protected override void GetRequiredPosition (TextEditor editor, Window tipWindow, out int requiredWidth, out double xalign)
		{
			var win = (TooltipInformationWindow)tipWindow;
			requiredWidth = win.Allocation.Width;
			xalign = 0.5;
		}
		#endregion

		public override TooltipItem GetItem(TextEditor editor, int offset)
		{
			// Note: Normally, the document already should be open
			var doc=IdeApp.Workbench.GetDocument(editor.Document.FileName);

			if (doc == null)
				return null;

			var ast = doc.GetDAst();
			
			// Due the first note, the AST already should exist
			if (ast == null)
				return null;

			// Get code cache
			var codeCache = DResolverWrapper.CreateParseCacheView(doc);

			// Create editor context
			var line=editor.GetLineByOffset(offset);

			var ed = new EditorData {
				CaretOffset=offset,
				CaretLocation = new CodeLocation(offset - line.Offset, editor.OffsetToLineNumber(offset)),
				ModuleCode = editor.Text,
				ParseCache = codeCache,
				SyntaxTree = ast
			};

			// Let the engine build all contents
			LooseResolution.NodeResolutionAttempt att;
			ISyntaxRegion sr;
			var rr = LooseResolution.ResolveTypeLoosely(ed, out att, out sr);

			// Create tool tip item
			if (rr != null)
				return new TooltipItem (new TTI{t = rr, sr = sr}, offset, 1);
			
			return null;
		}
	}
}
