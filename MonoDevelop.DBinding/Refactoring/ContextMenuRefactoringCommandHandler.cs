using System;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Refactoring;
using MonoDevelop.Ide.Gui.Content;
using System.Linq;

namespace MonoDevelop.D.Refactoring
{
	public enum Commands
	{
		GenerateMakefile,
		ContextMenuRefactoringCommands,
		OpenDDocumentation,
	}

	public class ContextMenuRefactoringCommandHandler : CommandHandler
	{
		readonly RefactoringCommandCapsule caps = new RefactoringCommandCapsule();

		protected override void Run(object dataItem)
		{
			if (dataItem is Action)
				(dataItem as Action)();
		}

		protected override void Update(CommandArrayInfo info)
		{
			caps.lastResults = Resolver.DResolverWrapper.ResolveHoveredCode(out caps.ctxt, out caps.ed);

			bool renameable = false;
			if (caps.lastResults != null && caps.lastResults.Any((t)=>{
				var n = DResolver.GetResultMember(t);
				renameable = DRenameRefactoring.CanRenameNode(n);
				return n!=null;								
			}))
			{
				info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.GotoDeclaration), new Action(caps.GotoDeclaration));
				info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.FindReferences), new Action(caps.FindReferences));

				info.AddSeparator();

				if (renameable)
					info.Add(IdeApp.CommandService.GetCommandInfo(EditCommands.Rename), new Action(caps.RenameSymbol));
			}
			else
				info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.ImportSymbol), new Action(caps.TryImportMissingSymbol));

			info.Add(IdeApp.CommandService.GetCommandInfo (Commands.OpenDDocumentation), new Action(caps.OpenDDoc));
		}
	}
}
