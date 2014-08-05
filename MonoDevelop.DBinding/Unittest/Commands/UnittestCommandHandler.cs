//
// UnittestCommandHandler.cs
//
// Author:
//       Foerdi
//
// https://github.com/aBothe/Mono-D/pull/334
//
// Copyright (c) 2013

using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects;
using MonoDevelop.D.Projects.Dub;
using System;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Unittest.Commands
{
	class UnittestCmdHdlrFromEditor : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			info.Visible = info.Enabled = doc != null && doc.HasProject && doc.Project is AbstractDProject;
		}

		protected override void Run (object dataItem)
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			var prj = doc.Project as AbstractDProject;
			if (prj == null)
				return;

			var monitor = Ide.IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor("dunittests","Run Unittest", MonoDevelop.Ide.Gui.Stock.RunProgramIcon, true, true);

			var pad = Ide.IdeApp.Workbench.ProgressMonitors.GetPadForMonitor(monitor);
			if (pad != null)
				pad.BringToFront();

			monitor.BeginTask("Starting Unit Tests", 1);

			DispatchService.BackgroundDispatch(()=>{
				try{
				if (prj is DubProject)
				{
					DubBuilder.ExecuteProject(prj as DubProject, monitor,
						new ExecutionContext(MonoDevelop.Core.Runtime.ProcessService.DefaultExecutionHandler, Ide.IdeApp.Workbench.ProgressMonitors, IdeApp.Workspace.ActiveExecutionTarget), 
						IdeApp.Workspace.ActiveConfiguration, "test");
				}
				else if(prj is DProject)
				{
					var dprj = prj as DProject;
					var cfg = dprj.GetConfiguration(IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;

					var cmd = UnittestCore.ExtractCommand(UnittestSettings.UnittestCommand);
					string args = UnittestCore.GetCommandArgs(UnittestSettings.UnittestCommand.Substring(cmd.Length + 1), doc.FileName, dprj, cfg);
					string errorOutput;
					string stdOutput;
					string execDir = cfg.OutputDirectory.ToAbsolute(prj.BaseDirectory);

					ProjectBuilder.ExecuteCommand(cmd, args, execDir, monitor, out stdOutput, out errorOutput);

					monitor.Log.WriteLine(stdOutput);
					monitor.Log.WriteLine(errorOutput);
				}
				}
				catch(Exception ex)
				{
					monitor.ReportError("Error during unit testing", ex);
				}
				finally
				{
					monitor.Log.WriteLine("unittest done.");
					monitor.EndTask();
					monitor.Dispose();
				}
			});
		}
	}
}

