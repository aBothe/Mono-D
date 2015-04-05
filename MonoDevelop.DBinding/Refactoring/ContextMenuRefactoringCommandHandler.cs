using System;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Refactoring;
using System.Linq;
using MonoDevelop.Core;
using D_Parser.Refactoring;
using System.Collections.Generic;

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
			var t = dataItem as object[];
			if (t != null)
			{
				if (t.Length == 2)
				{
					if(RefactoryCommands.ImportSymbol.Equals(t[0]))
						ImportStmtCreation.GenerateImportStatementForNode(t[1] as INode, caps.ed, new TextDocumentAdapter(IdeApp.Workbench.ActiveDocument.Editor));
					if (RefactoryCommands.QuickFix.Equals (t [0])) {
						var n = t [1] as DNode;
						var textToGenerate = DNode.GetNodePath (n, false) + ".";

						caps.lastDoc.Editor.Insert (caps.lastDoc.Editor.LocationToOffset(caps.lastSyntaxObject.Location.Line, caps.lastSyntaxObject.Location.Column), textToGenerate);
					}
				}
			}
		}

		protected override void Update(CommandArrayInfo info)
		{
			if (caps.Update ()) {
				if (caps.resultResolutionAttempt != LooseResolution.NodeResolutionAttempt.RawSymbolLookup) {
					var refactoringMenu = new CommandInfoSet { Text = GettextCatalog.GetString ("Refactoring") };

					if(caps.lastResults.Any(t => t is DSymbol && DRenameRefactoring.CanRenameNode ((t as DSymbol).Definition)))
						refactoringMenu.CommandInfos.Add (IdeApp.CommandService.GetCommandInfo (EditCommands.Rename), new Action (caps.RenameSymbol));

					if (refactoringMenu.CommandInfos.Count > 0)
						info.Add (refactoringMenu);

					info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.GotoDeclaration), new Action (caps.GotoDeclaration));
					info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.FindReferences), new Action (() => caps.FindReferences (false)));

					if (caps.lastResults.Any (t => t is DSymbol && (t as DSymbol).Definition.Parent is DClassLike))
						info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.FindAllReferences), new Action (() => caps.FindReferences (true)));

					if (caps.lastResults.Any (t => {
						var ds = DResolver.StripMemberSymbols (t);
						return ds is ClassType || ds is InterfaceType;
					}))
						info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.FindDerivedClasses), new Action (caps.FindDerivedClasses));
				} else {
					var importSymbolMenu = new CommandInfoSet { Text = GettextCatalog.GetString ("Resolve") };

					foreach (var m in caps.GetImportableModulesForLastResults()) {

						importSymbolMenu.CommandInfos.Add (new CommandInfo {
							Text = "import " + AbstractNode.GetNodePath (m, true) + ";", 
							Icon = MonoDevelop.Ide.Gui.Stock.AddNamespace
						}, new object[]{ RefactoryCommands.ImportSymbol, m });
					}

					if (importSymbolMenu.CommandInfos.Count > 0) {
						importSymbolMenu.CommandInfos.AddSeparator ();

						var alreadyAddedItems = new List<DModule> ();
						foreach (var t in caps.lastResults) {
							var ds = t as DSymbol;
							if (ds == null)
								continue;

							var m = ds.Definition.NodeRoot as DModule;
							if (m != null && !alreadyAddedItems.Contains (m)) {
								alreadyAddedItems.Add (m);

								importSymbolMenu.CommandInfos.Add (new CommandInfo{
									Text = AbstractNode.GetNodePath(ds.Definition, true)
								}, new object[] { RefactoryCommands.QuickFix, ds.Definition });
							}
						}


						// To explicitly show the Ctrl+Alt+Space hint.
						importSymbolMenu.CommandInfos.AddSeparator ();
						importSymbolMenu.CommandInfos.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.ImportSymbol), new Action (caps.TryImportMissingSymbol));

						info.Add (importSymbolMenu);
					}
				}
			}

			if(SortImportsCommandHandler.CanSortImports(caps.lastDoc))
				info.Add (IdeApp.CommandService.GetCommandInfo (RefactoryCommands.SortImports), new Action(()=>SortImportsCommandHandler.SortImports(caps.lastDoc)));
		}
	}
}
