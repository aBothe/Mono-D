using System.Collections.Generic;
using System.IO;
using System.Text;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Building
{
	public class MakefileGeneration
	{
		public static void GenerateMakefile(DProject prj, DProjectConfiguration cfg, ref string file)
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

			s.AppendLine("compiler=" + compiler.SourceCompilerCommand);
			s.AppendLine("linker=" + buildCommands.Linker);
			s.AppendLine();
			s.AppendLine("target="+ cfg.OutputDirectory.Combine(cfg.CompiledOutputName).ToRelative(Project.BaseDirectory));

			var srcObjPairs = new Dictionary<string, string>();
			var objs= new List<string>();

			foreach (var pf in Project.Files)
			{
				if (pf.BuildAction != BuildAction.Compile)
					continue;
				
				var obj = ProjectBuilder.GetRelativeObjectFileName(cfg.ObjectDirectory, pf, DCompilerService.ObjectExtension);

				objs.Add(obj);
				srcObjPairs[pf.FilePath.ToRelative(Project.BaseDirectory)] = obj;
			}

			s.AppendLine("objects = "+ string.Join(" ",objs));
			s.AppendLine();
			s.AppendLine();
			s.AppendLine("all: $(target)");

			// Linker
			s.AppendLine();
			s.AppendLine("$(target): $(objects)");

			var linkArgs = ProjectBuilder.FillInMacros (
				ProjectBuilder.GenAdditionalAttributes(compiler, cfg) + 
				Arguments.LinkerArguments + " " + cfg.ExtraLinkerArguments,
                new DLinkerMacroProvider
                {
                    ObjectsStringPattern = "{0}",
                    Objects = new[]{"$(objects)"},
                    TargetFile = "$@",
                    RelativeTargetDirectory = cfg.OutputDirectory.ToRelative (Project.BaseDirectory),
                    Libraries = ProjectBuilder.GetLibraries(cfg, compiler)
                });

			s.AppendLine("\t@echo Linking...");
			s.AppendLine("\t$(linker) "+ linkArgs.Trim());


			// Compiler
			s.AppendLine();

			var compilerCommand = "\t$(compiler) "+ ProjectBuilder.FillInMacros(
				Arguments.CompilerArguments + " " + cfg.ExtraCompilerArguments,
				new DCompilerMacroProvider{
					IncludePathConcatPattern = compiler.ArgumentPatterns.IncludePathPattern,
					Includes = Project.IncludePaths,
					ObjectFile = "$@", SourceFile = "$?"
				});

			foreach(var kv in srcObjPairs)
			{
				s.AppendLine(kv.Value + " : "+ kv.Key);
				s.AppendLine(compilerCommand);
				s.AppendLine();
			}

			// Clean up
			s.AppendLine("clean:");
			s.AppendLine("\t"+(OS.IsWindows?"del /Q":"$(RM)")+" \"$(target)\" $(objects)");
			

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

				if (cfg != null)
				{
					var file = "";
					MakefileGeneration.GenerateMakefile(prj, cfg, ref file);

					MessageService.ShowMessage("Makefile generated", "See " + file);
				}
				else
					MessageService.ShowError("Makefile could not be generated!");
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
