using System.Collections.Generic;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;

namespace D_Parser.Completion
{
	/// <summary>
	/// Encapsules tooltip content.
	/// If there are more than one tooltip contents, there are more than one resolve results
	/// </summary>
	public class AbstractTooltipContent
	{
		public ResolveResult ResolveResult;
		public string Title;
		public string Description;
	}

	public class AbstractTooltipProvider
	{
		public static AbstractTooltipContent[] BuildToolTip(IEditorData Editor)
		{
			try
			{
				IStatement curStmt = null;
				var rr = DResolver.ResolveType(Editor,
					new ResolverContext
					{
						ScopedBlock = DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt),
						ScopedStatement = curStmt,
						ParseCache = Editor.ParseCache,
						ImportCache = Editor.ImportCache
					}, true, true);

				if (rr.Length < 1)
					return null;

				var l = new List<AbstractTooltipContent>(rr.Length);
				foreach (var res in rr)
					l.Add(BuildTooltipContent(res));

				return l.ToArray();
			}
			catch { }
			return null;
		}

		static AbstractTooltipContent BuildTooltipContent(ResolveResult res)
		{
			var modRes = res as ModuleResult;
			var memRes = res as MemberResult;
			var typRes = res as TypeResult;

			// Only show one description for items sharing descriptions
			string description = "";

			if (modRes != null)
				description = modRes.ResolvedModule.Description;
			else if (memRes != null)
				description = memRes.ResolvedMember.Description;
			else if (typRes != null)
				description = typRes.ResolvedTypeDefinition.Description;

			return new AbstractTooltipContent
			{
				ResolveResult = res,
				Title = (res is ModuleResult ? (res as ModuleResult).ResolvedModule.FileName : res.ToString()),
				Description = description
			};
		}
	}
}
