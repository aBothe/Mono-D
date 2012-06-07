using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		ResolverContextStack ctxt;

		private ExpressionEvaluator() { }

		public static bool IsEqual(IExpression ex, IExpression ex2, ResolverContextStack ctxt)
		{
			var val_x1 = Evaluate(ex, ctxt);
			var val_x2 = Evaluate(ex2, ctxt);

			//TEMPORARILY: Remove the string comparison
			if (val_x1 == null && val_x2 == null)
				return ex.ToString() == ex2.ToString();

			return IsEqual(val_x1, val_x2);
		}

		public static bool IsEqual(IExpressionValue val_x1, IExpressionValue val_x2)
		{
			//TODO
			return val_x1 != null && val_x2 != null && val_x1.Value == val_x2.Value;
		}

		public static ResolveResult Resolve(IExpression arg, ResolverContextStack ctxt)
		{
			var ev=Evaluate(arg, ctxt);

			if (ev == null)
				return null;

			return new ExpressionValueResult{ 
				DeclarationOrExpressionBase=arg, 
				Value=ev
			};
		}

		public static IExpressionValue Evaluate(IExpression expression, ResolverContextStack ctxt)
		{
			return new ExpressionEvaluator { ctxt = ctxt }.Evaluate(expression);
		}

		/// <summary>
		/// Tries to evaluate a const initializer of the const/enum variable passed in by r
		/// </summary>
		/// <param name="r">Contains a member result that holds a const'ed variable with a static initializer</param>
		public static IExpressionValue TryToEvaluateConstInitializer(
			IEnumerable<ResolveResult> r,
			ResolverContextStack ctxt)
		{
			// But: If it's a variable that represents a const value..
			var r_noAlias = DResolver.TryRemoveAliasesFromResult(r);
			if (r_noAlias != null)
				foreach (var r_ in r_noAlias)
				{
					if (r_ is MemberResult)
					{
						var n = ((MemberResult)r_).Node as DVariable;

						if (n != null && n.IsConst)
						{
							// .. resolve it's pre-compile time value and make the returned value the given argument
							var val = Evaluate(n.Initializer, ctxt);

							if (val != null && val.Value != null)
								return val;
						}
					}
				}
			return null;
		}

		public IExpressionValue Evaluate(IExpression x)
		{
			//if (x is PrimaryExpression)
				//return Evaluate((PrimaryExpression)x);
			if (x is TypeDeclarationExpression)
				return Evaluate((TypeDeclarationExpression)x);

			return null;
		}

		public IExpressionValue Evaluate(TypeDeclarationExpression x)
		{
			var r=TypeDeclarationResolver.Resolve(x.Declaration, ctxt);

			if(r!=null)
				return TryToEvaluateConstInitializer(r,ctxt);
			return null;
		}

		#region Helpers
		public static bool ToBool(object value)
		{
			bool b = false;

			try
			{
				b = Convert.ToBoolean(value);
			}
			catch { }

			return b;
		}

		public static double ToDouble(object value)
		{
			double d = 0;

			try
			{
				d = Convert.ToDouble(value);
			}
			catch { }

			return d;
		}

		public static long ToLong(object value)
		{
			long d = 0;

			try
			{
				d = Convert.ToInt64(value);
			}
			catch { }

			return d;
		}
		#endregion
	}
}
