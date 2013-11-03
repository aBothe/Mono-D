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
		AbstractType lastResult;
		INode firstResultNode;
		ResolutionContext lastContext;

		bool Update()
		{
			var lastResults = DResolverWrapper.ResolveHoveredCode(out lastContext);

			if (lastResults == null || lastResults.Length == 0)
			{
				firstResultNode = null;
				return false;
			}

			foreach (var r in lastResults)
				if ((firstResultNode = DResolver.GetResultMember (lastResult = r)) != null)
					return true;

			return false;
		}

		[CommandHandler(Refactoring.Commands.OpenDDocumentation)]
		void OpenDDocumentation()
		{
			if (!Update())
				return;

			var cl = IdeApp.Workbench.ActiveDocument.Editor.Caret.Location;
			var url = Refactoring.DDocumentationLauncher.GetReferenceUrl(lastResult, lastContext, new CodeLocation(cl.Column, cl.Line));

			if (url != null)
				Refactoring.DDocumentationLauncher.LaunchRelativeDUrl(url);
		}

		[CommandHandler(RefactoryCommands.FindReferences)]
		void FindReferences()
		{
			if(Update())
				ReferenceFinding.StartReferenceSearchAsync(firstResultNode);
		}

		[CommandHandler(RefactoryCommands.GotoDeclaration)]
		void GotoDeclaration()
		{
			if (Update ())
				GotoDeclaration (lastResult);
		}

		internal static void GotoDeclaration(AbstractType lastResult)
		{
			var n = DResolver.GetResultMember (lastResult);
			// Redirect to the actual definition on import bindings
			if (n is ImportSymbolAlias)
				n = DResolver.GetResultMember ((lastResult as DSymbol).Base);

			GotoDeclaration (n);
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
			if(Update ())
				new DRenameHandler().Start(firstResultNode);
		}

		[CommandHandler(RefactoryCommands.ImportSymbol)]
		void TryImportMissingSymbol()
		{
			SymbolImportRefactoring.CreateImportStatementForCurrentCodeContext();
		}
	}
}
