using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Components.Commands;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Refactoring;

namespace MonoDevelop.D.Refactoring
{
	public class RefactoringCommandsExtension : TextEditorExtension
	{
		ResolveResult[] lastResults;
		INode firstResultNode;
		ResolverContextStack lastContext;

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
			if(n!=null)
				IdeApp.Workbench.OpenDocument(
					((IAbstractSyntaxTree)n.NodeRoot).FileName,
					n.StartLocation.Line,
					n.StartLocation.Column, OpenDocumentOptions.Default);
		}

		[CommandHandler(EditCommands.Rename)]
		void Rename()
		{
			new RenamingRefactoring().Run(IdeApp.Workbench.ActiveDocument.HasProject ?
					IdeApp.Workbench.ActiveDocument.Project as DProject : null, firstResultNode);
		}

		[CommandHandler(RefactoryCommands.ImportSymbol)]
		void TryImportMissingSymbol()
		{
			SymbolImportRefactoring.CreateImportStatementForCurrentCodeContext();
		}
	}
}
