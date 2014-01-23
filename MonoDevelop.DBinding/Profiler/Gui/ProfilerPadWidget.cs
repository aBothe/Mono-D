using System;
using MonoDevelop.Ide;
using Gtk;
using D_Parser.Dom;
using MonoDevelop.D.Profiler.Commands;
using MonoDevelop.Core;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Profiler.Gui
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class ProfilerPadWidget : Bin
	{
		ListStore traceFunctionsStore;
		DProfilerPad profilerPad;
	
		public ProfilerPadWidget (DProfilerPad pad)
		{
			profilerPad = pad;
			this.Build ();
			
			traceFunctionsStore = new ListStore (typeof(long), typeof(long), typeof(long), typeof(long), typeof(string), typeof(DNode));
			
			TreeModelSort cardSort = new TreeModelSort (traceFunctionsStore);
			
			nodeView.Model = cardSort;
			
			AddColumn("Num Calls", 0);
			AddColumn("Tree Time [µs]", 1);
			AddColumn("Func Time [µs]", 2);
			AddColumn("Per Call", 3);
			AddColumn("Func Symbol", 4);
		
			nodeView.ShowAll();
			RefreshSwitchProfilingIcon();
		}

		protected void OnRefreshActionActivated (object sender, EventArgs e)
		{
			IdeApp.CommandService.DispatchCommand( "MonoDevelop.D.Profiler.Commands.ProfilerCommands.AnalyseTaceLog");
		}
		
		public void ClearTracedFunctions()
		{
			traceFunctionsStore.Clear();
		}
		
		public void AddTracedFunction(long numCalls, long treeTime, long funcTime, long perCall, DNode symbol)
		{
			traceFunctionsStore.AppendValues(numCalls, treeTime, funcTime, perCall, symbol.ToString(false, true), symbol);
		}

		protected void OnNodeViewRowActivated (object o, RowActivatedArgs args)
		{
			GotoSelectedFunction();
		}
		
		private void AddColumn(string title, int index)
		{
			TreeViewColumn column = nodeView.AppendColumn (title, new CellRendererText (), "text", index);
			column.Resizable = true;
			column.SortColumnId = index;
			column.SortIndicator = true;
		}
		
		public void RefreshSwitchProfilingIcon()
		{
			if(ProfilerModeHandler.IsProfilerMode)
				switchProfilingModeAction.StockId = "gtk-yes";
			else
				switchProfilingModeAction.StockId = "gtk-no";
		}
		
		protected void OnSwitchProfilingModeActionActivated (object sender, EventArgs e)
		{
			ProfilerModeHandler.IsProfilerMode = !ProfilerModeHandler.IsProfilerMode;
		}

		protected void OnOpenTraceLogActionActivated (object sender, EventArgs e)
		{
			string file = TraceLogParser.TraceLogFile(IdeApp.ProjectOperations.CurrentSelectedProject as DProject);
			if(file != null)
				IdeApp.Workbench.OpenDocument(new FilePath(file), true);
		}

		protected void OnCopyRowActionActivated (object sender, EventArgs e)
		{
			Clipboard clipboard = GetClipboard(Gdk.Atom.Intern("CLIPBOARD", false));
			
			TreeIter iter;
			TreeModel model;
			nodeView.Selection.GetSelected(out model, out iter);
		
			if(model == null || !nodeView.Selection.IterIsSelected (iter))
				return;
				
			long numCalls = (long)model.GetValue(iter,0);
			long treeTime = (long)model.GetValue(iter,1);
			long funcTime = (long)model.GetValue(iter,2);
			long perCall = (long)model.GetValue(iter,3);
			string function = model.GetValue(iter,4) as String;
			
			clipboard.Text = string.Join("\t",new object[]{numCalls,treeTime,funcTime,perCall,function});
		}

		protected void OnGoToFunctionActionActivated (object sender, EventArgs e)
		{
			GotoSelectedFunction();
		}
		
		private void GotoSelectedFunction()
		{
			TreeIter iter;
			TreeModel model;
			nodeView.Selection.GetSelected(out model, out iter);
			
			if(model == null || !nodeView.Selection.IterIsSelected (iter))
				return;
				
			var n = model.GetValue(iter,5) as DNode;
			profilerPad.TraceParser.GoToFunction(n);
		}
	}
}

