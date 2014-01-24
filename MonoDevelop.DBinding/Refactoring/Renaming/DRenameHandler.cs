// by Alexander Bothe (info@alexanderbothe.com)
using MonoDevelop.Refactoring;

namespace MonoDevelop.D.Refactoring
{
	public class DRenameHandler : AbstractRefactoringCommandHandler
	{
		protected override void Update (RefactoringOptions options, MonoDevelop.Components.Commands.CommandInfo info)
		{
			var renameRefactoring = new DRenameRefactoring ();
			if (!renameRefactoring.IsValid (options))
				info.Bypass = true;
		}

		/// <param name="data">must be a DSymbol</param>
		protected override void Run(object data)
		{
			var doc = Ide.IdeApp.Workbench.ActiveDocument;

			if(doc!=null)
				this.Run(new RefactoringOptions(doc) { SelectedItem = data });
		}
		
		protected override void Run (RefactoringOptions options)
		{
			var renameRefactoring = new DRenameRefactoring ();
			if (renameRefactoring.IsValid (options))
				renameRefactoring.Run (options);
		}
	}
}

