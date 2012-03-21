using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Core;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.FindInFiles;
using System.Threading;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Building;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Refactoring;
using MonoDevelop.Ide.Commands;

namespace MonoDevelop.D.Refactoring
{
	public enum Commands
	{
		ContextMenuRefactoringCommands,
		OpenDDocumentation,
	}

	public class ContextMenuRefactoringCommandHandler : CommandHandler
	{
		ResolveResult res;
		INode n;

		protected override void Run(object dataItem)
		{
			if (dataItem is Action)
				(dataItem as Action)();
		}

		protected override void Update(CommandArrayInfo info)
		{
			ResolverContextStack ctxt;
			var rr = Resolver.DResolverWrapper.ResolveHoveredCode(out ctxt);

			if (rr != null && rr.Length > 0)
			{
				res = rr[rr.Length - 1];

				n = DResolver.GetResultMember(res);

				if (n != null)
				{
					info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.GotoDeclaration), new Action(GotoDeclaration));
					info.Add(IdeApp.CommandService.GetCommandInfo(RefactoryCommands.FindReferences), new Action(FindReferences));

					if (RenamingRefactoring.CanRename(n))
					{
						info.AddSeparator();
						info.Add(IdeApp.CommandService.GetCommandInfo(EditCommands.Rename), new Action(RenameSymbol));
					}
				}
			}
			info.Add(IdeApp.CommandService.GetCommandInfo(Commands.OpenDDocumentation), new Action(OpenDDoc));
		}

		void OpenDDoc()
		{
			var url=DDocumentationLauncher.GetReferenceUrl();

			if (url != null)
				Refactoring.DDocumentationLauncher.LaunchRelativeDUrl(url);
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
			new RenamingRefactoring().Run(IdeApp.Workbench.ActiveDocument.HasProject ?
					IdeApp.Workbench.ActiveDocument.Project as DProject : null,n);
		}
	}
}
