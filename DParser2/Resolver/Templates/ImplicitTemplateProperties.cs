using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	/// <summary>
	/// http://dlang.org/template.html
	/// see 'Implicit Template Properties'
	/// </summary>
	internal class ImplicitTemplateProperties
	{
		/// <summary>
		/// Returns true if dc contains exclusively children that are named like dc.
		/// </summary>
		public static bool ContainsEquallyNamedChildrenOnly(DClassLike dc)
		{
			//Edit: The actual dmd implementation differs from the spec immensely:
			// It's only required that there are items called like dc, nothing else.
			return dc != null && dc[dc.Name].Count != 0;
		}

		public static bool TryGetImplicitProperty(TemplateType template, ResolverContextStack ctxt, out AbstractType[] matchingChild)
		{
			// Check if there are only children that are named as the parent template.
			// That's the requirement for the special treatment.
			matchingChild = null;
			if (!ContainsEquallyNamedChildrenOnly(template.Definition))
				return false;

			// Prepare a new context
			bool pop = !ctxt.NodeIsInCurrentScopeHierarchy(template.Definition);
			if (pop)
				ctxt.PushNewScope(template.Definition);

			// Introduce the deduced params to the current resolution context
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(template);

			// Get actual overloads,
			var overloads = template.Definition[template.Name];

			// resolve them
			var resolvedOverloads = TypeDeclarationResolver.HandleNodeMatches(overloads, ctxt, null, template.DeclarationOrExpressionBase);

			// and deduce their parameters whereas this time, the parent's parameter are given already, in the case it's e.g.
			// needed as return type or in a declaration condition:

			// Furthermore, pass all the arguments that have been passed to the super template, to the child,
			// so these arguments may be used again for some inner parameters.
			var args = new List<ISemantic>(template.DeducedTypes.Count);
			foreach (var kv in template.DeducedTypes)
				args.Add((ISemantic)kv.Value.ParameterValue ?? kv.Value.Base);

			matchingChild = TemplateInstanceHandler.DeduceParamsAndFilterOverloads(resolvedOverloads, args, true, ctxt);

			// Undo context-related changes
			if (pop)
				ctxt.Pop();
			else
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(template);

			return matchingChild != null && matchingChild.Length == 1 && matchingChild[0] != null;
		}
	}
}
