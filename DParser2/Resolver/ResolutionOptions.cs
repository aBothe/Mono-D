using System;

namespace D_Parser.Resolver
{
	[Flags]
	public enum ResolutionOptions
	{
		DontResolveAliases=1,
		DontResolveBaseClasses=2,
		DontResolveBaseTypes = DontResolveBaseClasses,

		/// <summary>
		/// Stops resolution if first match has been found
		/// </summary>
		StopAfterFirstMatch=8,

		/// <summary>
		/// Stops resolution at the end of a match's block if first match has been found.
		/// This will still resolve possible overloads but stops after leaving the overloads' scope.
		/// </summary>
		StopAfterFirstOverloads = StopAfterFirstMatch + 16,

		/// <summary>
		/// If set, the resolver won't filter out members by template parameter deduction.
		/// </summary>
		NoTemplateParameterDeduction= 32,

		Default = 0
	}
}
