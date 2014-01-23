using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Profiler.Commands
{
	public class ProfilerCommandHandler : CommandHandler
	{
		protected override void Run ()
		{
			MessageHandler guiRun = delegate
			{
				var project = IdeApp.ProjectOperations.CurrentSelectedProject as DProject;
				if(project == null)
					return;
				
				Pad pad =IdeApp.Workbench.GetPad<DProfilerPad>();
				if(pad == null || !(pad.Content is DProfilerPad))
					return;
					
				DProfilerPad profilerPad = (DProfilerPad)pad.Content;
				
				pad.Visible = true;
				profilerPad.AnalyseTraceFile(project);
			};
			DispatchService.GuiDispatch(guiRun);
		}
	}
}

