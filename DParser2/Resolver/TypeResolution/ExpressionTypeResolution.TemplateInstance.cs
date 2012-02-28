using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace D_Parser.Resolver.TypeResolution
{
	public class TemplateInstanceResolver
	{
		/// <summary>
		/// Used if a member method takes template arguments but doesn't have explicit ones given.
		/// 
		/// So, writeln(123) will be interpreted as writeln!int(123);
		/// </summary>
		public static ResolveResult[] ResolveAndFilterTemplateResults(
			IExpression[] templateArguments,
			IEnumerable<ResolveResult> resolvedTypes,
			ResolverContextStack ctxt)
		{
			var templateArgs = new List<ResolveResult[]>();

			// Note: If an arg resolution returns null, add it anyway to keep argument and parameter indexes parallel
			if (templateArguments != null)
				foreach (var arg in templateArguments)
					templateArgs.Add(ExpressionTypeResolver.Resolve(arg, ctxt));

			return ResolveAndFilterTemplateResults(templateArgs.Count > 0 ? templateArgs.ToArray() : null, resolvedTypes, ctxt);
		}


		public static ResolveResult[] ApplyDefaultTemplateParameters(IEnumerable<ResolveResult> resolvedTemplateIdentifiers, ResolverContextStack ctxt)
		{
			return ResolveAndFilterTemplateResults(null as ResolveResult[][], resolvedTemplateIdentifiers, ctxt);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="args"></param>
		/// <param name="resolvedTemplateIdentifiers"></param>
		/// <param name="ctxt"></param>
		/// <param name="enforeParameterArgumentMatch">If false, a template won't get kicked out if there are no parameters given but arguments.</param>
		/// <returns></returns>
		public static ResolveResult[] ResolveAndFilterTemplateResults(
			ResolveResult[][] args, 
			IEnumerable<ResolveResult> resolvedTemplateIdentifiers,
			ResolverContextStack ctxt,
			bool enforeParameterArgumentMatch=true)
		{
			if (resolvedTemplateIdentifiers == null)
				return null;

			
			var returnedTemplates = new List<ResolveResult>();

			foreach (ResolveResult rr in DResolver.TryRemoveAliasesFromResult(resolvedTemplateIdentifiers))
			{
				var tir = rr as TemplateInstanceResult;
				if (tir == null)
				{
					if (args == null || rr is DelegateResult)
						returnedTemplates.Add(rr);

					continue;
				}

				var dn = tir.Node as DNode;

				if (dn == null)
				{
					returnedTemplates.Add(rr);
					continue;
				}

				// If there aren't any parameters to check..
				if (dn.TemplateParameters == null || dn.TemplateParameters.Length == 0)
				{
					// .. and no arguments given (or if it's ok not to have parameters but arguments), add this result
					if (args == null || !enforeParameterArgumentMatch)
						returnedTemplates.Add(tir);

					// or omit the current result because it's not fitting to the given parameters
					continue;
				}

				#region First associate every parameter with possible arguments
				var parameterArgumentAssociations = tir.TemplateParameters = new Dictionary<ITemplateParameter, ResolveResult[]>();

				if (args == null)
				{
					// A type tuple also may consist of 0 types!
					if (dn.TemplateParameters[0] is TemplateTupleParameter)
					{
						parameterArgumentAssociations[dn.TemplateParameters[0]] = null;
						returnedTemplates.Add(tir);
						continue;
					}
					else if (GetTypeDefault(dn.TemplateParameters[0]) != null)
					{ 
						// If (at least) the first parameter has a default type, continue
					}
					else
						continue;
				}

				/*
				 * Things that need attention:
				 * -- Default arguments (Less args than parameters)
				 * -- Type specializations
				 * -- Type tuples (More args than parameters)
				 */

				int i = 0;
				for (; i < dn.TemplateParameters.Length; i++)
				{
					if (dn.TemplateParameters[i] is TemplateTupleParameter)
						break;

					if (args!=null && i < args.Length) // Ok, one arg per parameter
						parameterArgumentAssociations[dn.TemplateParameters[i]] = args[i];
					else // More params than args -- default type needed
					{
						// Get the default type
						parameterArgumentAssociations[dn.TemplateParameters[i]] = ResolveTypeDefault(dn.TemplateParameters[i], ctxt);
					}
				}

				// More args than params -- put them all in the last type tuple argument
				if (args!=null && args.Length > i)
				{
					var typeTuple = dn.TemplateParameters[dn.TemplateParameters.Length - 1] as TemplateTupleParameter;

					if (typeTuple == null) // If no type tuple parameter given, ignore this template instance result
						continue;
					else
					{
						var tupleTypes = new List<ResolveResult>();

						for (; i < args.Length; i++)
						{
							/*
							 * HACK: If there are multiple definitions of one type passed as argument, 
							 * fuck it and add the first result only.
							 */
							if (args[i] != null && args[i].Length > 0)
								tupleTypes.Add(args[i][0]);
						}

						parameterArgumentAssociations[typeTuple] = tupleTypes.ToArray();
					}
				}
				#endregion

				// Test every parameter / argument match
				if (TestParameterArgumentMatch(parameterArgumentAssociations))
					returnedTemplates.Add(tir);
			}

			if (returnedTemplates.Count == 0)
				return null;
			return returnedTemplates.ToArray();
		}

		static bool TestParameterArgumentMatch(Dictionary<ITemplateParameter, ResolveResult[]> assoc)
		{
			return true;
		}





		public static ResolveResult[] ResolveTypeSpecialization(ITemplateParameter p, ResolverContextStack ctxt)
		{
			var defType = GetTypeSpecialization(p);

			if (defType is IExpression)
				return ExpressionTypeResolver.Resolve((IExpression)defType, ctxt);
			else if (defType is ITypeDeclaration)
				return TypeDeclarationResolver.Resolve((ITypeDeclaration)defType, ctxt);

			return null;
		}

		/// <summary>
		/// Returns the specialization expression/type declaration of a template parameter
		/// </summary>
		static object GetTypeSpecialization(ITemplateParameter p)
		{
			if (p is TemplateAliasParameter)
			{
				var tap = p as TemplateAliasParameter;

				return (object)tap.SpecializationExpression ?? tap.SpecializationType;
			}
			else if (p is TemplateThisParameter)
				return GetTypeSpecialization((p as TemplateThisParameter).FollowParameter);
			else if (p is TemplateTupleParameter)
				return null;
			else if (p is TemplateTypeParameter)
				return (p as TemplateTypeParameter).Specialization;
			else if (p is TemplateValueParameter)
				return (p as TemplateValueParameter).SpecializationExpression;

			return null;
		}

		/// <summary>
		/// Resolves the default value of a template parameter.
		/// 
		/// for U = int it returns static type int
		/// </summary>
		static ResolveResult[] ResolveTypeDefault(ITemplateParameter p, ResolverContextStack ctxt)
		{
			var defType = GetTypeDefault(p);

			if (defType is IExpression)
				return ExpressionTypeResolver.Resolve((IExpression)defType, ctxt);
			else if (defType is ITypeDeclaration)
				return TypeDeclarationResolver.Resolve((ITypeDeclaration)defType, ctxt);

			return null;
		}

		static object GetTypeDefault(ITemplateParameter p)
		{
			if (p is TemplateAliasParameter)
			{
				var tap = p as TemplateAliasParameter;

				return (object)tap.DefaultExpression ?? tap.DefaultType;
			}
			else if (p is TemplateThisParameter)
				return GetTypeDefault((p as TemplateThisParameter).FollowParameter);
			else if (p is TemplateTupleParameter)
				return null;
			else if (p is TemplateTypeParameter)
				return (p as TemplateTypeParameter).Default;
			else if (p is TemplateValueParameter)
				return (p as TemplateValueParameter).DefaultExpression;

			return null;
		}

		public static ResolveResult[] SubstituteTemplateParameters(ResolveResult[] results, ResolveResult resolvedTemplates)
		{
			if (results == null || resolvedTemplates == null)
				return results;

			var r = new List<ResolveResult>();

			foreach (var rr in DResolver.TryRemoveAliasesFromResult(results))
			{
				var mr = rr as MemberResult;
				if (mr == null || !(mr.Node is TemplateParameterNode))
				{
					r.Add(rr);
					continue;
				}

				var tp = ((TemplateParameterNode)mr.Node).TemplateParameter;

				// Now search in the resolvedTemplates-parameter for 'tp' and add it's resolution results instead
				var surr = FindTemplateParameterSurrogate(tp, resolvedTemplates);

				if (surr != null)
					r.AddRange(surr);
			}

			return r.Count > 0 ? r.ToArray():null;
		}

		static ResolveResult[] FindTemplateParameterSurrogate(ITemplateParameter tp,ResolveResult resolvedTemplate)
		{
			if (tp == null || resolvedTemplate == null)
				return null;

			ResolveResult[] ret=null;

			var tir=resolvedTemplate as TemplateInstanceResult;
			if (tir != null)
			{
				if (tir.TemplateParameters != null && tir.TemplateParameters.TryGetValue(tp, out ret))
					return ret;
			}

			var mr = resolvedTemplate as MemberResult;

			if (mr != null && mr.MemberBaseTypes != null)
			{
				foreach (var rr in mr.MemberBaseTypes)
				{
					var ress = FindTemplateParameterSurrogate(tp, rr);

					if (ress != null)
						return ress;
				}
			}

			return FindTemplateParameterSurrogate(tp, resolvedTemplate.ResultBase);
		}
	}
}
