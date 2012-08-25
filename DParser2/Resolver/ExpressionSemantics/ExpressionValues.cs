using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class PrimitiveValue : ExpressionValue
	{
		public readonly int BaseTypeToken;

		/// <summary>
		/// To make math operations etc. more efficient, use the largest available structure to store scalar values.
		/// Also representing single characters etc.
		/// </summary>
		public readonly decimal Value;
		/// <summary>
		/// (For future use) For complex number handling, there's an extra value for storing the imaginary part of a number.
		/// </summary>
		public readonly decimal ImaginaryPart;

		public PrimitiveValue(bool Value, IExpression Expression)
			: this(DTokens.Bool, Value ? 1 : 0, Expression) { }

		public PrimitiveValue(int BaseTypeToken, decimal Value, IExpression Expression, decimal ImaginaryPart = 0M)
			: base(ExpressionValueType.Primitive, new PrimitiveType(BaseTypeToken,0, Expression))
		{
			this.BaseTypeToken = BaseTypeToken;
			this.Value = Value;
			this.ImaginaryPart = ImaginaryPart;
		}

		/// <summary>
		/// NaN constructor
		/// </summary>
		private PrimitiveValue(int baseType,IExpression x)
			: base(ExpressionValueType.Primitive, new PrimitiveType(baseType, 0, x))
		{
			IsNaN = true;
		}

		public readonly bool IsNaN;

		public static PrimitiveValue CreateNaNValue(IExpression x, int baseType = DTokens.Float)
		{
			return new PrimitiveValue(baseType, x);
		}

		public override string ToCode()
		{
			switch (BaseTypeToken)
			{
				case DTokens.Void:
					return "void";
				case DTokens.Bool:
					return Value == 1M ? "true" : "false";
				case DTokens.Char:
				case DTokens.Wchar:
				case DTokens.Dchar:
					return Char.ConvertFromUtf32((int)Value);
			}

			return Value.ToString() + (ImaginaryPart == 0 ? "" : ("+"+ImaginaryPart.ToString()+"i"));
		}
	}

	public class VoidValue : PrimitiveValue
	{
		public VoidValue(IExpression x)
			: base(DTokens.Void, 0M, x)
		{ }
	}

	#region Derived data types
	public class ArrayValue : ExpressionValue
	{
		#region Properties
		public bool IsString { get { return StringValue != null; } }

		/// <summary>
		/// If this represents a string, the string will be returned. Otherwise null.
		/// </summary>
		public string StringValue { get; private set; }
		public readonly LiteralSubformat StringFormat;

		/// <summary>
		/// If not a string, the evaluated elements will be returned. Otherwise null.
		/// </summary>
		public ISymbolValue[] Elements
		{
			get;// { return elements != null ? elements.ToArray() : null; }
			private set;
		}
		#endregion

		#region Ctor
		/// <summary>
		/// String constructor.
		/// Given result stores both type and idenfitierexpression whose Value is used as content
		/// </summary>
		public ArrayValue(ArrayType stringLiteralResult, IdentifierExpression stringLiteral=null)
			: base(ExpressionValueType.Array, stringLiteralResult, stringLiteral)
		{
			StringFormat = LiteralSubformat.Utf8;
			if (stringLiteralResult.DeclarationOrExpressionBase is IdentifierExpression)
			{
				StringFormat = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Subformat;
				StringValue = ((IdentifierExpression)stringLiteralResult.DeclarationOrExpressionBase).Value as string;
			}
			else
				StringValue = stringLiteral.Value as string;
		}

		/// <summary>
		/// String constructor.
		/// Used for generating string results 'internally'.
		/// </summary>
		public ArrayValue(ArrayType stringTypeResult, IExpression baseExpression, string content)
			: base(ExpressionValueType.Array, stringTypeResult, baseExpression)
		{
			StringFormat = LiteralSubformat.Utf8;
			StringValue = content;
		}

		public ArrayValue(ArrayType resolvedArrayType, params ISymbolValue[] elements)
			: base(ExpressionValueType.Array, resolvedArrayType)
		{
			Elements = elements;
		}
		#endregion

		public override string ToCode()
		{
			if (IsString)
			{
				var suff = "";

				if (StringFormat.HasFlag(LiteralSubformat.Utf16))
					suff = "w";
				else if (StringFormat.HasFlag(LiteralSubformat.Utf32))
					suff = "d";

				return "\"" + StringValue + "\"" + suff;
			}

			var s = "[";

			if (Elements != null)
				foreach (var e in Elements)
					if (e == null)
						s += "[null], ";
					else
						s += e.ToCode() + ", ";

			return s.TrimEnd(',',' ') + "]";
		}
	}

	public class AssociativeArrayValue : ExpressionValue
	{
		public ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>> Elements
		{
			get;
			private set;
		}

		public AssociativeArrayValue(AssocArrayType baseType, IExpression baseExpression,IList<KeyValuePair<ISymbolValue,ISymbolValue>> Elements)
			: base(ExpressionValueType.AssocArray, baseType, baseExpression)
		{
			this.Elements = new ReadOnlyCollection<KeyValuePair<ISymbolValue, ISymbolValue>>(Elements);
		}

		public override string ToCode()
		{
			var s = "[";

			if(Elements!=null)
				foreach (var e in Elements)
				{
					var k = e.Key == null ? "[null]" : e.Key.ToCode();
					var v = e.Value == null ? "[null]" : e.Value.ToCode();

					s += k + ":" + v + ", ";
				}

			return s.TrimEnd(',',' ') + "]";
		}
	}

	/// <summary>
	/// Used for both delegates and function references.
	/// </summary>
	public class DelegateValue : ExpressionValue
	{
		public AbstractType Definition { get; private set; }
		public bool IsFunction { get { return base.Type == ExpressionValueType.Function; } }

		public DMethod Method
		{
			get
			{
				if (Definition is DelegateType)
				{
					var dg = (DelegateType)Definition;

					if (dg.IsFunctionLiteral)
						return ((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod;
				}
				return Definition is DSymbol ? ((DSymbol)Definition).Definition as DMethod : null;
			}
		}

		public DelegateValue(DelegateType Dg)
			: base(ExpressionValueType.Delegate, Dg)
		{
			this.Definition = Dg;
		}

		public DelegateValue(AbstractType Definition, AbstractType ReturnType, bool IsFunction = false)
			: base(IsFunction ? ExpressionValueType.Function : ExpressionValueType.Delegate, ReturnType, Definition.DeclarationOrExpressionBase as IExpression)
		{
			this.Definition = Definition;
		}

		public override string ToCode()
		{
			return Definition == null ? "[null delegate]" : Definition.ToCode();
		}
	}
	#endregion

	#region User data types

	public abstract class InstanceValue : ReferenceValue
	{
		public readonly DClassLike Definition;
		public Dictionary<DVariable, ISymbolValue> Members = new Dictionary<DVariable, ISymbolValue>();
		public Dictionary<DVariable, AbstractType> MemberTypes = new Dictionary<DVariable, AbstractType>();

		public InstanceValue(DClassLike Class, AbstractType ClassType, IExpression instanceExpression)
			: base(Class, ClassType, instanceExpression)
		{

		}

		/// <summary>
		/// Initializes all variables that have gotten an explicit initializer.
		/// </summary>
		public void RunInitializers()
		{

		}
	}

	/// <summary>
	/// Stores a type. Used e.g. as foreexpressions for PostfixExpressions.
	/// </summary>
	public class TypeValue : ExpressionValue
	{
		public TypeValue(AbstractType r, IExpression x)
			: base(ExpressionValueType.Type, r, x) { }

		public override string ToCode()
		{
			return BaseExpression != null ? BaseExpression.ToString() : RepresentedType != null ? RepresentedType.ToString() : "null";
		}
	}

	public abstract class ReferenceValue : ExpressionValue
	{
		INode referencedNode;

		public ReferenceValue(INode Node, AbstractType type, IExpression x)
			: base(ExpressionValueType.None, type, x)
		{
		}
	}
	/*
	public class AliasValue : ExpressionValue
	{
		public AliasValue(IExpression x, MemberResult AliasResult)
			: base(ExpressionValueType.Alias, AliasResult, x) { }
	}

	public class InstanceValue : ExpressionValue
	{
		
	}

	public class StructInstanceValue : ExpressionValue
	{

	}

	public class UnionInstanceValue : ExpressionValue
	{

	}

	public class EnumInstanceValue : ExpressionValue
	{

	}

	public class ClassInstanceValue : ExpressionValue
	{

	}
	*/

	public class NullValue : ReferenceValue
	{
		public NullValue(IExpression x) : base(null, null, x) { }

		public override string ToCode()
		{
			return "null";
		}
	}
	#endregion

	/// <summary>
	/// Used when passing several function overloads from the inner evaluation function to the outer (i.e. method call) one.
	/// Not intended to be used in any other kind.
	/// </summary>
	public class InternalOverloadValue : ExpressionValue
	{
		public AbstractType[] Overloads { get; private set; }

		public InternalOverloadValue(AbstractType[] overloads, IExpression x)
			: base(ExpressionValueType.None, null, x)
		{
			this.Overloads = overloads;
		}

		public override string ToCode()
		{
			var s = "[Overloads array: ";

			if(Overloads!=null)
				foreach (var o in Overloads)
					s += o.ToCode() + ",";

			return s.TrimEnd(',') + "]";
		}
	}
}
