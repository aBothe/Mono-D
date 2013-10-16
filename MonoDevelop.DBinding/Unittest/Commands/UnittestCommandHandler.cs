//
// UnittestCommandHandler.cs
//
// Author:
//       Foerdi
//
// https://github.com/aBothe/Mono-D/pull/334
//
// Copyright (c) 2013

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
		CommandInfo commandInfo;
		protected override void Update (CommandInfo info)
		{
			commandInfo = info;
			base.Update (info);
		}
		protected override void Run (object dataItem)
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
				
				IdeApp.Workbench.SaveAll();
				
				if((string)commandInfo.Command.Id ==  "MonoDevelop.D.Unittest.Commands.UnittestCommands.RunExternal")
					UnittestCore.RunExternal(filePath,project,conf);
				else
					UnittestCore.Run(filePath,project,conf);
			};
			DispatchService.GuiDispatch(guiRun);
		}
	}
}

