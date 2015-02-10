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
			get { 
				return Document.GetDAst ();
			}
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
			ctxt.CancelOperation = false;
			token.Register(()=>ctxt.CancelOperation = true);
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

			IEnumerable<ISyntaxRegion> refs = null;

			CodeCompletion.DoTimeoutableCompletionTask (null, ctxt, () => {
				try {
					refs = D_Parser.Refactoring.ReferencesFinder.Scan (SyntaxTree, referencedNode, ctxt);
				} catch (Exception ex) {
					LoggingService.LogInfo ("Error during highlighting usages", ex);
				}
			});
			
			if(refs != null)
				foreach (var sr in refs)
				{
					CodeLocation loc;
					int len;
					if (sr is INode)
					{
						loc = (sr as INode).NameLocation;
						len = (sr as INode).Name.Length;
					}
					else if (sr is TemplateParameter)
					{
						loc = (sr as TemplateParameter).NameLocation;
						len = (sr as TemplateParameter).Name.Length;
					}
					else
					{
						loc = sr.Location;
						len = sr.EndLocation.Column - loc.Column;
					}

					yield return new Ide.FindInFiles.MemberReference(sr,
						new ICSharpCode.NRefactory.TypeSystem.DomRegion(Document.FileName, loc.Line, loc.Column, loc.Line, loc.Column + len),
						Document.Editor.LocationToOffset(loc.Line, loc.Column), len);
				}
		}
	}
}
