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
		public ISemantic ResolveResult;
		public string Title;
		public string Description;
	}

	public class AbstractTooltipProvider
	{
		public static AbstractTooltipContent[] BuildToolTip(IEditorData Editor)
		{
			try
			{
				var ctxt=ResolverContextStack.Create(Editor);
				// In the case we've got a method or something, don't return its base type, only the reference to it
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
				var rr = DResolver.ResolveType(Editor, ctxt, DResolver.AstReparseOptions.AlsoParseBeyondCaret | DResolver.AstReparseOptions.OnlyAssumeIdentifierList);

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

		static AbstractTooltipContent BuildTooltipContent(ISemantic res)
		{
			// Only show one description for items sharing descriptions
			string description = res is DSymbol ? ((DSymbol)res).Definition.Description : "";

			return new AbstractTooltipContent
			{
				ResolveResult = res,
				Title = (res is ModuleSymbol ? ((ModuleSymbol)res).Definition.FileName : res.ToString()),
				Description = description
			};
		}
	}
}
