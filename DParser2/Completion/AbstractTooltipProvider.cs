using System.Collections.Generic;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

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
				var rr = DResolver.ResolveType(Editor, DResolver.AstReparseOptions.AlsoParseBeyondCaret | DResolver.AstReparseOptions.OnlyAssumeIdentifierList);

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
				description = modRes.Module.Description;
			else if (memRes != null)
				description = memRes.Node.Description;
			else if (typRes != null)
				description = typRes.Node.Description;

			return new AbstractTooltipContent
			{
				ResolveResult = res,
				Title = (res is ModuleResult ? (res as ModuleResult).Module.FileName : res.ToString()),
				Description = description
			};
		}
	}
}
