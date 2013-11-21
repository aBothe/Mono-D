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
using MonoDevelop.Core;
using D_Parser.Refactoring;

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
				(dataItem as Action) ();
			else if (dataItem is INode) // Currently only used when importing missing symbols
				ImportDirectiveCreator.GenerateImportStatementForNode (dataItem as INode, caps.ed, (loc, s) => {
					var doc = IdeApp.Workbench.ActiveDocument.Editor;
					doc.Insert(doc.LocationToOffset(loc.Line,loc.Column), s);
				});
		}

		protected override void Update(CommandArrayInfo info)
		{
			caps.lastResults = Resolver.DResolverWrapper.ResolveHoveredCode(out caps.ctxt, out caps.ed);

			// Find Member Overloads
			bool renameable = false;
			if (caps.lastResults != null && caps.lastResults.Any ((t) => {
				var n = DResolver.GetResultMember (t);
				renameable = DRenameRefactoring.CanRenameNode (n);
				return n != null;								
			})) 
			{
				var refactoringMenu = new CommandInfoSet { Text = GettextCatalog.GetString ("Refactoring") };

				if (renameable)
					refactoringMenu.CommandInfos.Add (IdeApp.CommandService.GetCommandInfo (EditCommands.Rename), new Action (caps.RenameSymbol));

				if(refactoringMenu.CommandInfos.Count > 0)
					info.Add (refactoringMenu);

				info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.GotoDeclaration), new Action (caps.GotoDeclaration));
				info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.FindReferences), new Action (caps.FindReferences));
			}
			else 
			{
				bool _u;
				var nodes = ImportDirectiveCreator.TryFindingSelectedIdImportIndependently (caps.ed, out _u, false);

				if (nodes.Length > 0) {
					var importSymbolMenu = new CommandInfoSet { Text = GettextCatalog.GetString("Resolve") };

					foreach(var n in nodes)
						importSymbolMenu.CommandInfos.Add(new CommandInfo{
							Text = DNode.GetNodePath(n, true), 
								Icon = MonoDevelop.Ide.Gui.Stock.AddNamespace },n);

					// To explicitly show the Ctrl+Alt+Space hint.
					importSymbolMenu.CommandInfos.AddSeparator ();
					importSymbolMenu.CommandInfos.Add (IdeApp.CommandService.GetCommandInfo(RefactoryCommands.ImportSymbol), new Action(caps.TryImportMissingSymbol));

					info.Add (importSymbolMenu);
				}
			}

			info.Add(IdeApp.CommandService.GetCommandInfo (Commands.OpenDDocumentation), new Action(caps.OpenDDoc));
		}
	}
}
