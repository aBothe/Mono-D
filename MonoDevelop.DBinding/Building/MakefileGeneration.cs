using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Ide.Gui.Pads;
using System.IO;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Building
{
	public class MakefileGeneration
	{
		public static void GenerateMakefile(DProject prj, DProjectConfiguration cfg, string file = null)
		{
			if (string.IsNullOrEmpty(file))
				file = prj.BaseDirectory.Combine("makefile");

			var code = GenerateMakeCode(prj, cfg);

			File.WriteAllText(file, code);
		}

		public static string GenerateMakeCode(DProject Project, DProjectConfiguration cfg)
		{
			var compiler = Project.Compiler;

			var s = new StringBuilder();

			// Constants
			var buildCommands = compiler.GetOrCreateTargetConfiguration(cfg.CompileTarget);
			var Arguments = buildCommands.GetArguments(cfg.DebugMode);

			s.AppendLine("compiler=" + buildCommands.Compiler);
			s.AppendLine("linker=" + buildCommands.Linker);
			s.AppendLine();
			s.AppendLine("target="+ cfg.OutputDirectory.Combine(cfg.CompiledOutputName));
			s.AppendLine();

			var srcObjPairs = new Dictionary<string, string>();

			foreach (var pf in Project.Files)
			{
				if (pf.BuildAction != BuildAction.Compile)
					continue;
				/*
				srcObjPairs[pf.FilePath.ToRelative(Project.BaseDirectory)] = ProjectBuilder.HandleObjectFileNaming(
					cfg.ObjectDirectory, 
					);*/
			}



			s.AppendLine("all: $(sources) $(target)");



			// Linker
			s.AppendLine();
			s.AppendLine("target: $(objects)");

			var libs = new List<string> (compiler.DefaultLibraries);
			libs.AddRange (cfg.ExtraLibraries);

			var linkArgs = ProjectBuilder.FillInMacros (Arguments.LinkerArguments + " " + cfg.ExtraLinkerArguments,
                new DLinkerMacroProvider
                {
                    ObjectsStringPattern = "{0}",
                    Objects = new[]{"$(objects)"},
                    TargetFile = "$@",
                    RelativeTargetDirectory = cfg.OutputDirectory.ToRelative (Project.BaseDirectory),
                    Libraries = libs
                });
			s.AppendLine("\t$(linker) "+ linkArgs.Trim());


			// Compiler
			s.AppendLine();
			s.AppendLine("%.d : $" + DCompilerService.ObjectExtension);

			var sourceFileIncludePaths=new List<string>(compiler.ParseCache.ParsedDirectories);
			sourceFileIncludePaths.AddRange (Project.LocalIncludeCache.ParsedDirectories);

			s.AppendLine("\t$(compiler) "+ ProjectBuilder.FillInMacros(
				Arguments.CompilerArguments + " " + cfg.ExtraCompilerArguments,
				new DCompilerMacroProvider{
					IncludePathConcatPattern = buildCommands.IncludePathPattern,
					Includes = sourceFileIncludePaths,
					ObjectFile = "$@", SourceFile = "$<"
				}));

			return s.ToString();
		}
	}

	public class MakefileGenerationCommandHandler : CommandHandler
	{
		protected override void Run(object dataItem)
		{
			var p = Ide.IdeApp.Workbench.GetPad<MonoDevelop.Ide.Gui.Pads.ProjectPad.ProjectSolutionPad>();

			if (p == null && !(p.Content is ProjectSolutionPad))
				return;

			var psp = (ProjectSolutionPad)p.Content;
			var selectedItem = psp.TreeView.GetSelectedNode();

			if (selectedItem == null)
				return;

			if (selectedItem.DataItem is DProject)
			{
				var prj = (DProject)selectedItem.DataItem;
				var cfg = prj.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;

				if(cfg != null)
					MakefileGeneration.GenerateMakefile(prj, cfg);
			}
		}

		protected override void Update(CommandInfo info)
		{
			info.Enabled = false;
			var p=Ide.IdeApp.Workbench.GetPad<MonoDevelop.Ide.Gui.Pads.ProjectPad.ProjectSolutionPad>();

			if (p != null && p.Content is ProjectSolutionPad)
			{
				var psp = (ProjectSolutionPad)p.Content;
				var selectedItem = psp.TreeView.GetSelectedNode();

				if (selectedItem != null)
				{
					if (selectedItem.DataItem is DProject)
					{
						info.Enabled = true;
					}
				}
			}
		}
	}
}
