using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Resolver.Templates
{
	/// <summary>
	/// See http://msdn.microsoft.com/de-de/library/zaycz069.aspx
	/// </summary>
	public class SpecializationOrdering
	{
		ResolverContextStack ctxt;

		public static ResolveResult[] FilterFromMostToLeastSpecialized(
			List<ResolveResult> templateOverloads,
			ResolverContextStack ctxt)
		{
			if (templateOverloads == null)
				return null;

			var so = new SpecializationOrdering { ctxt = ctxt };

			var currentlyMostSpecialized = templateOverloads[0] as TemplateInstanceResult;

			for (int i = 1; i < templateOverloads.Count; i++)
			{
				var evenMoreSpecialized = so.GetTheMoreSpecialized(currentlyMostSpecialized, templateOverloads[i] as TemplateInstanceResult);

				if (evenMoreSpecialized != null)
				{
					currentlyMostSpecialized = evenMoreSpecialized;
				}
				else if (i == templateOverloads.Count - 1)
				{
					/*
					 * It might be the case that Type 1 is equally specialized as Type 2 is, but:
					 * If comparing Type 2 with Type 3 turns out that Type 3 is more specialized, return Type 3!
					 * (There probably will be a global resolution error cache  required to warn the user that
					 * all template parameters of Type 1 are equal to those of Type 2)
					 */

					// Ambiguous result -- ERROR!
					return new[] { currentlyMostSpecialized, templateOverloads[i] };
				}
			}

			return new[]{ currentlyMostSpecialized };
		}

		TemplateInstanceResult GetTheMoreSpecialized(TemplateInstanceResult r1, TemplateInstanceResult r2)
		{
			if (r1 == null || r2 == null)
				return null;

			if (IsMoreSpecialized(r1, r2))
			{
				if (IsMoreSpecialized(r2, r1))
					return null;
				else
					return r1;
			}
			else
			{ 
				if (IsMoreSpecialized(r2, r1))
					return r2;
				else
					return null;
			}
		}

		bool IsMoreSpecialized(TemplateInstanceResult r1, TemplateInstanceResult r2)
		{
			var dn1 = r1.Node as DNode;
			var dn2 = r2.Node as DNode;

			if (dn1 == null || dn1.TemplateParameters == null || dn2 == null || dn2.TemplateParameters == null)
				return false;

			var dummyList = new Dictionary<string, ResolveResult[]>();
			foreach (var t in dn1.TemplateParameters)
				dummyList.Add(t.Name, null);

			var tp1_enum = dn1.TemplateParameters.GetEnumerator();
			var tp2_enum = dn2.TemplateParameters.GetEnumerator();

			while (tp1_enum.MoveNext() && tp2_enum.MoveNext())
				if (!IsMoreSpecialized((ITemplateParameter)tp1_enum.Current, (ITemplateParameter)tp2_enum.Current, dummyList))
					return false;

			return true;
		}

		bool IsMoreSpecialized(ITemplateParameter t1, ITemplateParameter t2, Dictionary<string, ResolveResult[]> t1_dummyParameterList)
		{
			if (t1 is TemplateTypeParameter && t2 is TemplateTypeParameter &&
				!IsMoreSpecialized((TemplateTypeParameter)t1, (TemplateTypeParameter)t2, t1_dummyParameterList))
				return false;
			else if (t1 is TemplateValueParameter && t2 is TemplateValueParameter &&
				!IsMoreSpecialized((TemplateValueParameter)t1, (TemplateValueParameter)t2))
				return false;
			else if (t1 is TemplateAliasParameter && t2 is TemplateAliasParameter &&
				!IsMoreSpecialized((TemplateAliasParameter)t1, (TemplateAliasParameter)t2, t1_dummyParameterList))
				return false;
			else if (t1 is TemplateThisParameter && t2 is TemplateThisParameter && !
				IsMoreSpecialized(((TemplateThisParameter)t1).FollowParameter, ((TemplateThisParameter)t2).FollowParameter, t1_dummyParameterList))
				return false;

			return false;
		}

		bool IsMoreSpecialized(TemplateAliasParameter t1, TemplateAliasParameter t2, Dictionary<string,ResolveResult[]> t1_DummyParamList)
		{
			if (t1.SpecializationExpression != null)
			{
				if(t2.SpecializationExpression == null)
					return true;
				// It's not needed to test both expressions for equality because they actually were equal to the given template instance argument
				// à la  'a = b, a = c => b = c'
				return false;
			}
			else if (t1.SpecializationType != null)
			{
				if (t2.SpecializationType == null)
					return true;

				return IsMoreSpecialized(t1.SpecializationType, t2, t1_DummyParamList);
			}
			return false;
		}

		bool IsMoreSpecialized(TemplateValueParameter t1, TemplateValueParameter t2)
		{
			if (t1.SpecializationExpression != null && t2.SpecializationExpression == null)
				return true;
			return false;
		}

		/// <summary>
		/// Tests if t1 is more specialized than t2
		/// </summary>
		bool IsMoreSpecialized(TemplateTypeParameter t1, TemplateTypeParameter t2, Dictionary<string,ResolveResult[]> t1_DummyParamList)
		{
			// If one parameter is not specialized it should be clear
			if (t1.Specialization != null && t2.Specialization == null)
				return true;
			else if (t1.Specialization == null) // Return false if t2 is more specialized or if t1 as well as t2 are not specialized
				return false;

			return IsMoreSpecialized(t1.Specialization, t2, t1_DummyParamList);
		}

		bool IsMoreSpecialized(ITypeDeclaration Spec, ITemplateParameter t2, Dictionary<string, ResolveResult[]> t1_DummyParamList)
		{
			// Make a type out of t1's specialization
			var frame = ctxt.PushNewScope(ctxt.ScopedBlock.Parent as IBlockNode);

			// Make the T in e.g. T[] a virtual type so T will be replaced by it
			// T** will be X** then - so a theoretically valid type instead of a template param
			var dummyType = new[] { new TypeResult { Node = new DClassLike { Name = "X" } } };
			foreach (var kv in t1_DummyParamList)
				frame.DeducedTemplateParameters[kv.Key] = dummyType;

			var t1_TypeResults = Resolver.TypeResolution.TypeDeclarationResolver.Resolve(Spec, ctxt);
			if (t1_TypeResults == null || t1_TypeResults.Length == 0)
				return true;

			ctxt.Pop();

			// Now try to fit the virtual Type t2 into t1 - and return true if it's possible
			return new TemplateParameterDeduction(new Dictionary<string, ResolveResult[]>(), ctxt)
				.Handle(t2, t1_TypeResults[0]);
		}
	}
}
