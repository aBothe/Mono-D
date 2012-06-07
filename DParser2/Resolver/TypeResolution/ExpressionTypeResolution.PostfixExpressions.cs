using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Resolver.TypeResolution
{
	public partial class ExpressionTypeResolver
	{
		public static ResolveResult[] Resolve(PostfixExpression ex, ResolverContextStack ctxt)
		{
			if (ex is PostfixExpression_MethodCall)
				return Resolve(ex as PostfixExpression_MethodCall, ctxt);

			var baseExpression = Resolve(ex.PostfixForeExpression, ctxt);

			if (baseExpression == null)
				return null;

			// Important: To ensure correct behaviour, aliases must be removed before further handling
			baseExpression = DResolver.TryRemoveAliasesFromResult(baseExpression);

			if (baseExpression == null ||
				ex is PostfixExpression_Increment || // myInt++ is still of type 'int'
				ex is PostfixExpression_Decrement)
				return baseExpression;

			if (ex is PostfixExpression_Access)
				return Resolve(ex as PostfixExpression_Access, ctxt, baseExpression);

			var r = new List<ResolveResult>(baseExpression.Length);
			foreach (var b in baseExpression)
			{
				ResolveResult[] arrayBaseType = null;
				if (b is MemberResult)
					arrayBaseType = (b as MemberResult).MemberBaseTypes;
				else
					arrayBaseType = new[] { b };

                if (arrayBaseType == null)
                    return null;

				if (ex is PostfixExpression_Index)
				{
					foreach (var rr in arrayBaseType)
					{
						if (rr is ArrayResult)
						{
							var ar = rr as ArrayResult;
							/*
							    * myType_Array[0] -- returns TypeResult myType
							    * return the value type of a given array result
							    */
							//TODO: Handle opIndex overloads

							if (ar != null && ar.ResultBase != null)
								r.Add(ar.ResultBase);
						}
						/*
						    * int* a = new int[10];
						    * 
						    * a[0] = 12;
						    */
						else if (rr is StaticTypeResult && rr.DeclarationOrExpressionBase is PointerDecl)
							r.Add(rr.ResultBase);
					}
				}
				else if (ex is PostfixExpression_Slice)
				{
					/*
					 * myType_Array[0 .. 5] -- returns an array
					 */

					foreach (ArrayResult ar in arrayBaseType)
					{
						//TODO: Handle opSlice overloads

						r.Add(ar);
					}
				}
			}

			if (r.Count > 0)	
				return r.ToArray();
			return null;
		}

		public static ResolveResult[] Resolve(PostfixExpression_MethodCall call, ResolverContextStack ctxt, bool returnBaseTypesOnly = true)
		{
			// Deduce template parameters later on
			ResolveResult[] baseExpression = null;
			TemplateInstanceExpression tix=null;

			// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.DontResolveBaseTypes;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac=(PostfixExpression_Access)call.PostfixForeExpression;
				if (pac.AccessExpression is TemplateInstanceExpression)
					tix = (TemplateInstanceExpression)pac.AccessExpression;

				baseExpression = Resolve(pac, ctxt, null, call);
			}
			else if (call.PostfixForeExpression is TemplateInstanceExpression)
			{
				tix=(TemplateInstanceExpression)call.PostfixForeExpression;
				baseExpression = Resolve(tix, ctxt, null, false);
			}
			else
				baseExpression = Resolve(call.PostfixForeExpression, ctxt);

			ctxt.CurrentContext.ContextDependentOptions = optBackup;

			var methodOverloads = new List<ResolveResult>();

			#region Search possible methods, opCalls or delegates that could be called
			bool requireStaticItems = true;
			IEnumerable<ResolveResult> scanResults = DResolver.TryRemoveAliasesFromResult(baseExpression);
			var nextResults = new List<ResolveResult>();

			while (scanResults != null)
			{
				foreach (var b in scanResults)
				{
					if (b is MemberResult)
					{
						var mr = b as MemberResult;

						if (mr.Node is DMethod)
						{
							methodOverloads.Add(mr);
							continue;
						}

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
						if (mr.MemberBaseTypes != null)
						{
							nextResults.AddRange(mr.MemberBaseTypes);

							requireStaticItems = false;
						}
					}
					else if (b is DelegateResult)
					{
						var dg = b as DelegateResult;

						/*
						 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
						 * auto dg=delegate(x) {return x*3;};
						 * int b = dg(4);
						 */

						if (!dg.IsDelegateDeclaration)
							methodOverloads.Add(dg);
					}
					else if (b is TypeResult)
					{
						/*
						 * auto a = MyStruct(); -- opCall-Overloads can be used
						 */
						var classDef = (b as TypeResult).Node as DClassLike;

						if (classDef == null)
							continue;

						foreach (var i in classDef)
							if (i.Name == "opCall" && i is DMethod &&	(!requireStaticItems || (i as DNode).IsStatic))
								methodOverloads.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call));

						/*
						 * Every struct can contain a default ctor:
						 * 
						 * struct S { int a; bool b; }
						 * 
						 * auto s = S(1,true); -- ok
						 * auto s2= new S(2,false); -- error, no constructor found!
						 */
						if (classDef.ClassType == DTokens.Struct && methodOverloads.Count == 0)
						{
							//TODO: Enable returning further results
							return new[] { b };
						}
					}
				}

				scanResults = nextResults.Count==0 ? null:nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			#region Deduce template parameters and filter out unmatching overloads

			// UFCS argument assignment will be done per-overload and in the EvalAndFilterOverloads method!

			// First add optionally given template params
			// http://dlang.org/template.html#function-templates
			var resolvedCallArguments = tix==null ? 
				new List<ResolveResult[]>() :
				TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);

			// Then add the arguments' types
			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					resolvedCallArguments.Add(ExpressionTypeResolver.Resolve(arg, ctxt));

			var filteredMethods = TemplateInstanceHandler.EvalAndFilterOverloads(
				methodOverloads,
				resolvedCallArguments.Count > 0 ? resolvedCallArguments.ToArray() : null, 
				true, ctxt);

			if (!returnBaseTypesOnly)
			{
				if (filteredMethods == null || filteredMethods.Length == 0)
					return null;

				foreach (var m in filteredMethods)
				{
					var mr = m as MemberResult;
					mr.MemberBaseTypes = TypeDeclarationResolver.GetMethodReturnType(mr.Node as DMethod, ctxt);
				}

				return filteredMethods;
			}

			methodOverloads.Clear();
			if(filteredMethods!=null)
				methodOverloads.AddRange(filteredMethods);
			#endregion

			var r = new List<ResolveResult>();

			foreach (var rr in methodOverloads)
			{
				if (rr is MemberResult)
				{
					var mr = (MemberResult)rr;
					TypeDeclarationResolver.FillMethodReturnType(mr, ctxt);

					if (mr.MemberBaseTypes != null)
						r.AddRange(mr.MemberBaseTypes);
				}
				else if (rr is DelegateResult)
				{
					var dg = (DelegateResult)rr;
					TypeDeclarationResolver.FillMethodReturnType(dg, ctxt);

					if (dg.ReturnType != null)
						r.AddRange(dg.ReturnType);
				}
			}

			if (r.Count != 0)
				return r.ToArray();

			return null;
		}

		public static ResolveResult[] Resolve(PostfixExpression_Access acc, 
			ResolverContextStack ctxt, 
			ResolveResult[] resultBases = null,
			IExpression supExpression=null)
		{
			var baseExpression = resultBases ?? Resolve(acc.PostfixForeExpression, ctxt);

			if (acc.AccessExpression is TemplateInstanceExpression)
			{
				// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
				var res=Resolve((TemplateInstanceExpression)acc.AccessExpression, ctxt, baseExpression, 
					!(supExpression is PostfixExpression_MethodCall));

				// Try to resolve ufcs(?)
				if (res == null && baseExpression!=null && baseExpression.Length!=0)
					return UFCSResolver.TryResolveUFCS(baseExpression[0], acc, ctxt);
				
				return res;
			}
			else if (acc.AccessExpression is NewExpression)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
			}
			else if (acc.AccessExpression is IdentifierExpression)
			{
				var id = ((IdentifierExpression)acc.AccessExpression).Value as string;
				/*
				 * First off, try to resolve the identifier as it was a type declaration's identifer list part
				 */
				var results = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id, baseExpression, ctxt, acc);

				if (results != null)
					return results;

				/*
				 * Handle cases which can occur in an expression context only
				 */

				foreach (var b in baseExpression)
				{
					/*
					 * 1) UFCS
					 * 2) Static properties 
					 */
					var ufcsResult = UFCSResolver.TryResolveUFCS(b, acc, ctxt);

					if (ufcsResult != null)
						return ufcsResult;

					var staticTypeProperty = StaticPropertyResolver.TryResolveStaticProperties(b, id, ctxt);

					if (staticTypeProperty != null)
						return new[] { staticTypeProperty };
				}
			}
			else
				return baseExpression;

			return null;
		}

		public static ResolveResult[] Resolve(
			TemplateInstanceExpression tix,
			ResolverContextStack ctxt,
			IEnumerable<ResolveResult> resultBases = null,
			bool deduceParameters = true)
		{
			ResolveResult[] res = null;
			if (resultBases == null)
				res= TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdentifier.Id, ctxt, tix, tix.TemplateIdentifier.ModuleScoped);
			else
				res= TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdentifier.Id, resultBases, ctxt, tix);

			return !ctxt.Options.HasFlag(ResolutionOptions.NoTemplateParameterDeduction) && deduceParameters ?
				TemplateInstanceHandler.EvalAndFilterOverloads(res,tix, ctxt) : res;
		}
	}
}
