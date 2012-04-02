using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using System.Threading;

namespace D_Parser.Completion.Providers
{
	/// <summary>
	/// Adds method items to the completion list if the current expression's type is matching the methods' first parameter
	/// </summary>
	public class UFCSCompletionProvider
	{
		public static void Generate(ResolveResult rr, ResolverContextStack ctxt, IEditorData ed, ICompletionDataGenerator gen)
		{
			/*
			 * 1) Have visitor.
			 * 2) Iterate through scope levels.
			 * 3) Check if node is Method, containing at least 1 parameter
			 * 4) Get the first parameter's type.
			 * 5) Compare it to the base expression wrapped by 'rr'
			 *	-- Result comparison!?
			 * 6) Add to completion list if comparison successful
			 */

			// 1)
			var vis = new UFCSVisitor(ctxt)
			{ 
				FirstParamToCompareWith=rr,
				WorkAsync=true
			};

			// 2), 3), 4), 5)
			vis.IterateThroughScopeLayers(ed.CaretLocation);


			// 6)
			if (vis.Matches.Count != 0)
				foreach (var m in vis.Matches)
					gen.Add(m);
		}
	}
}
