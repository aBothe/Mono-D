using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver
{
	public class NameScan : RootsEnum
	{
		string filterId;
		public List<INode> Matches = new List<INode>();

		NameScan(ResolverContext ctxt) : base(ctxt) { }

		public static IEnumerable<INode> SearchMatchesAlongNodeHierarchy(ResolverContext ctxt, CodeLocation caret, string name)
		{
			var scan = new NameScan(ctxt) { filterId=name };

			scan.IterateThroughScopeLayers(caret);

			if (ctxt.ParseCache != null)
				foreach (var mod in ctxt.ParseCache)
				{
					var modNameParts = mod.ModuleName.Split('.');

					if (modNameParts[0] == name)
						scan.Matches.Add(mod);
				}

			return scan.Matches;
		}

		protected override void HandleItem(INode n)
		{
			if (n != null && n.Name == filterId)
				Matches.Add(n);
		}

		/// <summary>
		/// Scans through the node. Also checks if n is a DClassLike or an other kind of type node and checks their specific child and/or base class nodes.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="name"></param>
		/// <param name="parseCache">Needed when trying to search base classes</param>
		/// <returns></returns>
		public static INode[] ScanNodeForIdentifier(IBlockNode curScope, string name, ResolverContext ctxt)
		{
			var matches = new List<INode>();

			if (curScope.Count > 0)
				foreach (var n in curScope)
				{
					// Scan anonymous enums
					if (n is DEnum && n.Name == "")
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
				var baseClasses = DResolver.ResolveBaseClass(curScope as DClassLike, ctxt);
				if (baseClasses != null)
					foreach (var i in baseClasses)
					{
						var baseClass = i as TypeResult;
						if (baseClass == null)
							continue;
						// Search for items called name in the base class(es)
						var r = ScanNodeForIdentifier(baseClass.ResolvedTypeDefinition, name, ctxt);

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
			if (curScope is DNode && (curScope as DNode).TemplateParameters != null)
				foreach (var ch in (curScope as DNode).TemplateParameters)
				{
					if (name == ch.Name)
						matches.Add(new TemplateParameterNode(ch));
				}

			return matches.Count > 0 ? matches.ToArray() : null;
		}
	}
}
