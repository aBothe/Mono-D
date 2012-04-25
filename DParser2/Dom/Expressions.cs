using System;
using System.Collections.Generic;
using D_Parser.Parser;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom.Expressions
{
	public delegate INode[] ResolveTypeHandler(string identifier);

	public interface IExpression : ISyntaxRegion
	{
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
						/*
						 * a.b -- take the entire access expression instead of b only in order to be able to resolve it correctly
						 */
						var pfa = e as PostfixExpression_Access;
						if (pfa != null && pfa.AccessExpression == se && !(pfa.AccessExpression is ContainerExpression))
							continue;

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
			get { return Expressions.Count>0 ? Expressions[0].Location : CodeLocation.Empty; }
		}

		public CodeLocation EndLocation
		{
			get { return Expressions.Count>0 ? Expressions[Expressions.Count - 1].EndLocation : CodeLocation.Empty; }
		}

		public IExpression[] SubExpressions
		{
			get { return Expressions.ToArray(); }
		}
	}

	public class AssignExpression : OperatorBasedExpression
	{
		public AssignExpression(int opToken) { OperatorToken = opToken; }
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

		public IExpression[] SubExpressions
		{
			get { return new[] {OrOrExpression, TrueCaseExpression, FalseCaseExpression}; }
		}
	}

	public class OrOrExpression : OperatorBasedExpression
	{
		public OrOrExpression() { OperatorToken = DTokens.LogicalOr; }
	}

	public class AndAndExpression : OperatorBasedExpression
	{
		public AndAndExpression() { OperatorToken = DTokens.LogicalAnd; }
	}

	public class XorExpression : OperatorBasedExpression
	{
		public XorExpression() { OperatorToken = DTokens.Xor; }
	}

	public class OrExpression : OperatorBasedExpression
	{
		public OrExpression() { OperatorToken = DTokens.BitwiseOr; }
	}

	public class AndExpression : OperatorBasedExpression
	{
		public AndExpression() { OperatorToken = DTokens.BitwiseAnd; }
	}

	public class EqualExpression : OperatorBasedExpression
	{
		public EqualExpression(bool isUnEqual) { OperatorToken = isUnEqual ? DTokens.NotEqual : DTokens.Equal; }
	}

	public class IdendityExpression : OperatorBasedExpression
	{
		public bool Not;

		public IdendityExpression(bool notIs) { Not = notIs; OperatorToken = DTokens.Is; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "is " + RightOperand.ToString();
		}
	}

	public class RelExpression : OperatorBasedExpression
	{
		public RelExpression(int relationalOperator) { OperatorToken = relationalOperator; }
	}

	public class InExpression : OperatorBasedExpression
	{
		public bool Not;

		public InExpression(bool notIn) { Not = notIn; OperatorToken = DTokens.In; }

		public override string ToString()
		{
			return LeftOperand.ToString() + (Not ? " !" : " ") + "in " + RightOperand.ToString();
		}
	}

	public class ShiftExpression : OperatorBasedExpression
	{
		public ShiftExpression(int shiftOperator) { OperatorToken = shiftOperator; }
	}

	public class AddExpression : OperatorBasedExpression
	{
		public AddExpression(bool isMinus) { OperatorToken = isMinus ? DTokens.Minus : DTokens.Plus; }
	}

	public class MulExpression : OperatorBasedExpression
	{
		public MulExpression(int mulOperator) { OperatorToken = mulOperator; }
	}

	public class CatExpression : OperatorBasedExpression
	{
		public CatExpression() { OperatorToken = DTokens.Tilde; }
	}

	public interface UnaryExpression : IExpression { }

	public class PowExpression : OperatorBasedExpression, UnaryExpression
	{
		public PowExpression() { OperatorToken = DTokens.Pow; }
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
	}

	public class UnaryExpression_Increment : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Increment; }
		}
	}

	public class UnaryExpression_Decrement : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Decrement; }
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
	}

	public class UnaryExpression_Add : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Plus; }
		}
	}

	public class UnaryExpression_Sub : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Minus; }
		}
	}

	public class UnaryExpression_Not : SimpleUnaryExpression
	{
		public override int ForeToken
		{
			get { return DTokens.Not; }
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

			if (!(Type is ArrayDecl))
			{
				ret += '(';
				if (Arguments != null)
					foreach (var e in Arguments)
						ret += e.ToString() + ",";

				ret = ret.TrimEnd(',') + ')';
			}

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
		public int[] CastParamTokens { get; set; }

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
        /// <summary>
        /// Can be either
        /// 1) An Identifier
        /// 2) A Template Instance
        /// 3) A NewExpression
        /// </summary>
        public IExpression AccessExpression;

		public override string ToString()
		{
			var r = PostfixForeExpression.ToString() + '.';

            if (AccessExpression != null)
                r += AccessExpression.ToString();

			return r;
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
				return new[]{PostfixForeExpression, AccessExpression};
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
					if(a!=null)
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
			var ret = PostfixForeExpression!=null ? PostfixForeExpression.ToString():"";
				
			ret += "[";

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

	#region Primary Expressions
	public interface PrimaryExpression : IExpression { }

	public class TemplateInstanceExpression : AbstractTypeDeclaration,PrimaryExpression,ContainerExpression
	{
		public IdentifierDeclaration TemplateIdentifier;
		public IExpression[] Arguments;

		public override string ToString(bool IncludesBase)
		{
			var ret = IncludesBase && InnerDeclaration != null ? (InnerDeclaration.ToString() + ".") : "";
			
			if(TemplateIdentifier!=null)
				ret+=TemplateIdentifier.ToString();

			ret += "!";

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
						return "'"+Value??""+"'";
					case Parser.LiteralFormat.StringLiteral:
						return "\"" + Value??"" + "\"";
					case Parser.LiteralFormat.VerbatimStringLiteral:
						return "r\"" + Value??"" + "\"";
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
	}

	public class TokenExpression : PrimaryExpression
	{
		public int Token=DTokens.INVALID;

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
	}

	/// <summary>
	/// auto arr= [1,2,3,4,5,6];
	/// </summary>
	public class ArrayLiteralExpression : PrimaryExpression,ContainerExpression
	{
		public readonly List<IExpression> Elements = new List<IExpression>();

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in Elements)
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

		public IExpression[] SubExpressions
		{
			get { return Elements!=null && Elements.Count>0? Elements.ToArray() : null; }
		}
	}

	/// <summary>
	/// auto arr=['a':0xa, 'b':0xb, 'c':0xc, 'd':0xd, 'e':0xe, 'f':0xf];
	/// </summary>
	public class AssocArrayExpression : PrimaryExpression,ContainerExpression
	{
		public IList<KeyValuePair<IExpression, IExpression>> Elements = new List<KeyValuePair<IExpression, IExpression>>();

		public override string ToString()
		{
			var s = "[";
			foreach (var expr in Elements)
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

		public IExpression[] SubExpressions
		{
			get {
				var l = new List<IExpression>();

				foreach (var kv in Elements)
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

		public IExpression[] SubExpressions
		{
			get { return new[]{Expression}; }
		}
	}
	#endregion

	#region Initializers

	public interface IVariableInitializer { }

	public abstract class AbstractVariableInitializer : IVariableInitializer,IExpression
	{
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

	public class VoidInitializer : AbstractVariableInitializer
	{
		public VoidInitializer() { }
	}

	public class ArrayInitializer : AssocArrayExpression,IVariableInitializer { }

	public class StructInitializer : AbstractVariableInitializer, ContainerExpression
	{
		public StructMemberInitializer[] MemberInitializers;

		public sealed override string ToString()
		{
			var ret = "{";

			if (MemberInitializers != null)
				foreach (var i in MemberInitializers)
					ret += i.ToString() + ",";

			return ret.TrimEnd(',') + "}";
		}

		public IExpression[] SubExpressions
		{
			get {
				if (MemberInitializers == null)
					return null;

				var l = new List<IExpression>(MemberInitializers.Length);

				foreach (var mi in MemberInitializers)
					if(mi.Value!=null)
						l.Add(mi.Value);

				return l.ToArray();
			}
		}
	}

	public class StructMemberInitializer
	{
		public string MemberName = string.Empty;
		public IExpression Value;

		public sealed override string ToString()
		{
			return (!string.IsNullOrEmpty(MemberName) ? (MemberName + ":") : "") + Value.ToString();
		}
	}
	#endregion
}
