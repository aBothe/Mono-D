using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ASTScanner
{
	public class NameScan : AbstractVisitor
	{
		string filterId;
		public List<INode> Matches = new List<INode>();

		NameScan(ResolverContextStack ctxt) : base(ctxt) { }

		public static IEnumerable<INode> SearchMatchesAlongNodeHierarchy(ResolverContextStack ctxt, CodeLocation caret, string name)
		{
			var scan = new NameScan(ctxt) { filterId=name };

			scan.IterateThroughScopeLayers(caret);

			return scan.Matches;
		}

		protected override bool HandleItem(INode n)
		{
            if (n != null && n.Name == filterId)
            {
                Matches.Add(n);

                if (Context.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch))
                    return true;
            }

            /*
             * Can't tell if workaround .. or just nice idea:
             * 
             * To still be able to show sub-packages e.g. when std. has been typed,
             * take the first import that begins with std.
             * In HandleNodeMatch, it'll be converted to a module package result then.
             */
            else if (n is IAbstractSyntaxTree)
            {
                var modName = ((IAbstractSyntaxTree)n).ModuleName;
                if (modName.Split('.')[0] == filterId)
                {
                    bool canAdd = true;

                    foreach (var m in Matches)
                        if (m is IAbstractSyntaxTree)
                        {
                            canAdd = false;
                            break;
                        }

                    if (canAdd)
                    {
                        Matches.Add(n);

                        if (Context.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch))
                            return true;
                    }
                }
            }

            return false;
		}

		/// <summary>
		/// Scans through the node. Also checks if n is a DClassLike or an other kind of type node and checks their specific child and/or base class nodes.
		/// </summary>
		/// <param name="parseCache">Needed when trying to search base classes</param>
		public static INode[] ScanNodeForIdentifier(IBlockNode curScope, string name, ResolverContextStack ctxt)
		{
			var matches = new List<INode>();

			if (curScope.Count > 0)
				foreach (var n in curScope)
				{
					// Scan anonymous enums
					if (n is DEnum && string.IsNullOrEmpty(n.Name))
					{
						foreach (var k in n as DEnum)
							if (k.Name == name)
								matches.Add(k);
					}

					if (n.Name == name)
						matches.Add(n);
				}

			// If our current Level node is a class-like, also attempt to search in its baseclass!
			if (curScope is DClassLike)
			{
				var tr = new TypeResult { Node=(DClassLike)curScope };
				DResolver.ResolveBaseClasses(tr, ctxt, true);
				if (tr.BaseClass != null)
					foreach (var i in tr.BaseClass)
					{
						if (i == null)
							continue;
						// Search for items called name in the base class(es)
						var r = ScanNodeForIdentifier((IBlockNode)i.Node, name, ctxt);

						if (r != null)
							matches.AddRange(r);
					}
			}

			// Check parameters
			if (curScope is DMethod)
			{
				var dm = curScope as DMethod;
				foreach (var ch in dm.Parameters)
				{
					if (name == ch.Name)
						matches.Add(ch);
				}
			}

			// and template parameters
			if (curScope is DNode && ((DNode)curScope).TemplateParameters != null)
				foreach (var ch in ((DNode)curScope).TemplateParameters)
				{
					if (name == ch.Name)
						matches.Add(new TemplateParameterNode(ch) { Owner = (DNode)curScope });
				}

			return matches.Count > 0 ? matches.ToArray() : null;
		}
	}
}
