using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public class PrimitiveValue : IExpressionValue
	{
		public PrimitiveType Type
		{
			get;
			private set;
		}

		public ITypeDeclaration RepresentedType
		{
			get
			{

				return null;
			}
			set
			{
				
			}
		}

		public object Value
		{
			get;
			private set;
		}

		public IExpression BaseExpression
		{
			get;
			private set;
		}

		public PrimitiveValue(PrimitiveType ValueType, object Value, IExpression Expression)
		{
			this.Type = ValueType;
			this.Value = Value;
			this.BaseExpression = Expression;
		}

		/// <summary>
		/// Returns true if the represented value is either null (ref type), 0 (int/float), false (bool) or empty (string)
		/// </summary>
		public bool IsNullFalseOrEmpty
		{
			get {
				if (Value == null)
					return true;

				try
				{
					switch (Type)
					{
						case PrimitiveType.Bool:
							return !Convert.ToBoolean(Value);
						case PrimitiveType.Char:
							var c = Convert.ToChar(Value);

							return c == '\0';
					}
				}
				catch {}
				return false;
			}
		}
	}

	public enum PrimitiveType
	{
		Bool=1,
		Char=2,
		Int=4,
		Float=8,
		String=16,

		Pointer=32,
		Array=64,
		Reference=128,
	}
}
