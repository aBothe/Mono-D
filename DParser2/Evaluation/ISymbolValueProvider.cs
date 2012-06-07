using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;

namespace D_Parser.Evaluation
{
	interface ISymbolValueProvider
	{
		ResolverContextStack ResolutionContext { get; }
		bool IsSet(string name);
		object this[string Name] { get;set; }
	}
}
