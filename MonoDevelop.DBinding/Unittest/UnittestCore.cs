//
// UnittestCore.cs
//
// Author:
//       Foerdi
//
// https://github.com/aBothe/Mono-D/pull/334
//
// Copyright (c) 2013

using System;
using MonoDevelop.D.Projects;
using MonoDevelop.D.Building;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace MonoDevelop.D.Unittest
{
	public class UnittestCore
	{
		private static ProgressMonitorManager manager;
		private static IProgressMonitor monitor;
		
		
		public static void Run(string filePath, DProject project, DProjectConfiguration conf)
		{
			if(manager == null)
			{
				manager = new ProgressMonitorManager();
				monitor = manager.GetOutputProgressMonitor("Run Unittest",Stock.RunProgramIcon,true,true); //manager.GetBuildProgressMonitor();//manager.GetStatusProgressMonitor("Run Unittest",Stock.RunProgramIcon,true);
			}
			
			Pad pad = manager.GetPadForMonitor(monitor);
			if(pad != null)
				pad.BringToFront();
				
			monitor.BeginTask("start unittest...",2);
			
			new Thread(delegate (){
				string[] cmdParts = project.Compiler.RdmdUnittestCommand.Split(new string[]{" "}, 2 , StringSplitOptions.RemoveEmptyEntries);
				
//			string args = GetCommandArgs("-unittest -main $libs $includes $sources",filePath,project,conf);
				string args = GetCommandArgs(cmdParts.Length >= 2 ?cmdParts[1] : "",filePath,project,conf);
				string errorOutput;
				string stdOutput;
				string execDir = conf.OutputDirectory.FullPath;
				if(Directory.Exists(execDir) == false)
					execDir = project.BaseDirectory.FullPath;
				ProjectBuilder.ExecuteCommand(cmdParts[0],args,execDir,monitor,out errorOutput, out stdOutput);
				monitor.Log.WriteLine("unittest done.");
				monitor.EndTask();
			}).Start();
		} 
		
		static string GetCommandArgs(string baseCommandArgs, string filePath, DProject project, DProjectConfiguration conf)
		{
			var compiler =project.Compiler;
			ProjectBuilder.PrjPathMacroProvider prjPath = new ProjectBuilder.PrjPathMacroProvider {
				slnPath = project.ParentSolution != null ? ProjectBuilder.EnsureCorrectPathSeparators(project.ParentSolution.BaseDirectory) : ""
			};
			
			List<string> includes = new List<string>(project.IncludePaths);
			includes.Add(project.BaseDirectory.FullPath);
			
			string[] src = {filePath};
			OneStepBuildArgumentMacroProvider compilerMacro = new OneStepBuildArgumentMacroProvider
			{
				ObjectsStringPattern = compiler.ArgumentPatterns.ObjectFileLinkPattern,
				IncludesStringPattern = compiler.ArgumentPatterns.IncludePathPattern,

				SourceFiles = src,
				Includes = ProjectBuilder.FillInMacros(includes, prjPath),
				Libraries = ProjectBuilder.GetLibraries(conf, compiler),

			};
			
			return ProjectBuilder.FillInMacros(baseCommandArgs,compilerMacro, prjPath);
		}
	}
}

