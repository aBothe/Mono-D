using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Components.Commands;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Refactoring;
using MonoDevelop.Ide.TypeSystem;
using System;

namespace MonoDevelop.D.Refactoring
{
	public class RefactoringCommandsExtension : TextEditorExtension
	{
		AbstractType[] lastResults;
		INode firstResultNode;
		ResolutionContext lastContext;

		[CommandHandler(Refactoring.Commands.OpenDDocumentation)]
		void OpenDDocumentation()
		{
			var url = Refactoring.DDocumentationLauncher.GetReferenceUrl();

			if (url != null)
				Refactoring.DDocumentationLauncher.LaunchRelativeDUrl(url);
		}

		[CommandUpdateHandler(EditCommands.Rename)]
		[CommandUpdateHandler(RefactoryCommands.GotoDeclaration)]
		[CommandUpdateHandler(RefactoryCommands.FindReferences)]
		void FindReferences_Upd(CommandInfo ci)
		{
			lastResults = DResolverWrapper.ResolveHoveredCode(out lastContext);

			if (lastResults == null || lastResults.Length == 0)
			{
				ci.Bypass = true;
				return;
			}

			ci.Bypass = true;

			foreach(var r in lastResults)
				if ((firstResultNode = DResolver.GetResultMember(r)) != null)
				{
					ci.Bypass = false;
					return;
				}
		}

		[CommandHandler(RefactoryCommands.FindReferences)]
		void FindReferences()
		{
			ReferenceFinding.StartReferenceSearchAsync(firstResultNode);
		}

		[CommandHandler(RefactoryCommands.GotoDeclaration)]
		void GotoDeclaration()
		{
			GotoDeclaration(firstResultNode);
		}

		public static void GotoDeclaration(INode n)
		{
			if (n != null && n.NodeRoot is DModule)
				IdeApp.Workbench.OpenDocument(
					((DModule)n.NodeRoot).FileName,
					n.Location.Line,
					n.Location.Column);
		}

		[CommandHandler(EditCommands.Rename)]
		void Rename()
		{
			new DRenameHandler().Start(firstResultNode);
		}

		[CommandHandler(RefactoryCommands.ImportSymbol)]
		void TryImportMissingSymbol()
		{
			SymbolImportRefactoring.CreateImportStatementForCurrentCodeContext();
		}
	}
}
