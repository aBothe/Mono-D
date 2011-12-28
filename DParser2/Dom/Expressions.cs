using System;
using System.Collections.Generic;
using D_Parser.Parser;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom.Expressions
{
	public class DExpressionDecl : AbstractTypeDeclaration
	{
		public IExpression Expression;

		public DExpressionDecl() { }

		public DExpressionDecl(IExpression dExpression)
		{
			this.Expression = dExpression;
		}

		public override string ToString(bool IncludeBase)
		{
			return (IncludeBase&& InnerDeclaration != null ? (InnerDeclaration.ToString()+'.') : "") + Expression.ToString();
		}
	}

	public delegate INode[] ResolveTypeHandler(string identifier);

	public interface IExpression
	{
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }

		ITypeDeclaration ExpressionTypeRepresentation{get;}
		bool IsConstant { get; }
		decimal DecValue { get; }
	}

	/// <summary>
	/// Expressions that contain other sub-expressions somewhere share this interface
	/// </summary>
	public interface ContainerExpression:IExpression
	{
		IExpression[] SubExpressions { get; }
	}

	public class ExpressionHelper
	{
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

		/// <summary>
		/// Scans through all container expressions recursively and returns the one that's nearest to 'Where'.
		/// Will return 'e' if nothing found or if there wasn't anything to scan
		/// </summary>
		public static IExpression SearchExpressionDeeply(IExpression e,CodeLocation Where)
		{
			while (e is ContainerExpression)
			{
				var currentContainer = e as ContainerExpression;

				if (!(e.Location <= Where || e.EndLocation >= Where))
					break;

				var subExpressions = currentContainer.SubExpressions;

				if (subExpressions == null || subExpressions.Length < 1)
					break;
				bool foundOne = false;
				foreach (var se in subExpressions)
					if (se != null && Where >= se.Location && Where <= se.EndLocation)
					{
						e = se;
						foundOne = true;
						break;
					}

				if (!foundOne)
					break;
			}

			return e;
		}
	}

	public abstract class OperatorBasedExpression : IExpression, ContainerExpression
	{
		public virtual IExpression LeftOperand { get; set; }
		public virtual IExpression RightOperand { get; set; }
		public int OperatorToken { get; protected set; }

		public override string ToString()
		{
			return LeftOperand.ToString() + DTokens.GetTokenString(OperatorToken) + (RightOperand != null ? RightOperand.ToString() : "");
		}

		public CodeLocation Location
		{
			get { return LeftOperand.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return RightOperand.EndLocation; }
		}

		public virtual bool IsConstant
		{
			get { return LeftOperand.IsConstant && RightOperand.IsConstant; }
		}

		public virtual decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public abstract ITypeDeclaration ExpressionTypeRepresentation
		{
			get;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{LeftOperand, RightOperand}; }
		}
	}

	public class Expression : IExpression, IEnumerable<IExpression>, ContainerExpression
	{
		public List<IExpression> Expressions = new List<IExpression>();

		public void Add(IExpression ex)
		{
			Expressions.Add(ex);
		}

		public IEnumerator<IExpression> GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public override string ToString()
		{
			var s = "";
			foreach (var ex in Expressions)
				s += ex.ToString() + ",";
			return s.TrimEnd(',');
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public CodeLocation Location
		{
			get { return Expressions[0].Location; }
		}

		public CodeLocation EndLocation
		{
			get { return Expressions[Expressions.Count].EndLocation; }
		}

		public bool IsConstant
		{
			get
			{
				foreach (var e in Expressions)
					if (!e.IsConstant)
						return false;
				return true;
			}
		}

		/*/// <summary>
		/// Will return the const value of the first expression only
		/// </summary>
		public object EvaluatedConstValue
		{
			get { return Expressions[0].EvaluatedConstValue; }
		}

		/// <summary>
		/// Will return all values
		/// </summary>
		public object[] EvaluatedConstValues
		{
			get
			{
				var l = new List<object>(Expressions.Count);
				foreach (var e in Expressions)
					l.Add(e.EvaluatedConstValue);

				return l.ToArray();
			}
		}*/


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return null; }
		}

		public decimal DecValue
		{
			get { return Expressions[0].DecValue; }
		}

		public IExpression[] SubExpressions
		{
			get { return Expressions.ToArray(); }
		}
	}

	public class AssignExpression : OperatorBasedExpression
	{
		public AssignExpression(int opToken) { OperatorToken = opToken; }
		
		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return RightOperand.ExpressionTypeRepresentation; }
		}

		public override bool IsConstant
		{
			get
			{
				return RightOperand.IsConstant;
			}
		}

		public override decimal DecValue
		{
			get
			{
				return RightOperand.DecValue;
			}
		}
	}

	public class ConditionalExpression : IExpression, ContainerExpression
	{
		public IExpression OrOrExpression { get; set; }

		public IExpression TrueCaseExpression { get; set; }
		public IExpression FalseCaseExpression { get; set; }

		public override string ToString()
		{
			return this.OrOrExpression.ToString() + "?" + TrueCaseExpression.ToString() +':' + FalseCaseExpression.ToString();
		}

		public CodeLocation Location
		{
			get { return OrOrExpression.Location; }
		}

		public CodeLocation EndLocation
		{
			get { return FalseCaseExpression.EndLocation; }
		}

		public bool IsConstant{	get { return OrOrExpression.IsConstant && TrueCaseExpression.IsConstant && FalseCaseExpression.IsConstant; }	}
		/*
		public object EvaluatedConstValue
		{
			get {
				var o = OrOrExpression.EvaluatedConstValue;
				return ExpressionHelper.ToBool(o)?TrueCaseExpression.EvaluatedConstValue : FalseCaseExpression.EvaluatedConstValue;
			}
		}*/

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return TrueCaseExpression.ExpressionTypeRepresentation; }
		}

		public decimal DecValue
		{
			get { return OrOrExpression.DecValue==1 ? TrueCaseExpression.DecValue : FalseCaseExpression.DecValue; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[] {OrOrExpression, TrueCaseExpression, FalseCaseExpression}; }
		}
	}

	public class OrOrExpression : OperatorBasedExpression
	{
		public OrOrExpression() { OperatorToken = DTokens.LogicalOr; }
		/*
		public override object EvaluatedConstValue
		{
			get
			{
				return ExpressionHelper.ToBool(LeftOperand.EvaluatedConstValue) || ExpressionHelper.ToBool(RightOperand.EvaluatedConstValue);
			}
		}*/

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public override decimal DecValue
		{
			get
			{
				return (LeftOperand.DecValue != 0 || RightOperand.DecValue != 0) ? 1 : 0;
			}
		}
	}

	public class AndAndExpression : OperatorBasedExpression
	{
		public AndAndExpression() { OperatorToken = DTokens.LogicalAnd; }
		/*
		public override object EvaluatedConstValue
		{
			get
			{
				return ExpressionHelper.ToBool(LeftOperand.EvaluatedConstValue) && ExpressionHelper.ToBool(RightOperand.EvaluatedConstValue);
			}
		}*/

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public override decimal DecValue
		{
			get
			{
				return (LeftOperand.DecValue != 0 && RightOperand.DecValue != 0) ? 1 : 0;
			}
		}
	}

	public class XorExpression : OperatorBasedExpression
	{
		public XorExpression() { OperatorToken = DTokens.Xor; }
		/*
		public override object EvaluatedConstValue
		{
			get
			{
				return ExpressionHelper.ToBool(LeftOperand.EvaluatedConstValue) ^ ExpressionHelper.ToBool(RightOperand.EvaluatedConstValue);
			}
		}*/

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return (long)LeftOperand.DecValue ^ (long)RightOperand.DecValue;
			}
		}
	}

	public class OrExpression : OperatorBasedExpression
	{
		public OrExpression() { OperatorToken = DTokens.BitwiseOr; }
		/*
		public override object EvaluatedConstValue
		{
			get
			{
				return ExpressionHelper.ToLong(LeftOperand.EvaluatedConstValue) | ExpressionHelper.ToLong(RightOperand.EvaluatedConstValue);
			}
		}*/

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return (long)LeftOperand.DecValue | (long)RightOperand.DecValue;
			}
		}
	}

	public class AndExpression : OperatorBasedExpression
	{
		public AndExpression() { OperatorToken = DTokens.BitwiseAnd; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return (long)LeftOperand.DecValue & (long)RightOperand.DecValue;
			}
		}
	}

	public class EqualExpression : OperatorBasedExpression
	{
		public EqualExpression(bool isUnEqual) { OperatorToken = isUnEqual ? DTokens.NotEqual : DTokens.Equal; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public override decimal DecValue
		{
			get
			{
				return (OperatorToken==DTokens.NotEqual?
					(LeftOperand.DecValue!=RightOperand.DecValue):
					(LeftOperand.DecValue==RightOperand.DecValue))?1:0;
			}
		}
	}

	public class IdendityExpression : OperatorBasedExpression
	{
		public bool Not;

		public IdendityExpression(bool notIs) { Not = notIs; OperatorToken = DTokens.Is; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "is " + RightOperand.ToString();
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}
	}

	public class RelExpression : OperatorBasedExpression
	{
		public RelExpression(int relationalOperator) { OperatorToken = relationalOperator; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public override decimal DecValue
		{
			get
			{
				bool ret = false;

				switch (OperatorToken)
				{
					case DTokens.LessThan:
						ret = LeftOperand.DecValue < RightOperand.DecValue;
						break;
					case DTokens.NotLessThan:
						ret = !(LeftOperand.DecValue < RightOperand.DecValue);
						break;
					case DTokens.LessEqual:
						ret = LeftOperand.DecValue <= RightOperand.DecValue;
						break;
					case DTokens.GreaterThan:
						ret = LeftOperand.DecValue > RightOperand.DecValue;
						break;
					case DTokens.NotGreaterThan:
						ret = !(LeftOperand.DecValue > RightOperand.DecValue);
						break;
					case DTokens.GreaterEqual:
						ret = LeftOperand.DecValue >= RightOperand.DecValue;
						break;
					case DTokens.UnequalAssign:
					case DTokens.Unequal:
						ret= LeftOperand.DecValue<RightOperand.DecValue || LeftOperand.DecValue>RightOperand.DecValue;
						break;
					case DTokens.NotUnequalAssign:
					case DTokens.NotUnequal:
						ret = !(LeftOperand.DecValue < RightOperand.DecValue || LeftOperand.DecValue > RightOperand.DecValue);
						break;
				}

				return ret?1:0;
			}
		}
	}

	public class InExpression : OperatorBasedExpression
	{
		public bool Not;

		public InExpression(bool notIn) { Not = notIn; OperatorToken = DTokens.In; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "in " + RightOperand.ToString();
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}
	}

	public class ShiftExpression : OperatorBasedExpression
	{
		public ShiftExpression(int shiftOperator) { OperatorToken = shiftOperator; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				switch (OperatorToken)
				{
					case DTokens.ShiftLeft:
						return (long)LeftOperand.DecValue << (int)RightOperand.DecValue;
					case DTokens.ShiftRightUnsigned:
						return (ulong)LeftOperand.DecValue >> (int)RightOperand.DecValue;
					case DTokens.ShiftRight:
						return (long)LeftOperand.DecValue >> (int)RightOperand.DecValue;
				}

				return 0;
			}
		}
	}

	public class AddExpression : OperatorBasedExpression
	{
		public AddExpression(bool isMinus) { OperatorToken = isMinus ? DTokens.Minus : DTokens.Plus; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return OperatorToken==DTokens.Minus?LeftOperand.DecValue-RightOperand.DecValue:LeftOperand.DecValue+RightOperand.DecValue;
			}
		}
	}

	public class MulExpression : OperatorBasedExpression
	{
		public MulExpression(int mulOperator) { OperatorToken = mulOperator; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				switch(OperatorToken)
				{
					case DTokens.Div:
						return LeftOperand.DecValue / RightOperand.DecValue;
					case DTokens.Times:
						return LeftOperand.DecValue * RightOperand.DecValue;
					case DTokens.Mod:
						return LeftOperand.DecValue % RightOperand.DecValue;
				}
				return 0;
			}
		}
	}

	public class CatExpression : OperatorBasedExpression
	{
		public CatExpression() { OperatorToken = DTokens.Tilde; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get {
				var lot = LeftOperand.ExpressionTypeRepresentation;

				if (lot is ArrayDecl)
					return lot;
				else
					return new ArrayDecl() { InnerDeclaration=lot};
			}
		}
	}

	public interface UnaryExpression : IExpression { }

	public class PowExpression : OperatorBasedExpression, UnaryExpression
	{
		public PowExpression() { OperatorToken = DTokens.Pow; }

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return LeftOperand.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return (decimal)Math.Pow((double)LeftOperand.DecValue,(double)RightOperand.DecValue);
			}
		}
	}

	public abstract class SimpleUnaryExpression : UnaryExpression, ContainerExpression
	{
		public abstract int ForeToken { get; }
		public IExpression UnaryExpression { get; set; }

		public override string ToString()
		{
			return DTokens.GetTokenString(ForeToken) + UnaryExpression.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get { return UnaryExpression.EndLocation; }
		}


		public abstract ITypeDeclaration ExpressionTypeRepresentation
		{
			get;
		}


		public virtual bool IsConstant
		{
			get { return UnaryExpression.IsConstant; }
		}

		public virtual decimal DecValue
		{
			get 
			{
				return UnaryExpression.DecValue;
			}
		}

		public virtual IExpression[] SubExpressions
		{
			get { return new[]{UnaryExpression}; }
		}
	}

	/// <summary>
	/// Creates a pointer from the trailing type
	/// </summary>
	public class UnaryExpression_And : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.BitwiseAnd; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new PointerDecl(UnaryExpression.ExpressionTypeRepresentation); }
		}
	}

	public class UnaryExpression_Increment : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Increment; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}
	}

	public class UnaryExpression_Decrement : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Decrement; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}
	}

	/// <summary>
	/// Gets the pointer base type
	/// </summary>
	public class UnaryExpression_Mul : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Times; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(this) {InnerDeclaration = UnaryExpression.ExpressionTypeRepresentation }; }
		}
	}

	public class UnaryExpression_Add : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Plus; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return Math.Abs(UnaryExpression.DecValue);
			}
		}
	}

	public class UnaryExpression_Sub : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Minus; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return -UnaryExpression.DecValue;
			}
		}
	}

	public class UnaryExpression_Not : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Not; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return UnaryExpression.DecValue==0 ? 1:0;
			}
		}
	}

	/// <summary>
	/// Bitwise negation operation:
	/// 
	/// int a=56;
	/// int b=~a;
	/// 
	/// b will be -57;
	/// </summary>
	public class UnaryExpression_Cat : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Tilde; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return UnaryExpression.ExpressionTypeRepresentation; }
		}

		public override decimal DecValue
		{
			get
			{
				return ~(long)UnaryExpression.DecValue;
			}
		}
	}

	/// <summary>
	/// (Type).Identifier
	/// </summary>
	public class UnaryExpression_Type : UnaryExpression
	{
		public ITypeDeclaration Type { get; set; }
		public string AccessIdentifier { get; set; }

		public override string ToString()
		{
			return "(" + Type.ToString() + ")." + AccessIdentifier;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(this); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}


	/// <summary>
	/// NewExpression:
	///		NewArguments Type [ AssignExpression ]
	///		NewArguments Type ( ArgumentList )
	///		NewArguments Type
	/// </summary>
	public class NewExpression : UnaryExpression, ContainerExpression
	{
		public ITypeDeclaration Type { get; set; }
		public IExpression[] NewArguments { get; set; }
		public IExpression[] Arguments { get; set; }

		/// <summary>
		/// true if new myType[10]; instead of new myType(1,"asdf"); has been used
		/// </summary>
		public bool IsArrayArgument { get; set; }

		public override string ToString()
		{
			var ret = "new";

			if (NewArguments != null)
			{
				ret += "(";
				foreach (var e in NewArguments)
					ret += e.ToString() + ",";
				ret = ret.TrimEnd(',') + ")";
			}

			if(Type!=null)
				ret += " " + Type.ToString();

			ret += IsArrayArgument ? '[' : '(';
			if(Arguments!=null)
				foreach (var e in Arguments)
					ret += e.ToString() + ",";

			ret = ret.TrimEnd(',') + (IsArrayArgument ? ']' : ')');

			return ret;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return Type; }
		}


		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get {
				var l = new List<IExpression>();

				if (NewArguments != null)
					l.AddRange(NewArguments);

				if (Arguments != null)
					l.AddRange(Arguments);

				if (l.Count > 0)
					return l.ToArray();

				return null;
			}
		}
	}

	/// <summary>
	/// NewArguments ClassArguments BaseClasslist { DeclDefs } 
	/// new ParenArgumentList_opt class ParenArgumentList_opt SuperClass_opt InterfaceClasses_opt ClassBody
	/// </summary>
	public class AnonymousClassExpression : UnaryExpression, ContainerExpression
	{
		public IExpression[] NewArguments { get; set; }
		public DClassLike AnonymousClass { get; set; }

		public IExpression[] ClassArguments { get; set; }

		public override string ToString()
		{
			var ret = "new";

			if (NewArguments != null)
			{
				ret += "(";
				foreach (var e in NewArguments)
					ret += e.ToString() + ",";
				ret = ret.TrimEnd(',') + ")";
			}

			ret += " class";

			if (ClassArguments != null)
			{
				ret += '(';
				foreach (var e in ClassArguments)
					ret += e.ToString() + ",";

				ret = ret.TrimEnd(',') + ")";
			}

			if (AnonymousClass != null && AnonymousClass.BaseClasses != null)
			{
				ret += ":";

				foreach (var t in AnonymousClass.BaseClasses)
					ret += t.ToString() + ",";

				ret = ret.TrimEnd(',');
			}

			ret += " {...}";

			return ret;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(this); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (NewArguments != null)
					l.AddRange(NewArguments);

				if (ClassArguments != null)
					l.AddRange(ClassArguments);

				//ISSUE: Add the Anonymous class object to the return list somehow?

				if (l.Count > 0)
					return l.ToArray();

				return null;
			}
		}
	}

	public class DeleteExpression : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Delete; }
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return null; }
		}

		public override bool IsConstant
		{
			get { return false; }
		}

		public override decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	/// <summary>
	/// CastExpression:
	///		cast ( Type ) UnaryExpression
	///		cast ( CastParam ) UnaryExpression
	/// </summary>
	public class CastExpression : UnaryExpression, ContainerExpression
	{
		public bool IsTypeCast
		{
			get { return Type != null; }
		}
		public IExpression UnaryExpression;

		public ITypeDeclaration Type { get; set; }
		public int[] CastParamTokens { get; set; } //TODO: Still unused

		public override string ToString()
		{
			var ret = "cast(";

			if (IsTypeCast)
				ret += Type.ToString();
			else
			{
				if(CastParamTokens!=null)
					foreach (var tk in CastParamTokens)
						ret += DTokens.GetTokenString(tk) + " ";
				ret = ret.TrimEnd(' ');
			}

			ret += ") ";
			
			if(UnaryExpression!=null)
				UnaryExpression.ToString();

			return ret;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get 
			{ 
				if(IsTypeCast)
					return Type;

				if (UnaryExpression != null)
					return UnaryExpression.ExpressionTypeRepresentation;

				return null;
			}
		}


		public bool IsConstant
		{
			get { return UnaryExpression.IsConstant; }
		}

		public decimal DecValue
		{
			get { return UnaryExpression.DecValue; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{UnaryExpression}; }
		}
	}

	public abstract class PostfixExpression : IExpression, ContainerExpression
	{
		public IExpression PostfixForeExpression { get; set; }

		public CodeLocation Location
		{
			get { return PostfixForeExpression.Location; }
		}

		public abstract CodeLocation EndLocation { get; set; }


		public virtual ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(this) { InnerDeclaration = PostfixForeExpression.ExpressionTypeRepresentation }; }
		}


		public virtual bool IsConstant
		{
			get { return false; }
		}

		public virtual decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public virtual IExpression[] SubExpressions
		{
			get { return new[]{PostfixForeExpression}; }
		}
	}

	/// <summary>
	/// PostfixExpression . Identifier
	/// PostfixExpression . TemplateInstance
	/// PostfixExpression . NewExpression
	/// </summary>
	public class PostfixExpression_Access : PostfixExpression
	{
		public IExpression NewExpression;
		public ITypeDeclaration TemplateOrIdentifier;

		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "." + 
				(TemplateOrIdentifier != null ? TemplateOrIdentifier.ToString() : 
				(NewExpression!=null? NewExpression.ToString(): ""));
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override ITypeDeclaration ExpressionTypeRepresentation
		{
			get {
				var t = TemplateOrIdentifier;

				if (t==null && NewExpression!=null)
					return NewExpression.ExpressionTypeRepresentation;

				if (t == null)
				{
					if (PostfixForeExpression != null)
						return PostfixForeExpression.ExpressionTypeRepresentation;
					return null;
				}

				t.InnerDeclaration = PostfixForeExpression.ExpressionTypeRepresentation;
				return t;
			}
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[]{NewExpression, PostfixForeExpression};
			}
		}
	}

	public class PostfixExpression_Increment : PostfixExpression
	{
		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "++";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public sealed override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return PostfixForeExpression.ExpressionTypeRepresentation; }
		}
	}

	public class PostfixExpression_Decrement : PostfixExpression
	{
		public override string ToString()
		{
			return PostfixForeExpression.ToString() + "--";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public sealed override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return PostfixForeExpression.ExpressionTypeRepresentation; }
		}
	}

	/// <summary>
	/// PostfixExpression ( )
	/// PostfixExpression ( ArgumentList )
	/// </summary>
	public class PostfixExpression_MethodCall : PostfixExpression
	{
		public IExpression[] Arguments;

		public int ArgumentCount
		{
			get { return Arguments == null ? 0 : Arguments.Length; }
		}

		public override string ToString()
		{
			var ret = PostfixForeExpression.ToString() + "(";

			if (Arguments != null)
				foreach (var a in Arguments)
					ret += a.ToString() + ",";

			return ret.TrimEnd(',') + ")";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public sealed override ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return PostfixForeExpression.ExpressionTypeRepresentation; }
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (Arguments != null)
					l.AddRange(Arguments);

				if (PostfixForeExpression != null)
					l.Add(PostfixForeExpression);

				return l.Count>0? l.ToArray():null;
			}
		}
	}

	/// <summary>
	/// IndexExpression:
	///		PostfixExpression [ ArgumentList ]
	/// </summary>
	public class PostfixExpression_Index : PostfixExpression
	{
		public IExpression[] Arguments;

		public override string ToString()
		{
			var ret = (PostfixForeExpression != null ? PostfixForeExpression.ToString() : "") + "[";

			if (Arguments != null)
				foreach (var a in Arguments)
					ret += a.ToString() + ",";

			return ret.TrimEnd(',') + "]";
		}

		public sealed override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				var l = new List<IExpression>();

				if (Arguments != null)
					l.AddRange(Arguments);

				if (PostfixForeExpression != null)
					l.Add(PostfixForeExpression);

				return l.Count > 0 ? l.ToArray() : null;
			}
		}
	}


	/// <summary>
	/// SliceExpression:
	///		PostfixExpression [ ]
	///		PostfixExpression [ AssignExpression .. AssignExpression ]
	/// </summary>
	public class PostfixExpression_Slice : PostfixExpression
	{
		public IExpression FromExpression;
		public IExpression ToExpression;

		public override string ToString()
		{
			var ret = PostfixForeExpression.ToString() + "[";

			if (FromExpression != null)
				ret += FromExpression.ToString();

			if (FromExpression != null && ToExpression != null)
				ret += "..";

			if (ToExpression != null)
				ret += ToExpression.ToString();

			return ret + "]";
		}

		public override CodeLocation EndLocation
		{
			get;
			set;
		}

		public override IExpression[] SubExpressions
		{
			get
			{
				return new[] { FromExpression, ToExpression};
			}
		}
	}

	public interface PrimaryExpression : IExpression { }

	public class TemplateInstanceExpression : AbstractTypeDeclaration,PrimaryExpression,ContainerExpression
	{
		public IdentifierDeclaration TemplateIdentifier;
		public IExpression[] Arguments;

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return TemplateIdentifier; }
		}

		public override string ToString(bool IncludesBase)
		{
			var ret = (IncludesBase && InnerDeclaration != null ? (InnerDeclaration.ToString() + ".") : "") + TemplateIdentifier + '!';

			if (Arguments != null)
			{
				if (Arguments.Length > 1)
				{
					ret += '(';
					foreach (var e in Arguments)
						ret += e.ToString() + ",";
					ret = ret.TrimEnd(',') + ")";
				}
				else if(Arguments.Length==1)
					ret += Arguments[0].ToString();
			}

			return ret;
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get { return Arguments; }
		}
	}

	/// <summary>
	/// Identifier as well as literal primary expression
	/// </summary>
	public class IdentifierExpression : PrimaryExpression
	{
		public bool IsIdentifier { get { return Value is string && Format==LiteralFormat.None; } }

		public readonly object Value;
		public readonly LiteralFormat Format;

		//public IdentifierExpression() { }
		public IdentifierExpression(object Val) { Value = Val; Format = LiteralFormat.None; }
		public IdentifierExpression(object Val, LiteralFormat LiteralFormat) { Value = Val; this.Format = LiteralFormat; }

		public override string ToString()
		{
			if(Format!=Parser.LiteralFormat.None)
				switch (Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						return "'"+Value+"'";
					case Parser.LiteralFormat.StringLiteral:
						return "\"" + Value + "\"";
					case Parser.LiteralFormat.VerbatimStringLiteral:
						return "r\"" + Value + "\"";
				}

			return Value==null?null: Value.ToString();
		}


		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { 
				if(Format==LiteralFormat.CharLiteral)
					return new DTokenDeclaration(DTokens.Char) { Location = this.Location, EndLocation = this.EndLocation };

				if ((Format & LiteralFormat.FloatingPoint) == LiteralFormat.FloatingPoint)
					return new DTokenDeclaration(DTokens.Float) { Location = this.Location, EndLocation = this.EndLocation };

				if (Format == LiteralFormat.Scalar)
					return new DTokenDeclaration(DTokens.Int) { Location = this.Location, EndLocation = this.EndLocation };

				//ISSUE: For easification, only work with strings, not wstrings or dstrings
				if (Format == LiteralFormat.StringLiteral || Format == LiteralFormat.VerbatimStringLiteral)
					return new IdentifierDeclaration("string") { Location=this.Location,EndLocation=this.EndLocation };

				return new IdentifierDeclaration(Value) { Location = this.Location, EndLocation = this.EndLocation };
			}
		}

		public bool IsConstant
		{
			get { return Format==LiteralFormat.Scalar; }
		}

		public decimal DecValue
		{
			get { return Convert.ToDecimal(Value); }
		}
	}

	public class TokenExpression : PrimaryExpression
	{
		public int Token;

		public TokenExpression() { }
		public TokenExpression(int T) { Token = T; }

		public override string ToString()
		{
			return DTokens.GetTokenString(Token);
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(Token) { Location = this.Location, EndLocation = this.EndLocation }; }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	/// <summary>
	/// BasicType . Identifier
	/// </summary>
	public class TypeDeclarationExpression : PrimaryExpression
	{
		public ITypeDeclaration Declaration;

		public TypeDeclarationExpression() { }
		public TypeDeclarationExpression(ITypeDeclaration td) { Declaration = td; }

		public override string ToString()
		{
			return Declaration != null ? Declaration.ToString() : "";
		}

		public CodeLocation Location
		{
			get { return Declaration!=null? Declaration.Location: CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Declaration != null ? Declaration.EndLocation : CodeLocation.Empty; }
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return Declaration; }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	/// <summary>
	/// auto arr= [1,2,3,4,5,6];
	/// </summary>
	public class ArrayLiteralExpression : PrimaryExpression,ContainerExpression
	{
		public ArrayLiteralExpression()
		{
			Expressions = new List<IExpression>();
		}

		public virtual List<IExpression> Expressions { get; set; }

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in Expressions)
				s += expr.ToString() + ", ";
			s = s.TrimEnd(' ', ',') + "]";
			return s;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get {

				var ret = new ArrayDecl();

				var exprs = Expressions;
				if (exprs.Count > 0)
					ret.ValueType = exprs[0].ExpressionTypeRepresentation;

				return ret; }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get { return Expressions!=null && Expressions.Count>0? Expressions.ToArray() : null; }
		}
	}

	public class AssocArrayExpression : PrimaryExpression,ContainerExpression
	{
		public IDictionary<IExpression, IExpression> KeyValuePairs = new Dictionary<IExpression, IExpression>();

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in KeyValuePairs)
				s += expr.Key.ToString() + ":" + expr.Value.ToString() + ", ";
			s = s.TrimEnd(' ', ',') + "]";
			return s;
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get {
				var ret = new ArrayDecl();

				if (KeyValuePairs.Count > 0)
					foreach(var kv in KeyValuePairs)
					{
						if(kv.Value!=null)
							ret.ValueType = kv.Value.ExpressionTypeRepresentation;

						if (kv.Key != null)
							ret.KeyType = kv.Key.ExpressionTypeRepresentation;

						// Break if we resolved both key and value types
						if (ret.ValueType!=null && ret.KeyType!=null)
							break;
					}

				return ret;
			}
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get {
				var l = new List<IExpression>();

				foreach (var kv in KeyValuePairs)
				{
					if(kv.Key!=null)
						l.Add(kv.Key);
					if(kv.Value!=null)
						l.Add(kv.Value);
				}

				return l.Count > 0 ? l.ToArray() : null;
			}
		}
	}

	public class FunctionLiteral : PrimaryExpression
	{
		public int LiteralToken = DTokens.Delegate;
		public bool IsLambda = false;

		public DMethod AnonymousMethod = new DMethod(DMethod.MethodType.AnonymousDelegate);

		public FunctionLiteral() { }
		public FunctionLiteral(int InitialLiteral) { LiteralToken = InitialLiteral; }

		public override string ToString()
		{
			if (IsLambda)
			{
				var s = "";

				if (AnonymousMethod.Parameters.Count == 1 && AnonymousMethod.Parameters[0].Type == null)
					s += AnonymousMethod.Parameters[0].Name;
				else
				{
					s += '(';
					foreach (var par in AnonymousMethod.Parameters)
					{
						s += par.ToString()+',';
					}

					s = s.TrimEnd(',')+')';
				}

				s += " => ";

				IStatement[] stmts=null;
				if (AnonymousMethod.Body != null && (stmts = AnonymousMethod.Body.SubStatements).Length > 0 &&
					stmts[0] is ReturnStatement)
					s += (stmts[0] as ReturnStatement).ReturnExpression.ToString();

				return s;
			}

			return DTokens.GetTokenString(LiteralToken) + (string.IsNullOrEmpty (AnonymousMethod.Name)?"": " ") + AnonymousMethod.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DelegateDeclaration() { IsFunction=LiteralToken==DTokens.Function, ReturnType=AnonymousMethod.Type, Parameters=AnonymousMethod.Parameters}; }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	public class AssertExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression[] AssignExpressions;

		public override string ToString()
		{
			var ret = "assert(";

			foreach (var e in AssignExpressions)
				ret += e.ToString() + ",";

			return ret.TrimEnd(',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public bool IsConstant
		{
			get {
				foreach (var e in AssignExpressions)
					if (!e.IsConstant)
						return false;
				return true; 
			}
		}

		public decimal DecValue
		{
			get {
				foreach (var e in AssignExpressions)
					if (e.DecValue == 0)
						return 0;

				return 1;
			}
		}

		public IExpression[] SubExpressions
		{
			get { return AssignExpressions; }
		}
	}

	public class MixinExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "mixin(" + AssignExpression.ToString() + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		//TODO: How to get this resolved?
		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return null; }
		}

		public bool IsConstant
		{
			get { return AssignExpression.IsConstant; }
		}

		public decimal DecValue
		{
			get { return AssignExpression.DecValue; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{AssignExpression}; }
		}
	}

	public class ImportExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression AssignExpression;

		public override string ToString()
		{
			return "import(" + AssignExpression.ToString() + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(new IdentifierExpression("", LiteralFormat.StringLiteral)); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{AssignExpression}; }
		}
	}

	public class TypeidExpression : PrimaryExpression,ContainerExpression
	{
		public ITypeDeclaration Type;
		public IExpression Expression;

		public override string ToString()
		{
			return "typeid(" + (Type != null ? Type.ToString() : Expression.ToString()) + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new IdentifierDeclaration("TypeInfo") { Location=Location, EndLocation=EndLocation, InnerDeclaration=new IdentifierDeclaration("object")}; }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get { 
				if(Expression!=null)
					return new[]{Expression};
				if (Type != null)
					return new[] { new TypeDeclarationExpression(Type)};
				return null;
			}
		}
	}

	public class IsExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression TestedExpression;
		public ITypeDeclaration TestedType;
		public string TypeAliasIdentifier;

		/// <summary>
		/// True if Type == TypeSpecialization instead of Type : TypeSpecialization
		/// </summary>
		public bool EqualityTest;

		public ITypeDeclaration TypeSpecialization;
		public int TypeSpecializationToken;

		public ITemplateParameter[] TemplateParameterList;

		public override string ToString()
		{
			var ret = "is(";

			if (TestedType != null)
				ret += TestedType.ToString();
			else if (TestedExpression != null)
				ret += TestedExpression.ToString();

			if (TypeAliasIdentifier != null)
				ret += ' ' + TypeAliasIdentifier;

			if (TypeSpecialization != null || TypeSpecializationToken!=0)
				ret +=(EqualityTest ? "==" : ":")+ (TypeSpecialization != null ? 
					TypeSpecialization.ToString() : // Either the specialization declaration
					DTokens.GetTokenString(TypeSpecializationToken)); // or the spec token

			if (TemplateParameterList != null)
			{
				ret += ",";
				foreach (var p in TemplateParameterList)
					ret += p.ToString() + ",";
			}

			return ret.TrimEnd(' ', ',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DTokenDeclaration(DTokens.Bool); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}

		public IExpression[] SubExpressions
		{
			get { 
				if(TestedExpression!=null)
				return new[]{TestedExpression};

				if (TestedType != null)
					return new[] { new TypeDeclarationExpression(TestedType)};

				return null;
			}
		}
	}

	public class TraitsExpression : PrimaryExpression
	{
		public string Keyword;

		public IEnumerable<TraitsArgument> Arguments;

		public override string ToString()
		{
			var ret = "__traits(" + Keyword;

			if (Arguments != null)
				foreach (var a in Arguments)
					ret += "," + a.ToString();

			return ret + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		//TODO: Get all the returned value types in detail
		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return Keyword.StartsWith("is")||Keyword.StartsWith("has")?new DTokenDeclaration(DTokens.Bool):new IdentifierDeclaration("object"); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	public class TraitsArgument
	{
		public ITypeDeclaration Type;
		public IExpression AssignExpression;

		public override string ToString()
		{
			return Type != null ? Type.ToString() : AssignExpression.ToString();
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}
	}

	/// <summary>
	/// ( Expression )
	/// </summary>
	public class SurroundingParenthesesExpression : PrimaryExpression,ContainerExpression
	{
		public IExpression Expression;

		public override string ToString()
		{
			return "(" + Expression.ToString() + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return Expression.ExpressionTypeRepresentation; }
		}

		public bool IsConstant
		{
			get { return Expression.IsConstant; }
		}

		public decimal DecValue
		{
			get { return Expression.DecValue; }
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Expression}; }
		}
	}



	#region Template parameters

	public interface ITemplateParameter
	{
		string Name { get; }
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }
	}

	public class TemplateParameterNode : AbstractNode
	{
		public readonly ITemplateParameter TemplateParameter;

		public TemplateParameterNode(ITemplateParameter param)
		{
			TemplateParameter = param;

			Name = param.Name;

			StartLocation = NameLocation = param.Location;
			EndLocation = param.EndLocation;
		}

		public sealed override string ToString()
		{
			return TemplateParameter.ToString();
		}

		public sealed override string ToString(bool Attributes, bool IncludePath)
		{
			return (GetNodePath(this, false) + "." + ToString()).TrimEnd('.');
		}
	}

	public class TemplateTypeParameter : ITemplateParameter
	{
		public string Name { get; set; }

		public ITypeDeclaration Specialization;
		public ITypeDeclaration Default;

		public sealed override string ToString()
		{
			var ret = Name;

			if (Specialization != null)
				ret += ":" + Specialization.ToString();

			if (Default != null)
				ret += "=" + Default.ToString();

			return ret;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateThisParameter : ITemplateParameter
	{
		public string Name { get { return FollowParameter.Name; } }

		public ITemplateParameter FollowParameter;

		public sealed override string ToString()
		{
			return "this" + (FollowParameter != null ? (" " + FollowParameter.ToString()) : "");
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateValueParameter : ITemplateParameter
	{
		public string Name { get; set; }
		public ITypeDeclaration Type;

		public IExpression SpecializationExpression;
		public IExpression DefaultExpression;

		public override string ToString()
		{
			return (Type!=null?(Type.ToString() + " "):"") + Name/*+ (SpecializationExpression!=null?(":"+SpecializationExpression.ToString()):"")+
				(DefaultExpression!=null?("="+DefaultExpression.ToString()):"")*/;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateAliasParameter : TemplateValueParameter
	{
		public ITypeDeclaration SpecializationType;
		public ITypeDeclaration DefaultType;

		public sealed override string ToString()
		{
			return "alias " + base.ToString();
		}
	}

	public class TemplateTupleParameter : ITemplateParameter
	{
		public string Name { get; set; }

		public sealed override string ToString()
		{
			return Name + " ...";
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	#endregion

	#region Initializers

	public interface DInitializer : IExpression { }

	public class VoidInitializer : TokenExpression, DInitializer
	{
		public VoidInitializer() : base(DTokens.Void) { }
	}

	public class ArrayInitializer : ArrayLiteralExpression, DInitializer
	{
		public ArrayMemberInitializer[] ArrayMemberInitializations;

		public sealed override List<IExpression> Expressions
		{
			get
			{
				if (ArrayMemberInitializations == null)
					return new List<IExpression>();
				var l = new List<IExpression>(ArrayMemberInitializations.Length);
				foreach (var ami in ArrayMemberInitializations)
					l.Add( ami.Left);

				return l;
			}
			set { }
		}

		public sealed override string ToString()
		{
			var ret = "[";

			if (ArrayMemberInitializations != null)
				foreach (var i in ArrayMemberInitializations)
					ret += i.ToString() + ",";

			return ret.TrimEnd(',') + "]";
		}
	}

	public class ArrayMemberInitializer
	{
		public IExpression Left;
		public IExpression Specialization;

		public sealed override string ToString()
		{
			return Left.ToString() + (Specialization != null ? (":" + Specialization.ToString()) : "");
		}
	}

	public class StructInitializer : DInitializer
	{
		public StructMemberInitializer[] StructMemberInitializers;

		public sealed override string ToString()
		{
			var ret = "{";

			if (StructMemberInitializers != null)
				foreach (var i in StructMemberInitializers)
					ret += i.ToString() + ",";

			return ret.TrimEnd(',') + "}";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public ITypeDeclaration ExpressionTypeRepresentation
		{
			get { return new DExpressionDecl(this); }
		}

		public bool IsConstant
		{
			get { return false; }
		}

		public decimal DecValue
		{
			get { throw new NotImplementedException(); }
		}
	}

	public class StructMemberInitializer
	{
		public string MemberName = string.Empty;
		public IExpression Specialization;

		public sealed override string ToString()
		{
			return (!string.IsNullOrEmpty(MemberName) ? (MemberName + ":") : "") + Specialization.ToString();
		}
	}

	#endregion
}
