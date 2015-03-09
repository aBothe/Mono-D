using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using Mono.TextEditor;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System;
using System.Threading;

namespace MonoDevelop.D.Gui
{
	public class MixinInsightPad : AbstractPadContent
	{
		public static MixinInsightPad Instance;

		#region Properties
		Gtk.ScrolledWindow scrolledWindow;
		TextEditor outputEditor;
		internal const string activateAutomatedCaretTrackingPropId = "MonoD.ExpressionEvaluation.TrackCaret";
		Gtk.Button abortButton;
		Thread evalThread;

		public static bool EnableCaretTracking
		{
			get { return PropertyService.Get<bool>(activateAutomatedCaretTrackingPropId, false); }
			set { PropertyService.Set(activateAutomatedCaretTrackingPropId, value); }
		}
		#endregion

		public override void Initialize(IPadWindow pad)
		{
			base.Initialize(pad);

			// Init editor
			outputEditor = new TextEditor();
			outputEditor.Events = Gdk.EventMask.AllEventsMask;
			outputEditor.Name = "outputEditor";
			outputEditor.TabsToSpaces = false;
			
			scrolledWindow = new Gtk.ScrolledWindow();
			scrolledWindow.Child = outputEditor;
			scrolledWindow.ShadowType = Gtk.ShadowType.In;
			scrolledWindow.ShowAll();
			outputEditor.ShowAll();

			var o = outputEditor.Options;
			outputEditor.Document.MimeType = Formatting.DCodeFormatter.MimeType;
			o.ShowLineNumberMargin = false;
			o.ShowFoldMargin = false;
			o.ShowIconMargin = false;
			outputEditor.Document.ReadOnly = true;


			// Init toolbar
			var tb = pad.GetToolbar(Gtk.PositionType.Top);

			var ch = new Gtk.ToggleButton();
			ch.Image = new Gtk.Image(Gtk.Stock.Refresh, Gtk.IconSize.Menu);
			ch.Active = EnableCaretTracking;
			ch.TooltipText = "Toggle automatic update after the caret has been moved.";
			ch.Toggled += (object s, EventArgs ea) => EnableCaretTracking = ch.Active;
			tb.Add(ch);

			abortButton = new Gtk.Button();
			abortButton.Sensitive = false;
			abortButton.Image = new Gtk.Image(Gtk.Stock.Stop, Gtk.IconSize.Menu);
			abortButton.TooltipText = "Stops the evaluation.";
			abortButton.Clicked += (object sender, EventArgs e) => AbortExecution();
			tb.Add(abortButton);

			tb.ShowAll();
			Instance = this;
		}

		public override Gtk.Widget Control
		{
			get { return scrolledWindow; }
		}

		#region GUI Interaction
		bool CanAbort
		{
			set { DispatchService.GuiDispatch(() => abortButton.Sensitive = value); }
		}

		void AbortExecution()
		{
			abortButton.Sensitive = false;

			if (evalThread != null && evalThread.IsAlive)
				evalThread.Abort();
		}

		#endregion

		#region Analysis
		public void Update()
		{
			AbortExecution();

			var ctxt = Completion.DCodeCompletionSupport.CreateCurrentContext();
			var stmt = DResolver.SearchStatementDeeplyAt(ctxt.ScopedBlock, ctxt.CurrentContext.Caret);

			if (stmt == null)
				return;

			evalThread = new Thread(execTh) { IsBackground = true, Priority = ThreadPriority.Lowest };
			evalThread.Start(new Tuple<ISyntaxRegion, ResolutionContext>(stmt, ctxt));
		}

		void execTh(object p)
		{
			CanAbort = true;
			try
			{
				var tup = p as Tuple<ISyntaxRegion, ResolutionContext>;
				var sr = tup.Item1;
				var ctxt = tup.Item2;

				object result;
				VariableValue vv;

				if (sr is MixinStatement)
					result = MixinAnalysis.ParseMixinDeclaration(sr as MixinStatement, ctxt, out vv);
				else if (sr is TemplateMixin)
					result = TypeDeclarationResolver.ResolveSingle((sr as TemplateMixin).Qualifier, ctxt);
				else
					result = null;

				var o = ExpressionEvaluationPad.BuildObjectString(result);
				DispatchService.GuiDispatch(() => outputEditor.Text = o);
			}
			finally
			{
				CanAbort = false;
			}
		}
		#endregion
	}
}
