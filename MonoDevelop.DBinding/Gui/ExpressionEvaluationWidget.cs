using System;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;
using D_Parser.Dom;
using D_Parser.Resolver;
using System.Text;
using MonoDevelop.Ide;
using MonoDevelop.Core;
using D_Parser.Dom.Expressions;

namespace MonoDevelop.D
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class ExpressionEvaluationWidget : Gtk.Bin
	{
		ExpressionEvaluationPad pad;
		Gtk.Button abortButton;

		public ExpressionEvaluationWidget (ExpressionEvaluationPad p)
		{
			this.Build ();
			this.pad = p;

			var o = editor.Options;
			inputEditor.Options = o;
			editor.Document.MimeType = 
				inputEditor.Document.MimeType = 
					Formatting.DCodeFormatter.MimeType;
			o.ShowLineNumberMargin = false;
			o.ShowFoldMargin = false;
			o.ShowInvalidLines = false;
			o.ShowIconMargin = false;

			editor.Document.ReadOnly = true;

			var tb = pad.Window.GetToolbar(Gtk.PositionType.Top);

			var ch = new Gtk.ToggleButton();
			ch.Image = new Gtk.Image(Gtk.Stock.Refresh, Gtk.IconSize.Menu);
			ch.Active = ExpressionEvaluationPad.EnableCaretTracking;
			ch.TooltipText = "Toggle automatic update after the caret has been moved.";
			ch.Toggled+=(object s, EventArgs ea)=>{ ExpressionEvaluationPad.EnableCaretTracking = (s as Gtk.ToggleToolButton).Active; };
			tb.Add(ch);

			var b = new Gtk.Button();
			b.Image = new Gtk.Image(Gtk.Stock.Execute, Gtk.IconSize.Menu);
			b.TooltipText = "Evaluates the expression typed in the upper input editor.";
			b.Clicked+=(object s, EventArgs ea)=>{ 
				MessageService.ShowMessage("Derp"); 
			};
			tb.Add(b);

			abortButton = new Gtk.Button();
			abortButton.Sensitive = false;
			abortButton.Image = new Gtk.Image(Gtk.Stock.Stop, Gtk.IconSize.Menu);
			abortButton.TooltipText = "Stops the evaluation.";
			abortButton.Clicked += (object sender, EventArgs e) => {

			};
			tb.Add(abortButton);

			tb.ShowAll();
		}

		public void Update (Document doc)
		{
			var ddoc = doc.ParsedDocument as ParsedDModule;
			if (ddoc == null)
				return;

			var ast = ddoc.DDom;
			if (ast == null)
				return;

			IStatement stmt;
			var caret = new D_Parser.Dom.CodeLocation (doc.Editor.Caret.Column, doc.Editor.Caret.Line);
			var bn = DResolver.SearchBlockAt (ast, caret, out stmt);
			bool isMixinStmt = stmt != null;
			var dbn = bn as DBlockNode;
			if (stmt == null && dbn != null && dbn.StaticStatements.Count != 0) {
				foreach (var ss in dbn.StaticStatements) {
					if (caret >= ss.Location && caret <= ss.EndLocation) {
						stmt = ss;
						break;
					}
				}
			}

			var ed = Completion.DCodeCompletionSupport.CreateEditorData (doc);
			var ctxt = new ResolutionContext (ed.ParseCache, new ConditionalCompilationFlags (ed), bn);

			var sb = new StringBuilder ();

			if (stmt is MixinStatement) {
				var mx = stmt as MixinStatement;

				if (isMixinStmt) {
					ctxt.CurrentContext.Set (mx);

					var bs = MixinAnalysis.ParseMixinStatement (mx, ctxt);

					if (bs != null)
						BuildStmtCode (sb, bs);
				} else {
					bn = MixinAnalysis.ParseMixinDeclaration (mx, ctxt);

					if (bn != null) {
						foreach (var n in bn)
							BuildModuleCode (sb, n as DNode);
					}
				}
			} else if (stmt is TemplateMixin) {
				var mxt = D_Parser.Resolver.ASTScanner.AbstractVisitor.GetTemplateMixinContent(ctxt, stmt as TemplateMixin);

				if(mxt != null)
					BuildModuleCode(sb, mxt.Definition);
			}

			DispatchService.GuiSyncDispatch(() => {
				editor.Text = sb.ToString(); 
				inputEditor.Text= stmt == null ? string.Empty : stmt.ToString();
			});
		}

		static void BuildStmtCode(StringBuilder sb, IStatement stmt, string indent = "")
		{
			if (stmt == null)
				return;

			if (stmt is BlockStatement)
			{
				var bs = stmt as BlockStatement;
				sb.Append("{");
				sb.AppendLine();

				var deeperIndent = indent + "\t";
				foreach (var sn in bs.SubStatements)
				{
					BuildStmtCode(sb, sn, deeperIndent);
				}

				sb.Append(indent);
				sb.Append("}");
				sb.AppendLine();
				return;
			}

			sb.Append(indent);
			sb.Append(stmt.ToCode());
			sb.AppendLine();
		}

		static void BuildModuleCode (StringBuilder sb, DNode bn, string indent = "")
		{
			if(bn == null)
				return;
			
			sb.Append (indent);
			sb.Append (bn.ToString (true, false));
			if (bn is IBlockNode)
			{
				sb.Append(" {");
				sb.AppendLine();

				var deeperIndent = indent + "\t";
				foreach (var sn in (bn as IBlockNode))
				{
					BuildModuleCode(sb, sn as DNode, deeperIndent);
				}

				sb.Append(indent);
				sb.Append('}');
				sb.AppendLine();
			}
			else
			{
				sb.Append(";");
				sb.AppendLine();
			}
		}
	}

	public class ExpressionEvaluationPad : AbstractPadContent
	{
		internal const string activateAutomatedCaretTrackingPropId = "mono-d-expression-evaluation-activate-caret-tracking";

		public static bool EnableCaretTracking {
			get{ return PropertyService.Get<bool> (activateAutomatedCaretTrackingPropId, true); }
			set{ PropertyService.Set(activateAutomatedCaretTrackingPropId, value); }
		}

		ExpressionEvaluationWidget widget;

		public ExpressionEvaluationPad ()
		{

		}
		
		public override void Initialize (IPadWindow window)
		{
			base.Initialize(window);
			widget = new ExpressionEvaluationWidget(this);

			widget.ShowAll();

		}

		public override Gtk.Widget Control {
			get{return widget;}
		}
		
		public override void Dispose ()
		{
			if(widget!=null)
				widget.Dispose();
		}

		public void Update (Document doc)
		{
			if(widget!=null)
				widget.Update(doc);
		}
	}
}

