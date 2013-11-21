using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Components.Commands;
using MonoDevelop.Refactoring;
using MonoDevelop.Ide.Commands;

namespace MonoDevelop.D.Refactoring
{
	public class RefactoringCommandsExtension : TextEditorExtension
	{
		readonly RefactoringCommandCapsule caps = new RefactoringCommandCapsule();

		bool Update()
		{
			caps.lastResults = MonoDevelop.D.Resolver.DResolverWrapper.ResolveHoveredCode(out caps.ctxt, out caps.ed, Document);

			return caps.lastResults != null && caps.lastResults.Length > 0;
		}

		[CommandHandler(Refactoring.Commands.OpenDDocumentation)]
		void OpenDDocumentation()
		{
			if (Update ())
				caps.OpenDDoc ();
		}

		[CommandHandler(RefactoryCommands.FindReferences)]
		void FindReferences()
		{
			if(Update())
				caps.FindReferences (false);
		}

		[CommandHandler(RefactoryCommands.FindAllReferences)]
		void FindAllReferences()
		{
			if (Update())
				caps.FindReferences(true);
		}

		[CommandHandler(RefactoryCommands.FindDerivedClasses)]
		void FindDerivedClasses()
		{
			if (Update())
				caps.FindDerivedClasses();
		}

		[CommandHandler(RefactoryCommands.GotoDeclaration)]
		void GotoDeclaration()
		{
			if(Update())
				caps.GotoDeclaration ();
		}

		[CommandUpdateHandler(Refactoring.Commands.OpenDDocumentation)]
		[CommandUpdateHandler(EditCommands.Rename)]
		[CommandUpdateHandler(RefactoryCommands.FindReferences)]
		[CommandUpdateHandler(RefactoryCommands.FindAllReferences)]
		[CommandUpdateHandler(RefactoryCommands.FindDerivedClasses)]
		[CommandUpdateHandler(RefactoryCommands.GotoDeclaration)]
		[CommandUpdateHandler(RefactoryCommands.ImportSymbol)]
		void Rename_Update(CommandInfo ci)
		{
			ci.Bypass = false;
		}

		[CommandHandler(EditCommands.Rename)]
		void Rename()
		{
			if(Update())
				caps.RenameSymbol ();
		}

		[CommandHandler(RefactoryCommands.ImportSymbol)]
		void TryImportMissingSymbol()
		{
			if(!Update())
				caps.TryImportMissingSymbol ();
		}
	}
}
