using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		#region Properties / Ctor
		/// <summary>
		/// True, if the expression's value shall be evaluated.
		/// False, if the expression's type is wanted only.
		/// </summary>
		private readonly bool eval;
		private readonly ResolverContextStack ctxt;
		/// <summary>
		/// Is not null if the expression value shall be evaluated.
		/// </summary>
		private readonly AbstractSymbolValueProvider ValueProvider;
		bool resolveConstOnly { get { return ValueProvider == null || ValueProvider.ConstantOnly; } set { if(ValueProvider!=null) ValueProvider.ConstantOnly = value; } }

		private Evaluation(AbstractSymbolValueProvider vp) { 
			this.ValueProvider = vp; 
			this.eval = true;
			this.ctxt = vp.ResolutionContext;
		}
		private Evaluation(ResolverContextStack ctxt) {
			this.ctxt = ctxt;
		}
		#endregion

		/// <summary>
		/// Uses the standard value provider for expression value evaluation
		/// </summary>
		public static ISymbolValue EvaluateValue(IExpression x, ResolverContextStack ctxt)
		{
			try
			{
				return EvaluateValue(x, new StandardValueProvider(ctxt));
			}
			catch
			{
				//TODO Redirect evaluation exception to some outer logging service
			}
			return null;
		}

		public static ISymbolValue EvaluateValue(IExpression x, AbstractSymbolValueProvider vp)
		{
			if (vp == null)
				vp = new StandardValueProvider(null);

			return new Evaluation(vp).E(x) as ISymbolValue;
		}

		public static AbstractType EvaluateType(IExpression x, ResolverContextStack ctxt)
		{
			return AbstractType.Get(new Evaluation(ctxt).E(x));
		}

		ISemantic E(IExpression x)
		{
			if (x is Expression) // a,b,c;
			{
				var ex = (Expression)x;
				/*
				 * The left operand of the ',' is evaluated, then the right operand is evaluated. 
				 * The type of the expression is the type of the right operand, 
				 * and the result is the result of the right operand.
				 */

				if (eval)
				{
					for (int i = 0; i < ex.Expressions.Count; i++)
					{
						var v = E(ex.Expressions[i]);

						if (i == ex.Expressions.Count - 1)
							return v;
					}

					throw new EvaluationException(x, "There must be at least one expression in the expression chain");
				}
				else
					return ex.Expressions.Count == 0 ? null : E(ex.Expressions[ex.Expressions.Count - 1]);
			}

			else if (x is SurroundingParenthesesExpression)
				return E((x as SurroundingParenthesesExpression).Expression);

			else if (x is ConditionalExpression) // a ? b : c
				return E((ConditionalExpression)x);

			else if (x is OperatorBasedExpression)
				return E((OperatorBasedExpression)x);

			else if (x is UnaryExpression)
				return E((UnaryExpression)x);

			else if (x is PostfixExpression)
				return E((PostfixExpression)x);

			else if (x is PrimaryExpression)
				return E((PrimaryExpression)x);

			return null;
		}

		public static bool IsFalseZeroOrNull(ISymbolValue v)
		{
			var pv = v as PrimitiveValue;
			if (pv != null)
				try
				{
					return !Convert.ToBoolean(pv.Value);
				}
				catch { }
			else
				return v is NullValue;

			return v != null;
		}

		/// <summary>
		/// Removes all variable references by resolving them via the given value provider.
		/// Useful when only the value is of interest, not its container or other things.
		/// </summary>
		public static ISymbolValue GetVariableContents(ISymbolValue v, AbstractSymbolValueProvider vp)
		{
			while (v is VariableValue)
				v = vp[((VariableValue)v).Variable];

			return v;
		}
	}
}
