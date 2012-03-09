using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public enum ResolutionOptions
	{
		ResolveAliases=2^0,
		ResolveBaseClasses=2^1,

		/// <summary>
		/// Stops resolution if first match has been found
		/// </summary>
		StopAfterFirstMatch=2^2,

		/// <summary>
		/// Stops resolution at the end of a match's block if first match has been found.
		/// This will still resolve possible overloads but stops after leaving the overloads' scope.
		/// </summary>
		StopAfterFirstOverloads = StopAfterFirstMatch + 2^3,

		Default = ResolveAliases | ResolveBaseClasses
	}
}
