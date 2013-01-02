using System;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Profiler.Commands;

namespace MonoDevelop.D.Profiler.Gui
{
	public class DProfilerPad : TreeViewPad
	{
		private ProfilerPadWidget widget;
		public TraceLogParser TraceParser {get; private set;}
		
		public static void ShowPad(bool visible)
		{
			Pad pad =Ide.IdeApp.Workbench.GetPad<DProfilerPad>();
			if(pad == null || !(pad.Content is DProfilerPad))
				return;
			
			pad.Visible = visible;
		}
		
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
		
		public void AnalyseTraceFile(DProject project)
		{
			TraceParser.Clear();
			
			var config = project.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;
			
			if (config == null || ProfilerModeHandler.IsProfilerMode == false || 
			    config.CompileTarget != DCompileTarget.Executable || 
			    project.Compiler.HasProfilerSupport == false)
			{
				return;
			}
			TraceParser.Parse(project, config.OutputDirectory);
		}
	}
}

