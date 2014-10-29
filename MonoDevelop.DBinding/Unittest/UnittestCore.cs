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
using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core.Execution;
using System.Linq;
using D_Parser.Dom;

namespace MonoDevelop.D.Unittest
{
	public static class UnittestCore
	{
		private static IConsole console;
		
		public static void RunExternal(string filePath, DProject project, DProjectConfiguration conf)
		{
			if(console == null)
				console = ExternalConsoleFactory.Instance.CreateConsole(false);
				
			string args = GetCommandArgs(UnittestSettings.UnittestCommand, filePath, project, conf);
			//string execDir = GetExecDir(project, conf);
				
			//Runtime.ProcessService.StartConsoleProcess(cmdParts[0],args,execDir,console,null);
		}

		class UnittestMacros : OneStepBuildArgumentMacroProvider
		{
			public bool HasMain = false;
			public string compilerFlags;
			public string linkerFlags;

			public override void ManipulateMacros(Dictionary<string, string> macros)
			{
				macros["main"] = HasMain ? string.Empty : UnittestSettings.MainMethodFlag;
				macros["compilerflags"] = compilerFlags;
				macros["linkerflags"] = linkerFlags;

				base.ManipulateMacros(macros);
			}
		}
		
		public static string GetCommandArgs(string baseCommandArgs, string filePath, DProject project, DProjectConfiguration conf)
		{
			var compiler =project.Compiler;
			ProjectBuilder.PrjPathMacroProvider prjPath = new ProjectBuilder.PrjPathMacroProvider {
				slnPath = project.ParentSolution != null ? ProjectBuilder.EnsureCorrectPathSeparators(project.ParentSolution.BaseDirectory) : ""
			};
			
			List<string> includes = new List<string>(project.IncludePaths);
			includes.Add(project.BaseDirectory.FullPath);

			string[] src = {filePath};
			var compilerMacro = new UnittestMacros
			{
				ObjectsStringPattern = compiler.ArgumentPatterns.ObjectFileLinkPattern,
				IncludesStringPattern = compiler.ArgumentPatterns.IncludePathPattern,

				SourceFiles = src,
				Includes = ProjectBuilder.FillInMacros(includes, prjPath),
				Libraries = ProjectBuilder.GetLibraries(conf, compiler),

				HasMain = HasMainMethod(D_Parser.Misc.GlobalParseCache.GetModule(filePath)),
				compilerFlags = conf.ExtraCompilerArguments,
				linkerFlags = conf.ExtraLinkerArguments
			};
			
			return ProjectBuilder.FillInMacros(baseCommandArgs,compilerMacro, prjPath);
		}

		public static bool HasMainMethod(DModule ast)
		{
			if (ast == null)
				return false;

			//TODO: pragma(main)

			var en = ast["main"];
			if (en != null && en.Any((m) => m is DMethod))
				return true;

			en = ast["WinMain"];
			return en != null && en.Any((m) => m is DMethod);
		}

		public static string ExtractCommand(string args)
		{
			args = args.TrimStart();
			int i;
			if (args.Length > 0 && args[0] == '"')
				i = args.IndexOf('"');
			else
				i = args.IndexOf(' ');

			if (i > 0)
				return args.Substring(0, i);

			return string.Empty;
		}
	}
}

