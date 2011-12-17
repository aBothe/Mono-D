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

namespace MonoDevelop.D.Refactoring
{
	public enum Commands
	{
		ContextMenuRefactoringCommands,

		GotoDeclaration,
		FindReferences,
		RenameSymbols,
	}

	public class ContextMenuRefactoringCommandHandler : CommandHandler
	{
		ISearchProgressMonitor monitor;
		ResolverContext ctxt;
		ResolveResult res;
		INode n;

		protected override void Run(object dataItem)
		{
			if (dataItem is Action)
				(dataItem as Action)();
		}

		IAbstractSyntaxTree Module
		{
			get { return n.NodeRoot as IAbstractSyntaxTree; }
		}

		protected override void Update(CommandArrayInfo info)
		{
			var rr = Resolver.DResolverWrapper.ResolveHoveredCode(out ctxt);

			if (rr == null || rr.Length < 1)
				return;

			res = rr[rr.Length - 1];
			
			n = Resolver.DResolverWrapper.GetResultMember(res);

			if (n != null)
			{
				info.Add(IdeApp.CommandService.GetCommandInfo(Commands.GotoDeclaration), new Action(GotoDeclaration));
				info.Add(IdeApp.CommandService.GetCommandInfo(Commands.FindReferences), new Action(FindReferences));
				info.AddSeparator();
				info.Add(IdeApp.CommandService.GetCommandInfo(Commands.RenameSymbols), new Action(RenameSymbol));
			}
		}


		void GotoDeclaration()
		{
			IdeApp.OpenFiles(new[] { 
						new FileOpenInformation(
							Module.FileName, 
							n.StartLocation.Line, 
							n.StartLocation.Column, 
							OpenDocumentOptions.BringToFront | OpenDocumentOptions.HighlightCaretLine)
			});
		}

		public void FindReferences()
		{
			monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor(true, true);
			ThreadPool.QueueUserWorkItem(FindReferencesThread);
		}

		void FindReferencesThread(object state)
		{
			try
			{
				foreach (var sr in DReferenceFinder.FindReferences(
					IdeApp.Workbench.ActiveDocument.HasProject?
					IdeApp.Workbench.ActiveDocument.Project as DProject:null, 
					n, monitor))
					monitor.ReportResult(sr);
			}
			catch (Exception ex)
			{
				if (monitor != null)
					monitor.ReportError("Error finding references", ex);
				else
					LoggingService.LogError("Error finding references", ex);
			}
			finally
			{
				if (monitor != null)
					monitor.Dispose();
			}
		}

		void RenameSymbol()
		{
			new RenamingRefactoring().Run(IdeApp.Workbench.ActiveDocument.HasProject ?
					IdeApp.Workbench.ActiveDocument.Project as DProject : null,n);
		}
	}
}
