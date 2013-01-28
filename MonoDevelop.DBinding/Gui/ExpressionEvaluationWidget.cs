using System;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;
using D_Parser.Dom;
using D_Parser.Resolver;
using System.Text;

namespace MonoDevelop.D
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class ExpressionEvaluationWidget : Gtk.Bin
	{
		ExpressionEvaluationPad pad;

		public ExpressionEvaluationWidget (ExpressionEvaluationPad p)
		{
			this.Build ();
			this.pad = p;

			var o = editor.Options;
			editor.Document.MimeType = Formatting.DCodeFormatter.MimeType;
			o.ShowLineNumberMargin = false;
			o.ShowFoldMargin = false;
			o.ShowInvalidLines = false;
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
			var dbn = DResolver.SearchBlockAt (ast, caret, out stmt) as DBlockNode;
			bool isMixinStmt = stmt != null;

			if (stmt == null && dbn != null && dbn.StaticStatements.Count != 0) {
				foreach (var ss in dbn.StaticStatements) {
					if (caret >= ss.Location && caret <= ss.EndLocation) {
						stmt = ss;
						break;
					}
				}
			}

			if (stmt is MixinStatement) {
				var mx = stmt as MixinStatement;
				var ed = Completion.DCodeCompletionSupport.CreateEditorData(doc);
				var ctxt = new ResolutionContext(ed.ParseCache, new ConditionalCompilationFlags(ed), dbn, isMixinStmt ? stmt : null);

				var sb = new StringBuilder();

				MixinAnalysis.UseResultCaching = false;
				
				if(isMixinStmt)
				{
					var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt);
				}
				else
				{
					var bn = MixinAnalysis.ParseMixinDeclaration(mx, ctxt);

					if(bn != null)
					{
						foreach(var n in bn)
							BuildModuleCode(sb, n as DNode);
					}
				}
				
				MixinAnalysis.UseResultCaching = true;
				editor.Text = sb.ToString();
			}
		}

		static void BuildModuleCode (StringBuilder sb, DNode bn, string indent = "")
		{
			if(bn == null)
				return;
			
			sb.Append (indent);
			sb.AppendLine (bn.ToString (true, false));
			if (bn is IBlockNode) {
				sb.Append (indent);
				sb.Append('{');
				sb.AppendLine ();

				var deeperIndent = indent + "\t";
				foreach (var sn in (bn as IBlockNode)) {
					BuildModuleCode(sb, sn as DNode, deeperIndent);
				}

				sb.Append (indent);
				sb.Append('}');
				sb.AppendLine ();
			}
		}
	}

	public class ExpressionEvaluationPad : AbstractPadContent
	{
		ExpressionEvaluationWidget widget;

		public ExpressionEvaluationPad ()
		{
			widget = new ExpressionEvaluationWidget(this);
		}
		
		public override void Initialize (IPadWindow window)
		{
			base.Initialize(window);
			widget.ShowAll();
		}

		public override Gtk.Widget Control {
			get{return widget;}
		}
		
		public override void Dispose ()
		{
			widget.Dispose();
		}



		public void Update (Document doc)
		{
			widget.Update(doc);
		}
	}
}

