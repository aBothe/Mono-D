using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(PostfixExpression ex)
		{
			if (ex is PostfixExpression_MethodCall)
				return E((PostfixExpression_MethodCall)ex, !ctxt.Options.HasFlag(ResolutionOptions.ReturnMethodReferencesOnly));

			var foreExpr=E(ex.PostfixForeExpression);

			if(foreExpr is AliasedType)
				foreExpr = DResolver.StripAliasSymbol((AbstractType)foreExpr);

			if (foreExpr == null)
			{
				if (eval)
					throw new EvaluationException(ex.PostfixForeExpression, "Evaluation returned empty result");
				else
				{
					ctxt.LogError(new NothingFoundError(ex.PostfixForeExpression));
					return null;
				}
			}

			if (ex is PostfixExpression_Access)
			{
				bool ufcs=false;
				var r = E((PostfixExpression_Access)ex, out ufcs, foreExpr, true);
				ctxt.CheckForSingleResult(r, ex);
				return r != null && r.Length != 0 ? r[0] : null;
			}
			else if (ex is PostfixExpression_Increment)
				return E((PostfixExpression_Increment)ex, foreExpr);
			else if (ex is PostfixExpression_Decrement)
				return E((PostfixExpression_Decrement)foreExpr);

			// myArray[0]; myArray[0..5];
			// opIndex/opSlice ?
			if(foreExpr is MemberSymbol)
				foreExpr = DResolver.StripMemberSymbols((AbstractType)foreExpr);

			if (ex is PostfixExpression_Slice) 
				return E((PostfixExpression_Slice)ex, foreExpr);
			else if(ex is PostfixExpression_Index)
				return E((PostfixExpression_Index)ex, foreExpr);

			return null;
		}

		ISemantic E(PostfixExpression_MethodCall call, bool returnBaseTypeOnly=true)
		{
			// Deduce template parameters later on
			AbstractType[] baseExpression = null;
			ISymbolValue baseValue = null;
			TemplateInstanceExpression tix = null;
			bool isUFCSFunction = false;

			GetRawCallOverloads(call, out baseExpression, out baseValue, out tix, out isUFCSFunction);

			var methodOverloads = new List<AbstractType>();

			#region Search possible methods, opCalls or delegates that could be called
			bool requireStaticItems = true; //TODO: What if there's an opCall and a foreign method at the same time? - and then this variable would be bullshit
			IEnumerable<AbstractType> scanResults = DResolver.StripAliasSymbols(baseExpression);
			var nextResults = new List<AbstractType>();

			while (scanResults != null)
			{
				foreach (var b in scanResults)
				{
					if (b is MemberSymbol)
					{
						var mr = (MemberSymbol)b;

						if (mr.Definition is DMethod)
						{
							methodOverloads.Add(mr);
							continue;
						}
						else if (mr.Definition is DVariable)
						{
							// If we've got a variable here, get its base type/value reference
							if (eval)
							{
								var dgVal = ValueProvider[(DVariable)mr.Definition] as DelegateValue;

								if (dgVal != null)
								{
									nextResults.Add(dgVal.Definition);
									continue;
								}
								else
									throw new EvaluationException(call, "Variable must be a delegate, not anything else", mr);
							}
							else
							{
								var bt = DResolver.StripAliasSymbol(mr.Base ?? TypeDeclarationResolver.ResolveSingle(mr.Definition.Type, ctxt));

								// Must be of type delegate
								if (bt is DelegateType)
								{
									//TODO: Ensure that there's no further overload - inform the user elsewise

									if (returnBaseTypeOnly)
										return bt;
									else
										return new MemberSymbol(mr.Definition, bt, mr.DeclarationOrExpressionBase);
								}
								else
								{
									/*
									 * If mr.Node is not a method, so e.g. if it's a variable
									 * pointing to a delegate
									 * 
									 * class Foo
									 * {
									 *	string opCall() {  return "asdf";  }
									 * }
									 * 
									 * Foo f=new Foo();
									 * f(); -- calls opCall, opCall is not static
									 */
									nextResults.Add(bt);
									requireStaticItems = false;
								}
								//TODO: Can other types work as function/are callable?
							}
						}
					}
					else if (b is DelegateType)
					{
						var dg = (DelegateType)b;

						/*
						 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
						 * auto dg=delegate(x) {return x*3;};
						 * int b = dg(4);
						 */

						if (dg.IsFunctionLiteral)
							methodOverloads.Add(dg);
						else
						{
							// If it's just wanted to pass back the delegate's return type, skip the remaining parts of this method.
							if (eval) 
								throw new EvaluationException(call, "TODO", dg);
							//TODO
							//if(returnBaseTypeOnly)
							//TODO: Check for multiple definitions. Also, make a parameter-argument check to inform the user about wrong arguments.
							return dg;
						}
					}
					else if (b is ClassType)
					{
						/*
						 * auto a = MyStruct(); -- opCall-Overloads can be used
						 */
						var classDef = ((ClassType)b).Definition;

						if (classDef == null)
							continue;

						foreach (var i in classDef)
							if (i.Name == "opCall" && i is DMethod && (!requireStaticItems || (i as DNode).IsStatic))
								methodOverloads.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call) as MemberSymbol);
					}
					/*
					 * Every struct can contain a default ctor:
					 * 
					 * struct S { int a; bool b; }
					 * 
					 * auto s = S(1,true); -- ok
					 * auto s2= new S(2,false); -- error, no constructor found!
					 */
					else if (b is StructType && methodOverloads.Count == 0)
					{
						//TODO: Deduce parameters
						return b;
					}
				}

				scanResults = nextResults.Count == 0 ? null : nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			// Get all arguments' types
			var callArguments = new List<ISemantic>();

			// If it's sure that we got a ufcs call here, add the base expression's type as first argument type
			if (isUFCSFunction)
				callArguments.Add(eval ? (ISemantic)baseValue : baseExpression[0]);

			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					callArguments.Add(E(arg));

			#region Deduce template parameters and filter out unmatching overloads
			// First add optionally given template params
			// http://dlang.org/template.html#function-templates
			var tplParamDeductionArguments = tix == null ?
				new List<ISemantic>() :
				TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);

			// Then add the arguments[' member types]
			foreach (var arg in callArguments)
				if (arg is VariableValue)
					tplParamDeductionArguments.Add(ValueProvider[((VariableValue)arg).Variable]);
				else if(arg is AbstractType)
					tplParamDeductionArguments.Add(DResolver.StripMemberSymbols((AbstractType)arg));
				else
					tplParamDeductionArguments.Add(arg);

			var templateParamFilteredOverloads= TemplateInstanceHandler.EvalAndFilterOverloads(
				methodOverloads,
				tplParamDeductionArguments.Count > 0 ? tplParamDeductionArguments.ToArray() : null,
				true, ctxt);
			#endregion

			#region Filter by parameter-argument comparison
			var argTypeFilteredOverloads = new List<AbstractType>();

			if (templateParamFilteredOverloads != null)
				foreach (var ov in templateParamFilteredOverloads)
				{
					if (ov is MemberSymbol)
					{
						var ms = (MemberSymbol)ov;
						var dm = ms.Definition as DMethod;
						bool add = false;

						if (dm != null)
						{
							ctxt.CurrentContext.IntroduceTemplateParameterTypes(ms);

							add = false;

							if (callArguments.Count == 0 && dm.Parameters.Count == 0)
								add=true;
							else
								for (int i=0; i< dm.Parameters.Count; i++)
								{
									var paramType = TypeDeclarationResolver.ResolveSingle(dm.Parameters[i].Type, ctxt);
								
									// TODO: Expression tuples & variable argument lengths
									if (i >= callArguments.Count ||
										!ResultComparer.IsImplicitlyConvertible(callArguments[i], paramType, ctxt))
										continue;

									add = true;
								}

							if (add)
							{
								var bt=TypeDeclarationResolver.GetMethodReturnType(dm, ctxt);

								if (returnBaseTypeOnly)
									argTypeFilteredOverloads.Add(bt);
								else
									argTypeFilteredOverloads.Add(new MemberSymbol(dm, bt, ms.DeclarationOrExpressionBase, ms.DeducedTypes));
							}

							ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ms);
						}
					}
					else if(ov is DelegateType)
					{
						var dg = (DelegateType)ov;
						var bt = TypeDeclarationResolver.GetMethodReturnType(dg, ctxt);

						//TODO: Param-Arg check
						if (returnBaseTypeOnly)
							argTypeFilteredOverloads.Add(bt);
						else
							argTypeFilteredOverloads.Add(new DelegateType(bt, dg.DeclarationOrExpressionBase as FunctionLiteral, dg.Parameters));
					}
				}
			#endregion

			if (eval)
			{
				// Convert ISemantic[] to ISymbolValue[]
				var args = new List<ISymbolValue>(callArguments.Count);

				foreach (var a in callArguments)
					args.Add(a as ISymbolValue);

				// Execute/Evaluate the variable contents etc.
				return TryDoCTFEOrGetValueRefs(argTypeFilteredOverloads.ToArray(), call.PostfixForeExpression, true, args.ToArray());
			}
			else
			{
				// Check if one overload remains and return that one.
				ctxt.CheckForSingleResult(argTypeFilteredOverloads.ToArray(), call);
				return argTypeFilteredOverloads != null && argTypeFilteredOverloads.Count != 0 ? 
					argTypeFilteredOverloads[0] : null;
			}
		}

		void GetRawCallOverloads(PostfixExpression_MethodCall call, 
			out AbstractType[] baseExpression, 
			out ISymbolValue baseValue, 
			out TemplateInstanceExpression tix, 
			out bool isUFCSFunction)
		{
			baseExpression = null;
			baseValue = null;
			tix = null;
			isUFCSFunction = false;

			// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.DontResolveBaseTypes;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				if (pac.AccessExpression is TemplateInstanceExpression)
					tix = (TemplateInstanceExpression)pac.AccessExpression;

				var vs = E(pac, out isUFCSFunction, null, false);

				if (vs != null && vs.Length != 0)
				{
					if (vs[0] is ISymbolValue)
					{
						baseValue = (ISymbolValue)vs[0];
						baseExpression = new[] { baseValue.RepresentedType };
					}
					else if (vs[0] is InternalOverloadValue)
						baseExpression = ((InternalOverloadValue)vs[0]).Overloads;
					else
						baseExpression = TypeDeclarationResolver.Convert(vs);
				}
			}
			else
			{
				if (eval)
				{
					if (call.PostfixForeExpression is TemplateInstanceExpression)
					{
						tix = (TemplateInstanceExpression)call.PostfixForeExpression;
						baseValue = E(tix, false) as ISymbolValue;
					}
					else if (call.PostfixForeExpression is IdentifierExpression)
						baseValue = E((IdentifierExpression)call.PostfixForeExpression, false) as ISymbolValue;
					else
						baseValue = E(call.PostfixForeExpression) as ISymbolValue;

					if (baseValue is InternalOverloadValue)
						baseExpression = ((InternalOverloadValue)baseValue).Overloads;
					else
						baseExpression = new[] { baseValue.RepresentedType };
				}
				else
				{
					if (call.PostfixForeExpression is TemplateInstanceExpression)
						baseExpression = GetOverloads(tix=(TemplateInstanceExpression)call.PostfixForeExpression, null, false);
					else if (call.PostfixForeExpression is IdentifierExpression)
						baseExpression = GetOverloads((IdentifierExpression)call.PostfixForeExpression);
					else
						baseExpression = new[] { AbstractType.Get(E(call.PostfixForeExpression)) };
				}
			}

			ctxt.CurrentContext.ContextDependentOptions = optBackup;
		}

		public static AbstractType[] GetAccessedOverloads(PostfixExpression_Access acc, ResolverContextStack ctxt, out bool IsUFCS,
			ISemantic resultBase = null, bool DeducePostfixTemplateParams = true)
		{
			return TypeDeclarationResolver.Convert(new Evaluation(ctxt).E(acc, out IsUFCS, resultBase, DeducePostfixTemplateParams));
		}

		/// <summary>
		/// Returns either all unfiltered and undeduced overloads of a member of a base type/value (like b from type a if the expression is a.b).
		/// if <param name="EvalAndFilterOverloads"></param> is false.
		/// If true, all overloads will be deduced, filtered and evaluated, so that (in most cases,) a one-item large array gets returned
		/// which stores the return value of the property function b that is executed without arguments.
		/// Also handles UFCS - so if filtering is wanted, the function becom
		/// </summary>
		ISemantic[] E(PostfixExpression_Access acc, out bool IsUFCS,
			ISemantic resultBase = null, bool EvalAndFilterOverloads = true)
		{
			IsUFCS = false;

			if (acc == null)
				return null;

			var baseExpression = resultBase ?? E(acc.PostfixForeExpression);

			if (acc.AccessExpression is NewExpression)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
				return null;
			}

			/*
			 * Try to get ufcs functions at first!
			 * 
			 * void foo(int i) {}
			 * 
			 * class A
			 * {
			 *	void foo(int i, int a) {}
			 * 
			 *	void bar(){
			 *		123.foo(23); // Not allowed! 
			 *		// Anyway, if we tried to search ufcs functions AFTER searching from child to parent scope levels,
			 *		// it would return the local foo() only, not the global one..which would be an error then!
			 *  }
			 *  
			 * Probably also worth to notice is the property syntax..are property functions rather preferred than ufcs ones?
			 * }
			 */
			var	overloads = UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt) as AbstractType[];

			if (overloads == null)
			{
				if (acc.AccessExpression is TemplateInstanceExpression)
				{
					var tix = (TemplateInstanceExpression)acc.AccessExpression;
					// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
					overloads = GetOverloads(tix, resultBase == null ? null : new[] { AbstractType.Get(resultBase) }, EvalAndFilterOverloads);
				}

				else if (acc.AccessExpression is IdentifierExpression)
				{
					var id = ((IdentifierExpression)acc.AccessExpression).Value as string;

					overloads = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id, new[] { AbstractType.Get(baseExpression) }, ctxt, acc.AccessExpression);

					// Might be a static property
					if (overloads == null)
					{
						var staticTypeProperty = StaticPropertyResolver.TryResolveStaticProperties(AbstractType.Get(baseExpression), id, ctxt);

						if (staticTypeProperty != null)
							return new[] { staticTypeProperty };
					}
				}
				else
				{
					if (eval)
						throw new EvaluationException(acc, "Invalid access expression");
					ctxt.LogError(acc, "Invalid post-dot expression");
					return null;
				}
			}
			else
				IsUFCS = true;


			// If evaluation active and the access expression is stand-alone, return a single item only.
			if (EvalAndFilterOverloads && eval)
				return new[] { TryDoCTFEOrGetValueRefs(overloads, acc.AccessExpression) };

			return overloads;
		}

		ISemantic E(PostfixExpression_Index x, ISemantic foreExpression)
		{
			if (eval)
			{
				//TODO: Access pointer arrays(?)

				if (foreExpression is ArrayValue) // ArrayValue must be checked first due to inheritance!
				{
					var av = foreExpression as ArrayValue;

					// Make $ operand available
					var arrLen_Backup = ValueProvider.CurrentArrayLength;
					ValueProvider.CurrentArrayLength = av.Elements.Length;

					var n = E(x.Arguments[0]) as PrimitiveValue;

					ValueProvider.CurrentArrayLength = arrLen_Backup;

					if (n == null)
						throw new EvaluationException(x.Arguments[0], "Returned no value");

					int i = 0;
					try
					{
						i = Convert.ToInt32(n.Value);
					}
					catch { throw new EvaluationException(x.Arguments[0], "Index expression must be of type int"); }

					if (i < 0 || i > av.Elements.Length)
						throw new EvaluationException(x.Arguments[0], "Index out of range - it must be between 0 and " + av.Elements.Length);

					return av.Elements[i];
				}
				else if (foreExpression is AssociativeArrayValue)
				{
					var aa = (AssociativeArrayValue)foreExpression;

					var key = E(x.Arguments[0]);

					if (key == null)
						throw new EvaluationException(x.Arguments[0], "Returned no value");

					ISymbolValue val = null;

					foreach (var kv in aa.Elements)
						if (kv.Key.Equals(key))
							return kv.Value;

					throw new EvaluationException(x, "Could not find key '" + val + "'");
				}

				throw new EvaluationException(x.PostfixForeExpression, "Invalid index expression base value type", foreExpression);
			}
			else
			{
				if (foreExpression is AssocArrayType)
				{
					var ar = (AssocArrayType)foreExpression;
					/*
					 * myType_Array[0] -- returns TypeResult myType
					 * return the value type of a given array result
					 */
					//TODO: Handle opIndex overloads

					return ar.ValueType;
				}
				/*
				 * int* a = new int[10];
				 * 
				 * a[0] = 12;
				 */
				else if (foreExpression is PointerType)
					return ((PointerType)foreExpression).Base;

				ctxt.LogError(new ResolutionError(x, "Invalid base type for index expression"));
			}

			return null;
		}

		ISemantic E(PostfixExpression_Slice x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression; // Still of the array's type.
			

			if (!(foreExpression is ArrayValue))
				throw new EvaluationException(x.PostfixForeExpression, "Must be an array");

			var ar = (ArrayValue)foreExpression;
			var sl = (PostfixExpression_Slice)x;

			// If the [ ] form is used, the slice is of the entire array.
			if (sl.FromExpression == null && sl.ToExpression == null)
				return foreExpression;

			// Make $ operand available
			var arrLen_Backup = ValueProvider.CurrentArrayLength;
			ValueProvider.CurrentArrayLength = ar.Elements.Length;

			var bound_lower = E(sl.FromExpression) as PrimitiveValue;
			var bound_upper = E(sl.ToExpression) as PrimitiveValue;

			ValueProvider.CurrentArrayLength = arrLen_Backup;

			if (bound_lower == null || bound_upper == null)
				throw new EvaluationException(bound_lower == null ? sl.FromExpression : sl.ToExpression, "Must be of an integral type");

			int lower = -1, upper = -1;
			try
			{
				lower = Convert.ToInt32(bound_lower.Value);
				upper = Convert.ToInt32(bound_upper.Value);
			}
			catch { throw new EvaluationException(lower != -1 ? sl.FromExpression : sl.ToExpression, "Boundary expression must base an integral type"); }

			if (lower < 0)
				throw new EvaluationException(sl.FromExpression, "Lower boundary must be greater than 0");
			if (lower >= ar.Elements.Length)
				throw new EvaluationException(sl.FromExpression, "Lower boundary must be smaller than " + ar.Elements.Length);
			if (upper < lower)
				throw new EvaluationException(sl.ToExpression, "Upper boundary must be greater than " + lower);
			if (upper >= ar.Elements.Length)
				throw new EvaluationException(sl.ToExpression, "Upper boundary must be smaller than " + ar.Elements.Length);


			var rawArraySlice = new ISymbolValue[upper - lower];
			int j = 0;
			for (int i = lower; i < upper; i++)
				rawArraySlice[j++] = ar.Elements[i];

			return new ArrayValue(ar.RepresentedType as ArrayType, rawArraySlice);
		}

		ISemantic E(PostfixExpression_Increment x, ISemantic foreExpression)
		{
			// myInt++ is still of type 'int'
			if (!eval)
				return foreExpression;

			if (resolveConstOnly)
				throw new NoConstException(x);
			// Must be implemented anyway regarding ctfe
			return null;
		}

		ISemantic E(PostfixExpression_Decrement x, ISemantic foreExpression)
		{
			if (!eval)
				return foreExpression;

			if (resolveConstOnly)
				throw new NoConstException(x);
			// Must be implemented anyway regarding ctfe
			return null;
		}
	}
}
