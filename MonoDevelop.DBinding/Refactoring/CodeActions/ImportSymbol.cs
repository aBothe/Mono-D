using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Refactoring;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using Gtk;
using MonoDevelop.CodeActions;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Refactoring.CodeActions
{
    /// <summary>
    /// Provides a CodeAction for fast import in D language
    /// </summary>
    public class ImportSymbol : CodeActionProvider
    {
        public ImportSymbol()
        {
            this.Title = "Import symbol";
            this.MimeType = "text/x-d";
            this.Description = "";
        }

        public override IEnumerable<CodeAction> GetActions(Ide.Gui.Document document, ICSharpCode.NRefactory.TextLocation loc, System.Threading.CancellationToken cancellationToken)
        {
            if (!DLanguageBinding.IsDFile(document.FileName)) 
				yield break;

            var imports = GetSolutions(document); //it may be a bit too slow
            foreach(var i in imports)
                yield return new InnerAction(i);
        }

        /// <summary>
        /// Returns possible imports for current context
        /// </summary>
        IEnumerable<string> GetSolutions(Document doc)
        {
            //i had to copy-paste some code from other files
            ResolverContextStack ctxt;
            var rr = Resolver.DResolverWrapper.ResolveHoveredCode(out ctxt);
            if (rr != null && rr.Length > 0)
            {
                var res = rr[rr.Length - 1];
                var n = DResolver.GetResultMember(res);
                if (n != null)
                {
                    //seems like this can filter function calls
                    yield break;
                }
            }

            INode[] nodes;
            bool req;
            var edData = DResolverWrapper.GetEditorData(doc);
            try
            {
                nodes = ImportDirectiveCreator.TryFindingSelectedIdImportIndependently(edData, out req);
            }
            catch
            {
                yield break;
            }
            if(!req || nodes == null) yield break;
            foreach (var node in nodes)
            {
                var mod = node.NodeRoot as IAbstractSyntaxTree;
                if (mod != null && mod != edData.SyntaxTree)
                {
                    yield return mod.ModuleName;
                }
            }
            yield break;
        }

        /// <summary>
        /// Inserts import statement into correct place
        /// </summary>
        static void ApplySolution(string import, Document doc)
        {
            var eData = DResolverWrapper.GetEditorData(doc);
            var loc = new CodeLocation(0, DParser.FindLastImportStatementEndLocation(eData.SyntaxTree, eData.ModuleCode).Line + 1);
            doc.Editor.Insert(doc.Editor.GetLine(loc.Line).Offset, import.Trim() + doc.Editor.EolMarker);
            
            //IOInterface.InsertIntoCode(loc, "import " + mod.ModuleName + ";\n");
        }

        /// <summary>
        /// Represents position in menu of one possible import
        /// </summary>
        class InnerAction : CodeAction
        {
            public InnerAction(string title)
            {
                this.Title = " import " + title + ";";

            }
            public override void Run(Ide.Gui.Document document, ICSharpCode.NRefactory.TextLocation loc)
            {
                ApplySolution(Title, document);
            }
        }
    }
}
