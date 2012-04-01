using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Dom;

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
			var name="";

			if (acc.AccessExpression is IdentifierExpression)
				name = ((IdentifierExpression)acc.AccessExpression).Value as string;
			else if (acc.AccessExpression is TemplateInstanceExpression)
				name = ((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id;
			else
				return null;


			var vis = new UFCSVisitor(ctxt) {
				FirstParamToCompareWith=firstArgument,
				NameToSearch=name
			};

			vis.IterateThroughScopeLayers(acc.Location);

			if (vis.Matches.Count!=0)
				return TypeDeclarationResolver.HandleNodeMatches(vis.Matches, ctxt, null, acc);

			return null;
		}
	}
}
