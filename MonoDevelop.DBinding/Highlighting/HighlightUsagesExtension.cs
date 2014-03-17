using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using Mono.TextEditor;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.D.Parser;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.D.Projects;

// Code taken and modified from MonoDevelop.CSharp.Highlighting.HighlightUsagesExtension.cs
using D_Parser.Completion;
using MonoDevelop.SourceEditor;
using ICSharpCode.NRefactory;

namespace MonoDevelop.D.Highlighting
{
	class HighlightUsagesExtension : AbstractUsagesExtension<Tuple<ResolutionContext, DSymbol>>
	{
		public DModule SyntaxTree
		{
			get { return Document.ParsedDocument != null ? (Document.ParsedDocument as ParsedDModule).DDom : null; }
		}

		public override void Initialize()
		{
			base.Initialize();

			// Enable proper semantic highlighting because the syntaxmode won't be given the editor doc by default.
			var sm = Document.Editor.Document.SyntaxMode as DSyntaxMode;
			if (sm != null)
				sm.GuiDocument = Document;
		}

		protected override bool TryResolve(out Tuple<ResolutionContext, DSymbol> resolveResult)
		{
			ResolutionContext lastCtxt;
			IEditorData ed;
			var rr = DResolverWrapper.ResolveHoveredCode(out lastCtxt, out ed, Document);

			if (rr == null || rr.Length < 1 || !(rr[0] is DSymbol))
			{
				resolveResult = null;
				return false;
			}

			resolveResult = new Tuple<ResolutionContext, DSymbol>(lastCtxt, rr[0] as DSymbol);

			return true;
		}

		protected override IEnumerable<Ide.FindInFiles.MemberReference> GetReferences(Tuple<ResolutionContext, DSymbol> tup, System.Threading.CancellationToken token)
		{
			var ctxt = tup.Item1;
			ctxt.Cancel = token;
			var mr = tup.Item2;

			var referencedNode = mr.Definition;

			// Slightly hacky: To keep highlighting the id of e.g. a NewExpression, take the ctor's parent node (i.e. the class node)
			if (referencedNode is DMethod && ((DMethod)referencedNode).SpecialType == DMethod.MethodType.Constructor)
			{
				mr = mr.Base as DSymbol;
				if (mr == null)
					yield break;
				referencedNode = mr.Definition;
			}


			var parseCache = Document.HasProject ?
						(Document.Project as AbstractDProject).ParseCache :
						DCompilerService.Instance.GetDefaultCompiler().GenParseCacheView();

			IEnumerable<ISyntaxRegion> refs = null;
			try{
				refs = D_Parser.Refactoring.ReferencesFinder.Scan(SyntaxTree, referencedNode, ctxt);
			}
			catch (Exception ex)
			{
				LoggingService.LogInfo("Error during highlighting usages", ex);
			}
			
			if(refs != null)
				foreach (var sym in refs)
					yield return new Ide.FindInFiles.MemberReference(sym,
						new ICSharpCode.NRefactory.TypeSystem.DomRegion(Document.FileName, sym.Location.Line, sym.Location.Column, sym.EndLocation.Line, sym.EndLocation.Column),
						Document.Editor.LocationToOffset(sym.Location.Line, sym.Location.Column), sym.EndLocation.Column - sym.Location.Column);
		}
	}
}
