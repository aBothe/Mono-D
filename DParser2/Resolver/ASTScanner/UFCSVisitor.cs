using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using System.Diagnostics;

namespace D_Parser.Resolver.ASTScanner
{
	public class UFCSVisitor : AbstractVisitor
	{
		public UFCSVisitor(ResolverContextStack ctxt) : base(ctxt) { }

		/// <summary>
		/// If null, this filter will be bypassed
		/// </summary>
		public string NameToSearch;
		public ResolveResult FirstParamToCompareWith;

		public List<DMethod> Matches=new List<DMethod>();

		protected override bool HandleItem(INode n)
		{
			if ((NameToSearch == null ? !string.IsNullOrEmpty(n.Name) : n.Name == NameToSearch) && 
				n is DMethod)
			{
				var dm = (DMethod)n;

				if (dm.Parameters.Count != 0)
				{
					var firstParam = TypeResolution.TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, Context);

					//TODO: Compare the resolved parameter with the first parameter given
					if (true)
					{
						Matches.Add(dm);
					}
				}
			}

			return false;
		}
	}
}
