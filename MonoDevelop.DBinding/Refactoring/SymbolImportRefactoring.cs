using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Misc;
using MonoDevelop.Ide;
using MonoDevelop.D.Building;
using MonoDevelop.D.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver;
using D_Parser.Dom;

namespace MonoDevelop.D.Refactoring
{
	public class SymbolImportRefactoring
	{
		public static void CreateImportStatementForCurrentCodeContext()
		{
			/*
			 * 1) Get currently selected symbol
			 * 2) Enum through all parse caches to find first occurrence of this symbol
			 * 3) Find the current module's first import statement (or take a location after the module statement / take (0,0) as the statement location)
			 * 4) Create import statement
			 */

			// 1)
			var edData=DResolverWrapper.GetEditorData();
			var o = DResolver.GetScopedCodeObject(edData);

			// 2)

			var name = "";
			if (o is ITypeDeclaration)
			{
				
			}
		}
	}
}
