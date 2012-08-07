using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(OperatorBasedExpression x, ISemantic lValue=null)
		{
			if (x is AssignExpression)
				return E((AssignExpression)x,lValue);

			// TODO: Implement operator precedence (see http://forum.dlang.org/thread/jjohpp$oj6$1@digitalmars.com )

			if (x is XorExpression || // a ^ b
				x is OrExpression || // a | b
				x is AndExpression || // a & b
				x is ShiftExpression || // a << 8
				x is AddExpression || // a += b; a -= b;
				x is MulExpression || // a *= b; a /= b; a %= b;
				x is CatExpression || // a ~= b;
				x is PowExpression) // a ^^ b;
				return E_MathOp(x, lValue);

			else if (x is EqualExpression) // a==b
				return E((EqualExpression)x, lValue);

			else if (x is OrOrExpression || // a || b
				x is AndAndExpression || // a && b
				x is IdendityExpression || // a is T
				x is RelExpression) // a <= b
				return E_BoolOp(x, lValue as ISymbolValue);

			else if (x is InExpression) // a in b
				return E((InExpression)x, lValue);

			throw new WrongEvaluationArgException();
		}

		ISemantic E(AssignExpression x, ISemantic lValue=null)
		{
			if (!eval)
				return E(x.LeftOperand);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));

			//TODO

			return null;
		}

		ISemantic E_BoolOp(OperatorBasedExpression x, ISemantic lValue=null, ISemantic rValue = null)
		{
			if (!eval)
				return new PrimitiveType(DTokens.Bool);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));
			var r = TryGetValue(rValue ?? E(x.RightOperand));

			if (x is OrOrExpression)
			{
				// The OrOrExpression evaluates its left operand. 
				// If the left operand, converted to type bool, evaluates to true, 
				// then the right operand is not evaluated. If the result type of the OrOrExpression 
				// is bool then the result of the expression is true. 
				// If the left operand is false, then the right operand is evaluated. 
				// If the result type of the OrOrExpression is bool then the result 
				// of the expression is the right operand converted to type bool.
				return new PrimitiveValue(!(IsFalseZeroOrNull(l) && IsFalseZeroOrNull(r)), x);
			}
			else if (x is AndAndExpression)
				return new PrimitiveValue(!IsFalseZeroOrNull(l) && !IsFalseZeroOrNull(r), x);
			else if (x is IdendityExpression)
			{
				// http://dlang.org/expression.html#IdentityExpression
			}
			else if (x is RelExpression)
			{
				return HandleSingleMathOp(x, l, r, (a,b, op) => {

					// Unordered-ness is when at least one operator is Not any Number (NaN)
					bool unordered = a.IsNaN || b.IsNaN;

					bool relationIsTrue=false;
					bool cmpIm = a.ImaginaryPart != 0 || b.ImaginaryPart != 0;

					switch(x.OperatorToken)
					{
						case DTokens.GreaterThan: // greater, >
							relationIsTrue = a.Value > b.Value && (cmpIm ? a.ImaginaryPart > b.ImaginaryPart : true);
							break;
						case DTokens.GreaterEqual: // greater or equal, >=
							relationIsTrue = a.Value >= b.Value && a.ImaginaryPart >= b.ImaginaryPart;
							break;
						case DTokens.LessThan: // less, <
							relationIsTrue = a.Value < b.Value && (cmpIm ? a.ImaginaryPart < b.ImaginaryPart : true);
							break;
						case DTokens.LessEqual: // less or equal, <=
							relationIsTrue = a.Value <= b.Value && a.ImaginaryPart <= b.ImaginaryPart;
							break;
						case DTokens.Unordered: // unordered, !<>=
							relationIsTrue = unordered;
							break;
						case DTokens.LessOrGreater: // less or greater, <>
							relationIsTrue = (a.Value < b.Value || a.Value > b.Value) && (cmpIm ? 
								(a.ImaginaryPart < b.ImaginaryPart || a.ImaginaryPart > b.ImaginaryPart) : true);
							break;
						case DTokens.LessEqualOrGreater: // less, equal, or greater, <>=
							relationIsTrue = (a.Value < b.Value || a.Value >= b.Value) && (cmpIm ?
								(a.ImaginaryPart < b.ImaginaryPart || a.ImaginaryPart >= b.ImaginaryPart) : true);
							break;
						case DTokens.UnorderedOrGreater: // unordered or greater, !<=
							relationIsTrue = unordered || (a.Value > b.Value && (cmpIm ? a.ImaginaryPart > b.ImaginaryPart : true));
							break;
						case DTokens.UnorderedGreaterOrEqual: // unordered, greater, or equal, !<
							relationIsTrue = unordered || (a.Value >= b.Value && a.ImaginaryPart >= b.ImaginaryPart);
							break;
						case DTokens.UnorderedOrLess: // unordered or less, !>=
							relationIsTrue = unordered || (a.Value < b.Value && (cmpIm ? a.ImaginaryPart < b.ImaginaryPart : true));
							break;
						case DTokens.UnorderedLessOrEqual: // unordered, less, or equal, !>
							relationIsTrue = unordered || (a.Value <= b.Value && a.ImaginaryPart <= b.ImaginaryPart);
							break;
						case DTokens.UnorderedOrEqual: // unordered or equal, !<>
							relationIsTrue = unordered || (a.Value == b.Value && a.ImaginaryPart == b.ImaginaryPart);
							break;
					}

					return new PrimitiveValue(relationIsTrue, op);
				}, false);
			}

			throw new WrongEvaluationArgException();
		}

		ISemantic E(EqualExpression x, ISemantic lValue =null, ISemantic rValue = null)
		{
			var l = TryGetValue(lValue ?? E(x.LeftOperand));
			var r = TryGetValue(rValue ?? E(x.RightOperand));

			bool isEq = false;

			// If they are integral values or pointers, equality is defined as the bit pattern of the type matches exactly
			if (l is PrimitiveValue && r is PrimitiveValue)
			{
				var pv_l = (PrimitiveValue)l;
				var pv_r = (PrimitiveValue)r;

				isEq = pv_l.Value == pv_r.Value && pv_l.ImaginaryPart == pv_r.ImaginaryPart;
			}

			/*
			 * Furthermore TODO: object comparison, pointer content comparison
			 */

			return new PrimitiveValue(x.OperatorToken == DTokens.Equal ? isEq : !isEq, x);
		}

		/// <summary>
		/// a + b; a - b; etc.
		/// </summary>
		ISemantic E_MathOp(OperatorBasedExpression x, ISemantic lValue=null, ISemantic rValue=null)
		{
			if (!eval)
				return lValue ?? E(x.LeftOperand);

			var l = TryGetValue(lValue ?? E(x.LeftOperand));

			if (l == null)
			{
				/*
				 * In terms of adding opOverloading later on, 
				 * lvalue not being a PrimitiveValue shouldn't be a problem anymore - we simply had to
				 * search the type of l for methods called opAdd etc. and call that method via ctfe.
				 * Finally, return the value the opAdd method passed back - and everything is fine.
				 */

				/*
				 * Also, pointers should be implemented later on.
				 * http://dlang.org/expression.html#AddExpression
				 */

				throw new EvaluationException(x, "Left value must evaluate to a constant scalar value. Operator overloads aren't supported yet", lValue);
			}

			//TODO: Operator overloading

			// Note: a * b + c is theoretically treated as a * (b + c), but it's needed to evaluate it as (a * b) + c !
			if (x is MulExpression || x is PowExpression)
			{
				if (x.RightOperand is OperatorBasedExpression && !(x.RightOperand is AssignExpression)) //TODO: This must be true only if it's a math expression, so not an assign expression etc.
				{
					var sx = (OperatorBasedExpression)x.RightOperand;

					// Now multiply/divide/mod expression 'l' with sx.LeftOperand
					var intermediateResult = HandleSingleMathOp(x, l, E(sx.LeftOperand), mult);

					// afterwards, evaluate the operation between the result just returned and the sx.RightOperand.
					return E(sx, intermediateResult);
				}

				return HandleSingleMathOp(x, l, rValue ?? E(x.RightOperand), mult);
			}

			var r = TryGetValue(rValue ?? E(x.RightOperand));

			if(r == null)
				throw new EvaluationException(x, "Right operand must evaluate to a value", lValue);

			/*
			 * TODO: Handle invalid values/value ranges.
			 */

			if (x is XorExpression)
			{
				return HandleSingleMathOp(x, l,r, (a,b)=>{
					EnsureIntegralType(a);EnsureIntegralType(b);
					return (long)a.Value ^ (long)b.Value;
				});
			}
			else if (x is OrExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					return (long)a.Value | (long)b.Value;
				});
			}
			else if (x is AndExpression)
			{
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					return (long)a.Value & (long)b.Value;
				});
			}
			else if (x is ShiftExpression) 
				return HandleSingleMathOp(x, l, r, (a, b) =>
				{
					EnsureIntegralType(a); EnsureIntegralType(b);
					if (b.Value < 0 || b.Value > 31)
						throw new EvaluationException(b.BaseExpression, "Shift operand must be between 0 and 31", b);

					switch(x.OperatorToken)
					{
						case DTokens.ShiftLeft:
							return (long)a.Value << (int)b.Value; // TODO: Handle the imaginary part
						case DTokens.ShiftRight:
							return (long)a.Value >> (int)b.Value;
						case DTokens.ShiftRightUnsigned: //TODO: Find out where's the difference between >> and >>>
							return (ulong)a.Value >> (int)(uint)b.Value;
					}

					throw new EvaluationException(x, "Invalid token for shift expression", l,r);
				});
			else if (x is AddExpression)
				return HandleSingleMathOp(x, l, r, (a, b, op) =>
				{
					switch (op.OperatorToken)
					{
						case DTokens.Plus:
							return new PrimitiveValue(a.BaseTypeToken, a.Value + b.Value, x, a.ImaginaryPart + b.ImaginaryPart);
						case DTokens.Minus:
							return new PrimitiveValue(a.BaseTypeToken, a.Value - b.Value, x, a.ImaginaryPart - b.ImaginaryPart);
					}

					throw new EvaluationException(x, "Invalid token for add/sub expression", l, r);
				});
			else if (x is CatExpression)
			{
				// Notable: If one element is of the value type of the array, the element is added (either at the front or at the back) to the array

				var av_l = l as ArrayValue;
				var av_r = r as ArrayValue;

				if (av_l!=null && av_r!=null)
				{
					// Ensure that both arrays are of the same type
					if(!ResultComparer.IsEqual(av_l.RepresentedType, av_r.RepresentedType))
						throw new EvaluationException(x, "Both arrays must be of same type", l,r);

					// Might be a string
					if (av_l.IsString && av_r.IsString)
						return new ArrayValue(av_l.RepresentedType as ArrayType, x, av_l.StringValue + av_r.StringValue);
					else
					{
						var elements = new ISymbolValue[av_l.Elements.Length + av_r.Elements.Length];
						Array.Copy(av_l.Elements, 0, elements, 0, av_l.Elements.Length);
						Array.Copy(av_r.Elements, 0, elements, av_l.Elements.Length, av_r.Elements.Length);

						return new ArrayValue(av_l.RepresentedType as ArrayType, elements);
					}
				}

				ArrayType at = null;

				// Append the right value to the array
				if (av_l!=null &&  (at=av_l.RepresentedType as ArrayType) != null &&
					ResultComparer.IsImplicitlyConvertible(r.RepresentedType, at.ValueType, ctxt))
				{
					var elements = new ISymbolValue[av_l.Elements.Length + 1];
					Array.Copy(av_l.Elements, elements, av_l.Elements.Length);
					elements[elements.Length - 1] = r;

					return new ArrayValue(at, elements);
				}
				// Put the left value into the first position
				else if (av_r != null && (at = av_r.RepresentedType as ArrayType) != null &&
					ResultComparer.IsImplicitlyConvertible(l.RepresentedType, at.ValueType, ctxt))
				{
					var elements = new ISymbolValue[1 + av_r.Elements.Length];
					elements[0] = l;
					Array.Copy(av_r.Elements,0,elements,1,av_r.Elements.Length);

					return new ArrayValue(at, elements);
				}

				throw new EvaluationException(x, "At least one operand must be an (non-associative) array. If so, the other operand must be of the array's element type.", l, r);
			}
			
			throw new WrongEvaluationArgException();
		}

		ISymbolValue TryGetValue(ISemantic s)
		{
			if (s is VariableValue)
				return ValueProvider[((VariableValue)s).Variable];

			return s as ISymbolValue;
		}

		static PrimitiveValue mult(PrimitiveValue a, PrimitiveValue b, OperatorBasedExpression x)
		{
			decimal v = 0;
			decimal im=0;
			switch (x.OperatorToken)
			{
				case DTokens.Pow:
					v = (decimal)Math.Pow((double)a.Value, (double)b.Value);
					v = (decimal)Math.Pow((double)a.ImaginaryPart, (double)b.ImaginaryPart);
					break;
				case DTokens.Times:
					v= a.Value * b.Value;
					im=a.ImaginaryPart * b.ImaginaryPart;
					break;
				case DTokens.Div:
					if ((a.Value!=0 && b.Value == 0) || (a.ImaginaryPart!=0 && b.ImaginaryPart==0))
						throw new EvaluationException(x, "Right operand must not be 0");
					if(b.Value!=0)
						v= a.Value / b.Value;
					if(b.ImaginaryPart!=0)
						im=a.ImaginaryPart / b.ImaginaryPart;
					break;
				case DTokens.Mod:
					if ((a.Value!=0 && b.Value == 0) || (a.ImaginaryPart!=0 && b.ImaginaryPart==0))
						throw new EvaluationException(x, "Right operand must not be 0");
					if(b.Value!=0)
						v= a.Value % b.Value;
					if(b.ImaginaryPart!=0)
						im=a.ImaginaryPart % b.ImaginaryPart;
					break;
				default:
					throw new EvaluationException(x, "Invalid token for multiplication expression (*,/,% only)");
			}

			return new PrimitiveValue(a.BaseTypeToken, v, x, im);
		}

		void EnsureIntegralType(PrimitiveValue v)
		{
			if (!DTokens.BasicTypes_Integral[v.BaseTypeToken])
				throw new EvaluationException(v.BaseExpression, "Literal must be of integral type",v);
		}

		delegate decimal MathOp(PrimitiveValue x, PrimitiveValue y) ;
		delegate PrimitiveValue MathOp2(PrimitiveValue x, PrimitiveValue y, OperatorBasedExpression op);

		/// <summary>
		/// Handles mathemathical operation.
		/// If l and r are both primitive values, the MathOp delegate is executed.
		/// 
		/// TODO: Operator overloading.
		/// </summary>
		ISemantic HandleSingleMathOp(IExpression x, ISemantic l, ISemantic r, MathOp m)
		{
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
			{
				// If one 
				if (pl.IsNaN || pr.IsNaN)
					return PrimitiveValue.CreateNaNValue(x, pl.IsNaN ? pl.BaseTypeToken : pr.BaseTypeToken);

				return new PrimitiveValue(pl.BaseTypeToken, m(pl, pr), x);
			}

			throw new NotImplementedException("Operator overloading not implemented yet.");
		}

		ISemantic HandleSingleMathOp(OperatorBasedExpression x, ISemantic l, ISemantic r, MathOp2 m, bool UnorderedCheck = true)
		{
			var pl = l as PrimitiveValue;
			var pr = r as PrimitiveValue;

			//TODO: imaginary/complex parts

			if (pl != null && pr != null)
			{
				if (UnorderedCheck && (pl.IsNaN || pr.IsNaN))
					return PrimitiveValue.CreateNaNValue(x, pl.IsNaN ? pl.BaseTypeToken : pr.BaseTypeToken);

				return m(pl, pr, x);
			}

			throw new NotImplementedException("Operator overloading not implemented yet.");
		}

		ISemantic E(ConditionalExpression x)
		{
			if (eval)
			{
				var b = E(x.OrOrExpression) as ISymbolValue;

				if (IsFalseZeroOrNull(b))
					return E(x.FalseCaseExpression);
				else
					return E(x.TrueCaseExpression);
			}

			return E(x.TrueCaseExpression);
		}

		ISemantic E(InExpression x, ISemantic l=null)
		{
			// The return value of the InExpression is null if the element is not in the array; 
			// if it is in the array it is a pointer to the element.

			return E(x.RightOperand);
		}
	}
}
