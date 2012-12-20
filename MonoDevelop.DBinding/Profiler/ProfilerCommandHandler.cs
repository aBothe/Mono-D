using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Profiler
{
	public class ProfilerCommandHandler : CommandHandler
	{
		protected override void Run ()
		{
			MessageHandler guiRun = delegate
			{
				DProject project = IdeApp.ProjectOperations.CurrentSelectedProject as DProject;
				if(project == null)
					return;
				
				Pad pad =Ide.IdeApp.Workbench.GetPad<DProfilerPad>();
				if(pad == null || !(pad.Content is DProfilerPad))
					return;
					
				DProfilerPad profilerPad = (DProfilerPad)pad.Content;
				
				profilerPad.AnalyseTraceFile(project);
			};
			DispatchService.GuiDispatch(guiRun);
		}
		/*
		protected override void Update (CommandArrayInfo info)
		{
			if(info.Count != 0)
				return;
			DProject project = IdeApp.ProjectOperations.CurrentSelectedProject as DProject;
			if(project == null)
				return;
			bool enabled = Helper.IsDProjectProfileable(project);
			
			CommandInfo runProfiler = IdeApp.CommandService.GetCommandInfo(ProfilerCommands.RunProfiler);
			runProfiler.Enabled = enabled;
			info.Add(runProfiler, ProfilerRunType.RunWihoutDebug);
			
			CommandInfo runDebugProfiler = IdeApp.CommandService.GetCommandInfo(ProfilerCommands.RunDebugProfiler);
			runDebugProfiler.Enabled = enabled;
			info.Add(runDebugProfiler, ProfilerRunType.RunWithDebug);
		} */ 
	}
}

