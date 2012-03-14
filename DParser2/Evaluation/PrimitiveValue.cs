using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Evaluation
{
	public struct PrimitiveValue
	{
		public readonly PrimitiveType Type;
		public readonly object Value;

		public PrimitiveValue(PrimitiveType ValueType, object Value)
		{
			this.Type = ValueType;
			this.Value = Value;
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
		Bool,
		Char,
		Int,
		Float,
		String,
		Reference
	}
}
