using System;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public interface ISymbolValue : IEquatable<ISymbolValue>, ISemantic
	{
		ExpressionValueType Type { get; }

		AbstractType RepresentedType { get; }
		IExpression BaseExpression { get; }
	}

	public abstract class ExpressionValue : ISymbolValue
	{
		IExpression _baseExpression;

		public ExpressionValue(ExpressionValueType Type,
			AbstractType RepresentedType)
		{
			this.Type = Type;
			this.RepresentedType = RepresentedType;
		}

		public ExpressionValue(ExpressionValueType Type,
			AbstractType RepresentedType,
			IExpression BaseExpression) : this(Type, RepresentedType)
		{
			this._baseExpression = BaseExpression;
		}

		public ExpressionValueType Type
		{
			get;
			private set;
		}

		public AbstractType RepresentedType
		{
			get;
			private set;
		}

		public IExpression BaseExpression
		{
			get { return _baseExpression!=null || RepresentedType == null ? _baseExpression : RepresentedType.DeclarationOrExpressionBase as IExpression; }
			private set {
				_baseExpression = value;
			}
		}

		public virtual bool Equals(ISymbolValue other)
		{
			return SymbolValueComparer.IsEqual(this, other);
		}

		public abstract string ToCode();

		public override string ToString()
		{
			try
			{
				return ToCode();
			}
			catch
			{ 
				return null; 
			}
		}

		public static implicit operator AbstractType(ExpressionValue v)
		{
			return v.RepresentedType;
		}
	}

	public enum ExpressionValueType
	{
		/// <summary>
		/// Represents all Basic Data Types
		/// </summary>
		Primitive,

		// Derived Data Types
		Pointer,
		Array,
		AssocArray,
		Function,
		Delegate,

		// User data types
		Alias,
		Enum,
		Struct,
		Union,
		/// <summary>
		/// The expression returns a class instance
		/// </summary>
		Class,

		/// <summary>
		/// The expression isn't an expression but a type representation.
		/// Used when resolving static members of e.g. classes.
		/// </summary>
		Type,

		/// <summary>
		/// Unkown/Special value that's not intended to be used regularly
		/// </summary>
		None
	}
}
