using System;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Refactoring;
using MonoDevelop.Ide.Gui.Content;

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
		ResolutionContext ctxt;
		AbstractType res;
		INode n;

		protected override void Run(object dataItem)
		{
			if (dataItem is Action)
				(dataItem as Action)();
		}

		protected override void Update(CommandArrayInfo info)
		{
			var rr = Resolver.DResolverWrapper.ResolveHoveredCode(out ctxt);

			bool noRes = true;

			if (rr != null && rr.Length > 0)
			{
				res = rr[rr.Length - 1];

				n = DResolver.GetResultMember(res);

				if (n != null)
				{
					noRes = false;
					info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.GotoDeclaration), new Action(GotoDeclaration));
					info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.FindReferences), new Action(FindReferences));

					info.AddSeparator();

					if (DRenameRefactoring.CanRenameNode(n))
						info.Add(IdeApp.CommandService.GetCommandInfo(EditCommands.Rename), new Action(RenameSymbol));
				}
			}

			if(noRes)
				info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.ImportSymbol), new Action(ImportSymbol));

			info.Add(IdeApp.CommandService.GetCommandInfo (Commands.OpenDDocumentation), new Action(OpenDDoc));
		}

		void OpenDDoc()
		{
			var cl = IdeApp.Workbench.ActiveDocument.Editor.Caret.Location;
			var url=DDocumentationLauncher.GetReferenceUrl(res, ctxt, new CodeLocation(cl.Column, cl.Line));

			if (url != null)
				Refactoring.DDocumentationLauncher.LaunchRelativeDUrl(url);
		}

		void ImportSymbol()
		{
			SymbolImportRefactoring.CreateImportStatementForCurrentCodeContext();
		}

		void GotoDeclaration()
		{
			RefactoringCommandsExtension.GotoDeclaration(n);
		}

		public void FindReferences()
		{
			ReferenceFinding.StartReferenceSearchAsync(n);
		}

		void RenameSymbol()
		{
			new DRenameHandler().Start(n);
		}
	}
}
