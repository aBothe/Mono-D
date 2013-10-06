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
			monitor.BeginTask("start unittest...",2);
			
			string[] cmdParts = project.Compiler.RdmdUnittestCommand.Split(new string[]{" "}, 2 , StringSplitOptions.RemoveEmptyEntries);
			
//			string args = GetCommandArgs("-unittest -main $libs $includes $sources",filePath,project,conf);
			string args = GetCommandArgs(cmdParts.Length >= 2 ?cmdParts[1] : "",filePath,project,conf);
			string errorOutput;
			string stdOutput;
			ProjectBuilder.ExecuteCommand(cmdParts[0],args,project.BaseDirectory.FullPath,monitor,out errorOutput, out stdOutput);
			monitor.ReportSuccess(stdOutput);
			monitor.EndTask();
		} 
		
		static string GetCommandArgs(string baseCommandArgs, string filePath, DProject project, DProjectConfiguration conf)
		{
			var compiler =project.Compiler;
			ProjectBuilder.PrjPathMacroProvider prjPath = new ProjectBuilder.PrjPathMacroProvider {
				slnPath = project.ParentSolution != null ? ProjectBuilder.EnsureCorrectPathSeparators(project.ParentSolution.BaseDirectory) : ""
			};
			
			string[] src = {filePath};
			OneStepBuildArgumentMacroProvider compilerMacro = new OneStepBuildArgumentMacroProvider
			{
				ObjectsStringPattern = compiler.ArgumentPatterns.ObjectFileLinkPattern,
				IncludesStringPattern = compiler.ArgumentPatterns.IncludePathPattern,

				SourceFiles = src,
				Includes = ProjectBuilder.FillInMacros(project.IncludePaths, prjPath),
				Libraries = ProjectBuilder.GetLibraries(conf, compiler),

			};
			return ProjectBuilder.FillInMacros(baseCommandArgs,compilerMacro, prjPath);
		}
	}
}

