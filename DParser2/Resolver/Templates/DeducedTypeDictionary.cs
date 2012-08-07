using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.Templates
{
	public class DeducedTypeDictionary : Dictionary<string, TemplateParameterSymbol>	{
		public DeducedTypeDictionary() { }
		public DeducedTypeDictionary(Dictionary<string, TemplateParameterSymbol> d) : base(d) { }
		public DeducedTypeDictionary(IEnumerable<KeyValuePair<string, TemplateParameterSymbol>> l)
		{
			if (l != null)
				foreach (var i in l)
					Add(i.Key, i.Value);
		}

		public ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> ToReadonly()
		{
			return new ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>>(this.ToList());
		}
	}

	public class TemplateParameterSymbol : MemberSymbol
	{
		/// <summary>
		/// Only used for template value parameters.
		/// </summary>
		public readonly ISymbolValue ParameterValue;
		public readonly ITemplateParameter Parameter;

		public TemplateParameterSymbol(ITemplateParameter tp,
			ISemantic representedTypeOrValue,
			ISyntaxRegion originalParameterIdentifier = null,
			DNode parentNode = null)
			: base(new TemplateParameterNode(tp) { Parent = parentNode },
			AbstractType.Get(representedTypeOrValue), originalParameterIdentifier ?? tp)
		{
			this.Parameter = tp;
			this.ParameterValue = representedTypeOrValue as ISymbolValue;
		}

		public override string ToString()
		{
			return "(template param) "+Parameter.Name+" = "+(ParameterValue!=null ? ParameterValue.ToString() : (Base==null ? "" : Base.ToString()));
		}
	}
}
