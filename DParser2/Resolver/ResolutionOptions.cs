using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	[Flags]
	public enum ResolutionOptions
	{
		ResolveAliases=1,
		ResolveBaseClasses=2,

		/// <summary>
		/// Stops resolution if first match has been found
		/// </summary>
		StopAfterFirstMatch=4,

		/// <summary>
		/// Stops resolution at the end of a match's block if first match has been found.
		/// This will still resolve possible overloads but stops after leaving the overloads' scope.
		/// </summary>
		StopAfterFirstOverloads = StopAfterFirstMatch + 8,

		Default = ResolveAliases | ResolveBaseClasses
	}
}
