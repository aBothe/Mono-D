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

namespace MonoDevelop.D.Refactoring
{
	public enum Commands
	{
		ContextMenuRefactoringCommands,

		GotoDeclaration,
		FindReferences,
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

		public static ResolveResult[] ResolveHoveredCode(out ResolverContext ResolverContext)
		{
			ResolverContext = null;
			// Editor property preparations
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null || doc.FileName == FilePath.Null || IdeApp.ProjectOperations.CurrentSelectedSolution == null)
				return null;

			var editor = doc.GetContent<ITextBuffer>();
			if (editor == null)
				return null;

			int line, column;
			editor.GetLineColumnFromPosition(editor.CursorPosition, out line, out column);


			var ast = doc.ParsedDocument as ParsedDModule;

			var Project = doc.Project as DProject;
			var SyntaxTree = ast.DDom;

			if (SyntaxTree == null)
				return null;

			// Encapsule editor data for resolving
			var edData = new EditorData
			{
				CaretLocation = new CodeLocation(column, line),
				CaretOffset = editor.CursorPosition,
				ModuleCode = editor.Text,
				SyntaxTree = SyntaxTree as DModule,
				ParseCache = Project.ParsedModules,
				ImportCache = DResolver.ResolveImports(SyntaxTree as DModule, Project.ParsedModules)
			};

			// Resolve the hovered piece of code
			IStatement stmt = null;
			return DResolver.ResolveType(edData,
				ResolverContext=new ResolverContext
				{
					ParseCache = edData.ParseCache,
					ImportCache = edData.ImportCache,
					ScopedBlock = DResolver.SearchBlockAt(SyntaxTree, edData.CaretLocation, out stmt),
					ScopedStatement = stmt
				},
				true, true);
		}

		IAbstractSyntaxTree Module
		{
			get { return n.NodeRoot as IAbstractSyntaxTree; }
		}

		protected override void Update(CommandArrayInfo info)
		{
			var rr = ResolveHoveredCode(out ctxt);

			if (rr == null || rr.Length < 1)
				return;

			res = rr[rr.Length - 1];
			
			// Get resolved member/type definition node
			n = null;

			if (res is MemberResult)
				n = (res as MemberResult).ResolvedMember;
			else if (res is TypeResult)
				n = (res as TypeResult).ResolvedTypeDefinition;
			else if (res is ModuleResult)
				n = (res as ModuleResult).ResolvedModule;

			if (n != null)
			{
				info.Add(IdeApp.CommandService.GetCommandInfo(Commands.GotoDeclaration), new Action(GotoDeclaration));
				info.Add(IdeApp.CommandService.GetCommandInfo(Commands.FindReferences), new Action(FindReferences));
			}
		}


		void GotoDeclaration()
		{
			IdeApp.OpenFiles(new[] { 
						new FileOpenInformation(Module.FileName, n.StartLocation.Line, n.StartLocation.Column, OpenDocumentOptions.HighlightCaretLine)
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
				foreach (var sr in ReferenceFinder.FindReferences(IdeApp.ProjectOperations.CurrentSelectedSolution, n, monitor))
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
	}
}
