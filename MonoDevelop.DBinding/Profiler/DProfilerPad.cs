using System;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Profiler
{
	public class DProfilerPad : TreeViewPad
	{
		private ProfilerPadWidget widget;
		public TraceLogParser TraceParser {get; private set;}
		
		public DProfilerPad ()
		{
			widget = new ProfilerPadWidget(this);
			TraceParser = new TraceLogParser(widget);
		}
		
		public override void Initialize (NodeBuilder[] builders, TreePadOption[] options, string contextMenuPath)
		{
			base.Initialize (builders, options, contextMenuPath);
			
			
			widget.ShowAll();
			
			//TreeView.Clear ();
			//TreeView.LoadTree();
			//TreeView.LoadTree(ConnectionContextService.DatabaseConnections);
		}
		
		public override Gtk.Widget Control 
		{
			get { return widget; }
		}
		
		public void AnalyseTraceFile(DProject project)
		{
			var config = project.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;
			
			if (config == null || config.ProfilerMode == false || 
			    config.CompileTarget != DCompileTarget.Executable || 
			    project.Compiler.HasProfilerSupport == false)
			{
				return;
			}
			TraceParser.Parse(project, config.OutputDirectory);
		}
	}
}

