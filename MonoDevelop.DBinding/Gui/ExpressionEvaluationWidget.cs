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
using D_Parser.Parser;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using D_Parser.Resolver.ExpressionSemantics;
using Mono.TextEditor;
using D_Parser.Dom.Expressions;
using System.Threading;
using D_Parser.Misc;

namespace MonoDevelop.D
{
	public class ExpressionEvaluationPad : AbstractPadContent
	{
		#region Properties
		Gtk.VPaned vpaned;
		TextEditor inputEditor, editor;
		Gtk.Button executeButton;
		Gtk.Button abortButton;

		List<ErrorMarker> inputErrors = new List<ErrorMarker>();
		Thread evalThread;
		#endregion
		
		public override void Initialize (IPadWindow window)
		{
			base.Initialize(window);

			// Init editors
			inputEditor = new TextEditor();
			editor = new TextEditor();
			inputEditor.HeightRequest = 80;

			editor.Events = inputEditor.Events = Gdk.EventMask.AllEventsMask;

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


			// Init layout
			vpaned = new Gtk.VPaned();

			var scr1 = new Gtk.ScrolledWindow();
			scr1.ShadowType = Gtk.ShadowType.In;
			scr1.Child = inputEditor;
			vpaned.Add1(scr1);

			var scr2 = new Gtk.ScrolledWindow();
			scr2.ShadowType = Gtk.ShadowType.In;
			scr2.Child = editor;
			vpaned.Add2(scr2);

			vpaned.ShowAll();


			// Init toolbar
			var tb = window.GetToolbar(Gtk.PositionType.Top);

			executeButton = new Gtk.Button();
			executeButton.Image = new Gtk.Image(Gtk.Stock.Execute, Gtk.IconSize.Menu);
			executeButton.TooltipText = "Evaluates the expression typed in the upper input editor.";
			executeButton.Clicked += Execute;
			tb.Add(executeButton);

			abortButton = new Gtk.Button();
			abortButton.Sensitive = false;
			abortButton.Image = new Gtk.Image(Gtk.Stock.Stop, Gtk.IconSize.Menu);
			abortButton.TooltipText = "Stops the evaluation.";
			abortButton.Clicked += (object sender, EventArgs e) => AbortExecution();
			tb.Add(abortButton);

			tb.ShowAll();
		}

		public override Gtk.Widget Control {
			get{return vpaned;}
		}


		class ErrorMarker : UnderlineMarker
		{
			public ParserError Info { get; private set; }

			public ErrorMarker(TextDocument doc, ParserError info, DocumentLine line)
			{
				Info = info;
				LineSegment = line;
				// may be null if no line is assigned to the error.
				Wave = true;

				ColorName = Mono.TextEditor.Highlighting.ColorScheme.ErrorUnderlineString;
				StartCol = info.Location.Column;

				if (line == null)
				{
					EndCol = StartCol;
					return;
				}
				var start = line.Offset + StartCol;
				int o = start + 1;
				while (o < line.EndOffset)
				{
					char ch = doc.GetCharAt(o);
					if (!(char.IsLetterOrDigit(ch) || ch == '_'))
						break;
					o++;
				}
				EndCol = Info.Location.Column + o - start;
			}
		}

		void Execute(object s, EventArgs ea)
		{
			AbortExecution();
			editor.Text = string.Empty;

			// Parse input
			var tr = new System.IO.StringReader(inputEditor.Text);
			var p = DParser.Create(tr);
			p.Step();
			var x = p.Expression(null);
			tr.Close();

			// Handle evaluation errors
			foreach (var mkr in inputErrors)
				inputEditor.Document.RemoveMarker(mkr);

			if (p.ParseErrors.Count != 0)
			{
				foreach (var parserError in p.ParseErrors)
				{
					var ln = inputEditor.GetLine(parserError.Location.Line);
					var mkr = new ErrorMarker(inputEditor.Document, parserError, ln);
					inputErrors.Add(mkr);
					inputEditor.Document.AddMarker(ln, mkr);
				}
				inputEditor.Document.CommitUpdateAll();

				return;
			}

			// Evaluate
			var ctxt = Completion.DCodeCompletionSupport.CreateCurrentContext();

			evalThread = new Thread(execTh) { IsBackground = true, Priority = ThreadPriority.Lowest };
			evalThread.Start(new Tuple<IExpression, ResolutionContext>(x, ctxt));
		}

		bool CanAbort
		{
			set { DispatchService.GuiDispatch(() => { abortButton.Sensitive = value; executeButton.Sensitive = !value; }); }
		}

		void execTh(object p)
		{
			CanAbort = true;
			try
			{
				var tup = p as Tuple<IExpression, ResolutionContext>;
				object result = null;

				try
				{
					result = Evaluation.EvaluateValue(tup.Item1, tup.Item2);
				}
				catch (Exception exc)
				{
					result = exc.Message + "\n\n" + exc.StackTrace.Substring(0, Math.Min(300, exc.StackTrace.Length));
				}

				var o = BuildObjectString(result);
				DispatchService.GuiDispatch(() => editor.Text = o);
			}
			finally
			{
				CanAbort = false;
			}
		}

		void AbortExecution()
		{
			executeButton.Sensitive = true;
			abortButton.Sensitive = false;

			if (evalThread != null && evalThread.IsAlive)
				evalThread.Abort();
		}

		internal static string BuildObjectString(object result)
		{
			var sb = new StringBuilder();

			if (result is DSymbol)
			{
				var ds = result as DSymbol;

				//TODO:
				// - Define deduced parameters in a context
				// - Resolve every subnode's base type
				// - Replace all nodes' Type properties with the resolved one.

				BuildModuleCode(sb, ds.Definition);
			}
			else if (result is ErrorValue)
				sb.Append((result as ISymbolValue).ToString());
			else if (result is ISymbolValue)
				sb.Append((result as ISymbolValue).ToCode());
			else if (result is IStatement)
				BuildStmtCode(sb, result as IStatement);
			else if (result is IBlockNode)
				foreach (var n in result as IBlockNode)
					BuildModuleCode(sb, n as DNode);
			else if (result is DNode)
				BuildModuleCode(sb, result as DNode);
			else if (result is String)
				sb.Append(result as String);

			return sb.ToString();
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

			if (stmt is StatementContainingStatement)
				BuildStmtCode(sb, (stmt as StatementContainingStatement).ScopedStatement, indent + "\t");
		}

		static void BuildModuleCode(StringBuilder sb, DNode bn, string indent = "")
		{
			if (bn == null)
				return;

			sb.Append(indent);
			sb.Append(bn.ToString(true, false));
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
}

