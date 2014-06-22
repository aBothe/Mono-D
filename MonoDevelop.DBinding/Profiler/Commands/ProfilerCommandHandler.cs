using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Profiler.Commands
{
	public class ProfilerCommandHandler : CommandHandler
	{
		protected override void Update(CommandInfo info)
		{
			base.Update(info);
			info.Enabled = IdeApp.ProjectOperations.CurrentSelectedProject is AbstractDProject;
		}

		protected override void Run ()
		{
			MessageHandler guiRun = delegate
			{
				var project = IdeApp.ProjectOperations.CurrentSelectedProject as AbstractDProject;
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

