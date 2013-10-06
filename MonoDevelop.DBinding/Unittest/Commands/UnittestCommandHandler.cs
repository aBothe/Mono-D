using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Unittest.Commands
{
	public class UnittestCommandHandler : CommandHandler
	{
		protected override void Run ()
		{
			MessageHandler guiRun = delegate
			{
				var project = IdeApp.ProjectOperations.CurrentSelectedProject as DProject;
				if(project == null)
					return;
				
				DProjectConfiguration conf = project.Configurations["Unittest"] as DProjectConfiguration;
				if(conf == null)
					return;
				
				ProjectFile file = IdeApp.ProjectOperations.CurrentSelectedItem as ProjectFile;
				if(file == null)
					return;
				string filePath = file.FilePath.FullPath;
				
				UnittestCore.Run(filePath,project,conf);
			};
			DispatchService.GuiDispatch(guiRun);
		}
	}
}

