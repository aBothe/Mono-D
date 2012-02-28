using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
	public interface ITypeDeclaration
	{
		CodeLocation Location { get; set; }
		CodeLocation EndLocation { get; set; }

		ITypeDeclaration InnerDeclaration { get; set; }
		ITypeDeclaration InnerMost { get; set; }

		/// <summary>
		/// Used e.g. if it's known that a type declaration expresses a variable's name
		/// </summary>
		bool ExpressesVariableAccess { get; set; }

		string ToString();
		string ToString(bool IncludesBase);
	}

	public abstract class AbstractTypeDeclaration : ITypeDeclaration
	{
		public ITypeDeclaration InnerMost
		{
			get
			{
				if (InnerDeclaration == null)
					return this;
				else
					return InnerDeclaration.InnerMost;
			}
			set
			{
				if (InnerDeclaration == null)
					InnerDeclaration = value;
				else
					InnerDeclaration.InnerMost = value;
			}
		}

		public ITypeDeclaration InnerDeclaration
		{
			get;
			set;
		}

		public override string ToString()
		{
			return ToString(true);
		}

		public abstract string ToString(bool IncludesBase);

		public static implicit operator String(AbstractTypeDeclaration d)
		{
			return d.ToString(false);
		}

		CodeLocation _loc=CodeLocation.Empty;

		/// <summary>
		/// The type declaration's start location.
		/// If inner declaration given, its start location will be returned.
		/// </summary>
		public CodeLocation Location
		{
			get 
			{
				if (_loc != CodeLocation.Empty || InnerDeclaration==null)
					return _loc;

				return InnerMost.Location;
			}
			set { _loc = value; }
		}

		/// <summary>
		/// The actual start location without regarding inner declarations.
		/// </summary>
		public CodeLocation NonInnerTypeDependendLocation
		{
			get { return _loc; }
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}


		public bool ExpressesVariableAccess
		{
			get;
			set;
		}
	}

    /// <summary>
    /// Identifier, e.g. "foo"
    /// </summary>
    public class IdentifierDeclaration : AbstractTypeDeclaration
    {
		public string Id;

        public IdentifierDeclaration() { }
        public IdentifierDeclaration(string Value)
        { this.Id = Value; }

		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString() + ".") : "") +Convert.ToString(Id);
		}
	}

	/// <summary>
	/// int, void, float
	/// </summary>
    public class DTokenDeclaration : AbstractTypeDeclaration
    {
        public int Token;

        public DTokenDeclaration() { }
        public DTokenDeclaration(int Token)
        { this.Token = Token; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p">The token</param>
        /// <param name="td">Its base token</param>
        public DTokenDeclaration(int p, ITypeDeclaration td)
        {
            Token = p;
            InnerDeclaration = td;
        }

		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase && InnerDeclaration!=null?(InnerDeclaration.ToString() + '.'):"") + DTokens.GetTokenString(Token);
		}
	}

    /// <summary>
    /// Extends an identifier by an array literal.
    /// </summary>
    public class ArrayDecl : AbstractTypeDeclaration
    {
		/// <summary>
		/// Used for associative arrays; Contains all declaration parts that are located inside the square brackets.
		/// Integer by default.
		/// </summary>
        public ITypeDeclaration KeyType=new DTokenDeclaration(DTokens.Int);

		public bool ClampsEmpty = true;

		public IExpression KeyExpression;

		public bool IsRanged
		{
			get {
				return KeyExpression is PostfixExpression_Slice;
			}
		}
		public bool IsAssociative
		{
			get
			{
				return KeyType!=null && (!(KeyType is DTokenDeclaration) ||
					!DTokens.BasicTypes_Integral[(KeyType as DTokenDeclaration).Token]);
			}
		}

		/// <summary>
		/// Alias for InnerDeclaration; contains all declaration parts that are located in front of the square brackets.
		/// </summary>
		public ITypeDeclaration ValueType
		{
			get { return InnerDeclaration; }
			set { InnerDeclaration = value; }
		}

		public override string ToString(bool IncludesBase)
        {
			var ret = "";

			if (IncludesBase && ValueType != null)
				ret = ValueType.ToString();

			ret += "[";
			
			if(!ClampsEmpty)
			{
				if (KeyExpression != null)
					ret += KeyExpression.ToString();
				else if (KeyType != null)
					ret += KeyType.ToString();
			}

			return ret + "]";
        }
    }

    public class DelegateDeclaration : AbstractTypeDeclaration
    {
		/// <summary>
		/// Alias for InnerDeclaration.
		/// Contains 'int' in
		/// int delegate() foo;
		/// </summary>
        public ITypeDeclaration ReturnType
        {
            get { return InnerDeclaration; }
            set { InnerDeclaration = value; }
        }
        /// <summary>
        /// Is it a function(), not a delegate() ?
        /// </summary>
        public bool IsFunction = false;

        public List<INode> Parameters = new List<INode>();
		public DAttribute[] Modifiers;

		public override string ToString(bool IncludesBase)
        {
            string ret = (IncludesBase && ReturnType!=null? ReturnType.ToString():"") + (IsFunction ? " function" : " delegate") + "(";

            foreach (DVariable n in Parameters)
            {
                if (n.Type != null)
                    ret += n.Type.ToString();

                if (!String.IsNullOrEmpty(n.Name))
                    ret += (" " + n.Name);

                if (n.Initializer != null)
                    ret += "= " + n.Initializer.ToString();

                ret += ", ";
            }
            ret = ret.TrimEnd(',', ' ') + ")";
            return ret;
        }
    }

    /// <summary>
    /// int* ptr;
    /// </summary>
    public class PointerDecl : AbstractTypeDeclaration
    {
        public PointerDecl() { }
        public PointerDecl(ITypeDeclaration BaseType) { InnerDeclaration = BaseType; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? InnerDeclaration.ToString() : "") + "*";
        }
    }

    /// <summary>
    /// const(char)
    /// </summary>
    public class MemberFunctionAttributeDecl : AbstractTypeDeclaration
    {
        /// <summary>
        /// Equals <see cref="Token"/>
        /// </summary>
		public int Modifier=DTokens.Const;

        public ITypeDeclaration InnerType;

        public MemberFunctionAttributeDecl() { }
        public MemberFunctionAttributeDecl(int ModifierToken) { this.Modifier = ModifierToken; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString()+" ") : "") + DTokens.GetTokenString(Modifier) + "(" + (InnerType != null ? InnerType.ToString() : "") + ")";
        }
    }
    
    /// <summary>
    /// typeof(...)
    /// </summary>
    public class TypeOfDeclaration : AbstractTypeDeclaration
    {
    	public IExpression InstanceId;
    	
		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString()+" ") : "") + "typeof(" + (InstanceId != null ? InstanceId.ToString() : "") + ")";
		}
    }

	/// <summary>
	/// __vector(...)
	/// </summary>
	public class VectorDeclaration : AbstractTypeDeclaration
	{
		public IExpression Id;

		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase && InnerDeclaration != null ? (InnerDeclaration.ToString() + " ") : "") + "__vector(" + (Id != null ? Id.ToString() : "") + ")";
		}
	}

	/// <summary>
	/// template myTemplate(T...)
	/// </summary>
    public class VarArgDecl : AbstractTypeDeclaration
    {
        public VarArgDecl() { }
        public VarArgDecl(ITypeDeclaration BaseIdentifier) { InnerDeclaration = BaseIdentifier; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? InnerDeclaration.ToString() : "") + "...";
        }
    }
}