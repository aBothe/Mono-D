using System.Collections.Generic;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// UFCS: User function call syntax;
	/// A base expression will be used as a method'd first call parameter 
	/// so it looks like the first expression had a respective sub-method.
	/// Example:
	/// assert("fdas".reverse() == "asdf"); -- reverse() will be called with "fdas" as the first argument.
	/// 
	/// </summary>
	public class UFCSResolver
	{
		public static ResolveResult[] TryResolveUFCS(
			ResolveResult firstArgument, 
			PostfixExpression_Access acc, 
			ResolverContextStack ctxt)
		{
			if (ctxt == null)
				return null;
			
			var name="";

			if (acc.AccessExpression is IdentifierExpression)
				name = ((IdentifierExpression)acc.AccessExpression).Value as string;
			else if (acc.AccessExpression is TemplateInstanceExpression)
				name = ((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id;
			else
				return null;

			var methodMatches = new List<ResolveResult>();
			if(ctxt.ParseCache!=null)
				foreach (var pc in ctxt.ParseCache)
				{
					var tempResults=pc.UfcsCache.FindFitting(ctxt, acc.Location, firstArgument, name);

					if (tempResults != null)
						foreach (var m in tempResults)
						{
							var r = TypeDeclarationResolver.HandleNodeMatch(m, ctxt, firstArgument, acc);

							if (r is MemberResult)
							{
								((MemberResult)r).IsUFCSResult = true;
								methodMatches.Add(r);
							}
						}
				}

			return methodMatches.Count == 0 ? null : methodMatches.ToArray();
		}
	}
}
