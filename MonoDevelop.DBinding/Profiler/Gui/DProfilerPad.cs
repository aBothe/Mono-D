using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Profiler.Commands;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Profiler.Gui
{
	public class DProfilerPad : TreeViewPad
	{
		private ProfilerPadWidget widget;
		public TraceLogParser TraceParser {get; private set;}

		public ProfilerPadWidget Widget
		{
			get { return widget; }
		}
		
		public DProfilerPad ()
		{
			widget = new ProfilerPadWidget(this);
			TraceParser = new TraceLogParser(widget);
		}
		
		public override void Initialize (NodeBuilder[] builders, TreePadOption[] options, string contextMenuPath)
		{
			base.Initialize (builders, options, contextMenuPath);
			
			widget.ShowAll();
		}
		
		public override Gtk.Widget Control 
		{
			get { return widget; }
		}

		public void AnalyseTraceFile()
		{
			TraceParser.Parse();
		}
	}
}

