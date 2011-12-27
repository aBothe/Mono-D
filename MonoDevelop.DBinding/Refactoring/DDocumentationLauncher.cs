using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;
using MonoDevelop.D.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace MonoDevelop.D.Refactoring
{
	public class DDocumentationLauncher
	{
		/// <summary>
		/// Reads the current caret context, and opens the adequate reference site in the default browser
		/// </summary>
		public static void LaunchReferenceInBrowser()
		{
			ResolverContext ctxt = null;
			var rr=DResolverWrapper.ResolveHoveredCode(out ctxt);

			LaunchReferenceInBrowser(rr!=null?rr[0]:null,ctxt);
		}

		public static void LaunchReferenceInBrowser(ResolveResult result,ResolverContext ctxt)
		{
			if(result!=null)
			{
				var n = DResolverWrapper.GetResultMember(result);
			}
		}


		public static void LaunchFor(IStatement stmt)
		{

		}

		public static void LaunchFor(IExpression ex)
		{

		}
	}
}
