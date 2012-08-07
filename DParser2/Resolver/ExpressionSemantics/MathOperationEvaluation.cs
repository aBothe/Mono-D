using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class MathOperationEvaluation
	{
		public enum MathOp
		{
			Add,
			Sub,
			Mul,
			Div,

			Xor,
			Or,
			And,

			AndAnd,
			OrOr
		}

		public static bool TryCalc(object a, object b, MathOp op, out object x)
		{
			x = null;

			try
			{
				if (a is int)
				{
					var i1 = Convert.ToInt32(a);
					var i2 = Convert.ToInt32(b);

					switch (op)
					{
						case MathOp.Add:
							x = i1 + i2;
							break;
						case MathOp.Sub:
							x = i1 - i2;
							break;
						case MathOp.Mul:
							x = i1 * i2;
							break;
						case MathOp.Div:
							x = i1 / i2;
							break;

						case MathOp.Xor:
							x = i1 ^ i2;
							break;
						case MathOp.Or:
							x = i1 | i2;
							break;
						case MathOp.And:
							x = i1 & i2;
							break;

						case MathOp.AndAnd:
							break;
						case MathOp.OrOr:
							break;
					}
				}

			}
			catch (InvalidCastException exc)
			{
				return false;
			}

			return true;
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
