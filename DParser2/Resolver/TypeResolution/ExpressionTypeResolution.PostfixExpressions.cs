using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.TypeResolution
{
	public partial class ExpressionTypeResolver
	{
		public static ResolveResult[] Resolve(PostfixExpression ex, ResolverContextStack ctxt)
		{
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

			else if (ex is PostfixExpression_MethodCall)
				return Resolve(ex as PostfixExpression_MethodCall, ctxt, baseExpression);

			var r = new List<ResolveResult>(baseExpression.Length);
			foreach (var b in baseExpression)
			{
				ResolveResult[] arrayBaseType = null;
				if (b is MemberResult)
					arrayBaseType = (b as MemberResult).MemberBaseTypes;
				else
					arrayBaseType = new[] { b };

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

		public static ResolveResult[] Resolve(PostfixExpression_MethodCall call, ResolverContextStack ctxt, ResolveResult[] baseExpression=null)
		{
			if(baseExpression==null)
				baseExpression = Resolve(call.PostfixForeExpression, ctxt);

			#region Search possible methods, opCalls or delegates that could be called
			var methodContainingResultsToCheck = new List<ResolveResult>();

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
							methodContainingResultsToCheck.Add(mr);
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
							methodContainingResultsToCheck.Add(dg);
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
								methodContainingResultsToCheck.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call));
					}
				}

				scanResults = nextResults.Count==0 ? null:nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodContainingResultsToCheck.Count == 0)
				return null;

			#region Compare (template) parameters with given arguments to filter out unwanted overloads
			var resolvedCallArguments = new List<ResolveResult[]>();

			// Note: If an arg wasn't able to be resolved (returns null) - add it anyway to keep the indexes parallel
			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					resolvedCallArguments.Add(ExpressionTypeResolver.Resolve(arg, ctxt));

			/*
			 * std.stdio.writeln(123) does actually contain
			 * a template instance argument: writeln!int(123);
			 * So although there's no explicit type given, 
			 * TemplateParameters will still contain static type int!
			 * 
			 * Therefore, and only if no writeln!int was given as foreexpression like in writeln!int(123),
			 * treat the call arguments (here: 123) as types and try to match them to at least one of the method results
			 * 
			 * If no template parameters were required, baseExpression will remain untouched.
			 */

			if (!(call.PostfixForeExpression is TemplateInstanceExpression))
			{
				var filteredMethods = TemplateInstanceResolver.ResolveAndFilterTemplateResults(
					resolvedCallArguments.Count > 0 ? resolvedCallArguments.ToArray() : null,
					methodContainingResultsToCheck, ctxt, false);

				methodContainingResultsToCheck.Clear();
				methodContainingResultsToCheck.AddRange(filteredMethods);
			}
			#endregion

			var r = new List<ResolveResult>();

			foreach (var rr in methodContainingResultsToCheck)
			{
				if (rr is MemberResult)
				{
					var mr = (MemberResult)rr;

					if (mr.MemberBaseTypes != null)
						r.AddRange(mr.MemberBaseTypes);
				}
				else if (rr is DelegateResult)
				{
					var dg = (DelegateResult)rr;

					if (dg.ReturnType != null)
						r.AddRange(dg.ReturnType);
				}
			}

			if (r.Count != 0)
				return r.ToArray();

			return null;
		}

		public static ResolveResult[] Resolve(PostfixExpression_Access acc, ResolverContextStack ctxt, ResolveResult[] resultBases = null)
		{
			var baseExpression = resultBases ?? Resolve(acc.PostfixForeExpression, ctxt);

			if (acc.TemplateInstance != null)
				return Resolve(acc.TemplateInstance, ctxt, baseExpression);
			else if (acc.NewExpression != null)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
			}
			else if (acc.Identifier != null)
			{
				/*
				 * First off, try to resolve the identifier as it was a type declaration's identifer list part
				 */
				var results = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(acc.Identifier,baseExpression,ctxt,acc);

				if (results != null)
					return results;

				/*
				 * Handle cases which can occur in an expression context only
				 */

				foreach (var b in baseExpression)
				{
					/*
					 * 1) Static properties
					 * 2) ??
					 */
					var staticTypeProperty = StaticPropertyResolver.TryResolveStaticProperties(b, acc.Identifier, ctxt);

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
			IEnumerable<ResolveResult> resultBases = null)
		{
			if (resultBases == null)
				return TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdentifier.Id, ctxt, tix);

			return TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdentifier.Id, resultBases, ctxt, tix);
		}
	}
}
