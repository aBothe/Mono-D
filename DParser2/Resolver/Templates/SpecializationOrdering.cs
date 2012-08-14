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

		public static AbstractType[] FilterFromMostToLeastSpecialized(
			List<AbstractType> templateOverloads,
			ResolverContextStack ctxt)
		{
			if (templateOverloads == null)
				return null;

			var so = new SpecializationOrdering { ctxt = ctxt };

			/*
			 * Note: If there are functions that are specialized equally, like
			 * void foo(T) (T t) {} and
			 * void foo(T) (T t, int a) {},
			 * both functions have to be returned - because foo!string matches both overloads.
			 * Later on, in the parameter-argument-comparison, these overloads will be filtered a second time - only then, 
			 * two overloads would be illegal.
			 */
			var lastEquallySpecializedOverloads = new List<AbstractType>();
			var currentlyMostSpecialized = templateOverloads[0];
			lastEquallySpecializedOverloads.Add(currentlyMostSpecialized);

			for (int i = 1; i < templateOverloads.Count; i++)
			{
				var evenMoreSpecialized = so.GetTheMoreSpecialized(currentlyMostSpecialized, templateOverloads[i]);

				if (evenMoreSpecialized == null)
					lastEquallySpecializedOverloads.Add(templateOverloads[i]);
				else
				{
					currentlyMostSpecialized = evenMoreSpecialized;

					lastEquallySpecializedOverloads.Clear();
					lastEquallySpecializedOverloads.Add(currentlyMostSpecialized);
				}
			}

			return lastEquallySpecializedOverloads.ToArray();
		}

		AbstractType GetTheMoreSpecialized(AbstractType r1, AbstractType r2)
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

		bool IsMoreSpecialized(AbstractType r1, AbstractType r2)
		{
			// Probably an issue: Assume a type to be more specialized if it's a symbol
			if (r1 is DSymbol && !(r2 is DSymbol))
				return true;
			else if (r2 is DSymbol && !(r1 is DSymbol))
				return false;
			else if (!(r1 is DSymbol && r2 is DSymbol))
				return false;

			var dn1 = ((DSymbol)r1).Definition;
			var dn2 = ((DSymbol)r2).Definition;

			if (dn1 == null || dn1.TemplateParameters == null || dn2 == null || dn2.TemplateParameters == null)
				return false;

			var dummyList = new Dictionary<string, ISemantic>();
			foreach (var t in dn1.TemplateParameters)
				dummyList.Add(t.Name, null);

			var tp1_enum = dn1.TemplateParameters.GetEnumerator();
			var tp2_enum = dn2.TemplateParameters.GetEnumerator();

			while (tp1_enum.MoveNext() && tp2_enum.MoveNext())
				if (!IsMoreSpecialized((ITemplateParameter)tp1_enum.Current, (ITemplateParameter)tp2_enum.Current, dummyList))
					return false;

			return true;
		}

		bool IsMoreSpecialized(ITemplateParameter t1, ITemplateParameter t2, Dictionary<string, ISemantic> t1_dummyParameterList)
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

		bool IsMoreSpecialized(TemplateAliasParameter t1, TemplateAliasParameter t2, Dictionary<string,ISemantic> t1_DummyParamList)
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
		bool IsMoreSpecialized(TemplateTypeParameter t1, TemplateTypeParameter t2, Dictionary<string,ISemantic> t1_DummyParamList)
		{
			// If one parameter is not specialized it should be clear
			if (t1.Specialization != null && t2.Specialization == null)
				return true;
			else if (t1.Specialization == null) // Return false if t2 is more specialized or if t1 as well as t2 are not specialized
				return false;

			return IsMoreSpecialized(t1.Specialization, t2, t1_DummyParamList);
		}

		bool IsMoreSpecialized(ITypeDeclaration Spec, ITemplateParameter t2, Dictionary<string, ISemantic> t1_DummyParamList)
		{
			// Make a type out of t1's specialization
			var frame = ctxt.PushNewScope(ctxt.ScopedBlock.Parent as IBlockNode);

			// Make the T in e.g. T[] a virtual type so T will be replaced by it
			// T** will be X** then - so a theoretically valid type instead of a template param
			var dummyType = new ClassType(new DClassLike { Name = "X" }, null, null);
			foreach (var kv in t1_DummyParamList)
				frame.DeducedTemplateParameters[kv.Key] = new TemplateParameterSymbol(t2,dummyType);

			var t1_TypeResults = Resolver.TypeResolution.TypeDeclarationResolver.Resolve(Spec, ctxt);
			if (t1_TypeResults == null || t1_TypeResults.Length == 0)
				return true;

			ctxt.Pop();

			// Now try to fit the virtual Type t2 into t1 - and return true if it's possible
			return new TemplateParameterDeduction(new DeducedTypeDictionary(), ctxt).Handle(t2, t1_TypeResults[0]);
		}
	}
}
