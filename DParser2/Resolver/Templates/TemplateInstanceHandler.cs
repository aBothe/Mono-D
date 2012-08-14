using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.Templates;
using System.Collections.ObjectModel;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	public class TemplateInstanceHandler
	{
		public static List<ISemantic> PreResolveTemplateArgs(TemplateInstanceExpression tix, ResolverContextStack ctxt)
		{
			// Resolve given argument expressions
			var templateArguments = new List<ISemantic>();

			if (tix != null && tix.Arguments!=null)
				foreach (var arg in tix.Arguments)
				{
					if (arg is TypeDeclarationExpression)
					{
						var tde = (TypeDeclarationExpression)arg;

						var res = TypeDeclarationResolver.Resolve(tde.Declaration, ctxt);

						if (ctxt.CheckForSingleResult(res, tde.Declaration) || res != null)
						{
							var mr = res[0] as MemberSymbol;
							if (mr != null && mr.Definition is DVariable)
							{
								var dv = (DVariable)mr.Definition;

								if (dv.IsAlias || dv.Initializer == null)
								{
									templateArguments.Add(mr);
									continue;
								}

								ISemantic eval = null;

								try
								{
									eval = new StandardValueProvider(ctxt)[dv];
								}
								catch(System.Exception ee) // Should be a non-const-expression error here only
								{
									ctxt.LogError(dv.Initializer, ee.Message);
								}

								templateArguments.Add(eval==null ? (ISemantic)mr : eval);
							}
							else
								templateArguments.Add(res[0]);
						}
					}
					else
						templateArguments.Add(Evaluation.EvaluateValue(arg, ctxt));
				}

			return templateArguments;
		}

		public static AbstractType[] EvalAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
			TemplateInstanceExpression templateInstanceExpr,
			ResolverContextStack ctxt)
		{
			return EvalAndFilterOverloads(rawOverloadList, PreResolveTemplateArgs(templateInstanceExpr, ctxt), false, ctxt);
		}

		/// <summary>
		/// Associates the given arguments with the template parameters specified in the type/method declarations 
		/// and filters out unmatching overloads.
		/// </summary>
		/// <param name="rawOverloadList">Can be either type results or method results</param>
		/// <param name="givenTemplateArguments">A list of already resolved arguments passed explicitly 
		/// in the !(...) section of a template instantiation 
		/// or call arguments given in the (...) appendix 
		/// that follows a method identifier</param>
		/// <param name="isMethodCall">If true, arguments that exceed the expected parameter count will be ignored as far as all parameters could be satisfied.</param>
		/// <param name="ctxt"></param>
		/// <returns>A filtered list of overloads which mostly fit to the specified arguments.
		/// Usually contains only 1 element.
		/// The 'TemplateParameters' property of the results will be also filled for further usage regarding smart completion etc.</returns>
		public static AbstractType[] EvalAndFilterOverloads(IEnumerable<AbstractType> rawOverloadList,
			IEnumerable<ISemantic> givenTemplateArguments,
			bool isMethodCall,
			ResolverContextStack ctxt)
		{
			if (rawOverloadList == null)
				return null;

			var filteredOverloads = DeduceOverloads(rawOverloadList, givenTemplateArguments, isMethodCall, ctxt);

			// If there are >1 overloads, filter from most to least specialized template param
			if (filteredOverloads.Count > 1)
				return SpecializationOrdering.FilterFromMostToLeastSpecialized(filteredOverloads, ctxt);
			else if (filteredOverloads.Count == 1)
				return filteredOverloads.ToArray();
			
			return null;
		}

		private static List<AbstractType> DeduceOverloads(
			IEnumerable<AbstractType> rawOverloadList, 
			IEnumerable<ISemantic> givenTemplateArguments, 
			bool isMethodCall, 
			ResolverContextStack ctxt)
		{
			bool hasTemplateArgsPassed = givenTemplateArguments != null;
			if (hasTemplateArgsPassed)
			{
				var enumm = givenTemplateArguments.GetEnumerator();
				hasTemplateArgsPassed = enumm.MoveNext();
				enumm.Dispose();
			}

			var filteredOverloads = new List<AbstractType>();

			if (rawOverloadList == null)
				return filteredOverloads;

			foreach (var o in DResolver.StripAliasSymbols(rawOverloadList))
			{
				if (!(o is DSymbol))
				{
					if(!hasTemplateArgsPassed)
						filteredOverloads.Add(o);
					continue;
				}

				var overload = (DSymbol)o;
				var tplNode = overload.Definition;

				// Generically, the node should never be null -- except for TemplateParameterNodes that encapsule such params
				if (tplNode == null)
				{
					filteredOverloads.Add(overload);
					continue;
				}

				// If the type or method has got no template parameters and if there were no args passed, keep it - it's legit.
				if (tplNode.TemplateParameters == null)
				{
					if (!hasTemplateArgsPassed || isMethodCall)
						filteredOverloads.Add(overload);
					continue;
				}

				var deducedTypes = new DeducedTypeDictionary { ParameterOwner=tplNode };
				foreach (var param in tplNode.TemplateParameters)
					deducedTypes[param.Name] = null; // Init all params to null to let deduction functions know what params there are

				if (DeduceParams(givenTemplateArguments, isMethodCall, ctxt, overload, tplNode, deducedTypes))
				{
					overload.DeducedTypes = deducedTypes.ToReadonly(); // Assign calculated types to final result
					filteredOverloads.Add(overload);
				}
				else
					overload.DeducedTypes = null;
			}
			return filteredOverloads;
		}

		private static bool DeduceParams(IEnumerable<ISemantic> givenTemplateArguments, 
			bool isMethodCall, 
			ResolverContextStack ctxt, 
			DSymbol overload, 
			DNode tplNode, 
			DeducedTypeDictionary deducedTypes)
		{
			bool isLegitOverload = true;

			var paramEnum = tplNode.TemplateParameters.GetEnumerator();

			var args= givenTemplateArguments == null ? new List<ISemantic>() : givenTemplateArguments;

			if (overload is MemberSymbol && ((MemberSymbol)overload).IsUFCSResult){
				var l = new List<ISemantic>();
				l.Add(overload.Base); // The base stores the first argument('s type)
				l.AddRange(args);
				args = l;
			}

			var argEnum = args.GetEnumerator();
			foreach (var expectedParam in tplNode.TemplateParameters)
				if (!DeduceParam(ctxt, overload, deducedTypes, argEnum, expectedParam))
				{
					isLegitOverload = false;
					break; // Don't check further params if mismatch has been found
				}

			if (!isMethodCall && argEnum.MoveNext())
			{
				// There are too many arguments passed - discard this overload
				isLegitOverload = false;
			}
			return isLegitOverload;
		}

		private static bool DeduceParam(ResolverContextStack ctxt, 
			DSymbol overload, 
			DeducedTypeDictionary deducedTypes,
			IEnumerator<ISemantic> argEnum, 
			ITemplateParameter expectedParam)
		{
			if (expectedParam is TemplateThisParameter && overload.Base != null)
			{
				var ttp = (TemplateThisParameter)expectedParam;

				// Get the type of the type of 'this' - so of the result that is the overload's base
				var t = DResolver.StripMemberSymbols(overload.Base);

				if (t == null || t.DeclarationOrExpressionBase == null)
					return false;

				//TODO: Still not sure if it's ok to pass a type result to it 
				// - looking at things like typeof(T) that shall return e.g. const(A) instead of A only.

				if (!CheckAndDeduceTypeAgainstTplParameter(ttp, t, deducedTypes, ctxt))
					return false;

				return true;
			}

			// Used when no argument but default arg given
			bool useDefaultType = false;
			if (argEnum.MoveNext() || (useDefaultType = HasDefaultType(expectedParam)))
			{
				// On tuples, take all following arguments and pass them to the check function
				if (expectedParam is TemplateTupleParameter)
				{
					var tupleItems = new List<ISemantic>();
					// A tuple must at least contain one item!
					tupleItems.Add(argEnum.Current);
					while (argEnum.MoveNext())
						tupleItems.Add(argEnum.Current);

					if (!CheckAndDeduceTypeTuple((TemplateTupleParameter)expectedParam, tupleItems, deducedTypes, ctxt))
						return false;
				}
				else if (argEnum.Current != null)
				{
					if (!CheckAndDeduceTypeAgainstTplParameter(expectedParam, argEnum.Current, deducedTypes, ctxt))
						return false;
				}
				else if (useDefaultType && CheckAndDeduceTypeAgainstTplParameter(expectedParam, null, deducedTypes, ctxt))
				{
					// It's legit - just do nothing
				}
				else
					return false;
			}
			// There might be too few args - but that doesn't mean that it's not correct - it's only required that all parameters got satisfied with a type
			else if (!AllParamatersSatisfied(deducedTypes))
				return false;

			return true;
		}

		public static bool AllParamatersSatisfied(DeducedTypeDictionary deductions)
		{
			foreach (var kv in deductions)
				if (kv.Value == null || kv.Value==null)
					return false;

			return true;
		}

		public static bool HasDefaultType(ITemplateParameter p)
		{
			if (p is TemplateTypeParameter)
				return ((TemplateTypeParameter)p).Default != null;
			else if (p is TemplateAliasParameter)
			{
				var ap = (TemplateAliasParameter)p;
				return ap.DefaultExpression != null || ap.DefaultType != null;
			}
			else if (p is TemplateThisParameter)
				return HasDefaultType(((TemplateThisParameter)p).FollowParameter);
			else if (p is TemplateValueParameter)
				return ((TemplateValueParameter)p).DefaultExpression != null;
			return false;
		}

		static bool CheckAndDeduceTypeAgainstTplParameter(ITemplateParameter handledParameter, 
			ISemantic argumentToCheck,
			DeducedTypeDictionary deducedTypes,
			ResolverContextStack ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes, ctxt).Handle(handledParameter, argumentToCheck);
		}

		static bool CheckAndDeduceTypeTuple(TemplateTupleParameter tupleParameter, 
			IEnumerable<ISemantic> typeChain,
			DeducedTypeDictionary deducedTypes,
			ResolverContextStack ctxt)
		{
			return new Templates.TemplateParameterDeduction(deducedTypes,ctxt).Handle(tupleParameter,typeChain);
		}
	}
}
