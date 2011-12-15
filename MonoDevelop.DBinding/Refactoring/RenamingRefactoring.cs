using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver;

namespace MonoDevelop.D.Refactoring
{
	public class RenamingRefactoring
	{
		INode n;

		public void Run(DProject project,ResolveResult renamedNodeResult)
		{
			// Get resolved member/type definition node
			n = null;

			if (renamedNodeResult is MemberResult)
				n = (renamedNodeResult as MemberResult).ResolvedMember;
			else if (renamedNodeResult is TypeResult)
				n = (renamedNodeResult as TypeResult).ResolvedTypeDefinition;
			else if (renamedNodeResult is ModuleResult)
				n = (renamedNodeResult as ModuleResult).ResolvedModule;

			if (n == null)
				return;

			// Enumerate references


			// Prepare current editor (setup textlinks and anchors)

			// Show rename helper popup

			// If user accepted rename, check other docs for further modifications
		}
	}
}
