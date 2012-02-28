using System.Collections.Generic;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;
using System;

namespace D_Parser.Dom
{
    /// <summary>
    /// Represents an attrribute a declaration may have or consists of
    /// </summary>
    public class DAttribute
    {
        public int Token;
        public object LiteralContent;
        public static readonly DAttribute Empty = new DAttribute(-1);

        public DAttribute(int Token)
        {
            this.Token = Token;
            LiteralContent = null;
        }

        public DAttribute(int Token, object Content)
        {
            this.Token = Token;
            this.LiteralContent = Content;
        }

        public override string ToString()
        {
			if (Token == DTokens.PropertyAttribute)
				return "@" + (LiteralContent==null?"": LiteralContent.ToString());
            if (LiteralContent != null)
                return DTokens.GetTokenString(Token) + "(" + LiteralContent.ToString() + ")";
			return DTokens.GetTokenString(Token);
        }

		/// <summary>
		/// Removes all public,private,protected or package attributes from the list
		/// </summary>
		public static void CleanupAccessorAttributes(List<DAttribute> HayStack)
		{
			foreach (var i in HayStack.ToArray())
			{
				if (DTokens.VisModifiers[i.Token])
					HayStack.Remove(i);
			}
		}

		/// <summary>
		/// Removes all public,private,protected or package attributes from the stack
		/// </summary>
		public static void CleanupAccessorAttributes(Stack<DAttribute> HayStack)
		{
			var l=new List<DAttribute>();

			while(HayStack.Count>0)
			{
				var attr=HayStack.Pop();
				if (!DTokens.VisModifiers[attr.Token])
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static void RemoveFromStack(Stack<DAttribute> HayStack, int Token)
		{
			var l = new List<DAttribute>();

			while (HayStack.Count > 0)
			{
				var attr = HayStack.Pop();
				if (attr.Token!=Token)
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static bool ContainsAccessorAttribute(Stack<DAttribute> HayStack)
		{
			foreach (var i in HayStack)
				if (DTokens.VisModifiers[i.Token])
					return true;
			return false;
		}

        public static bool ContainsAttribute(DAttribute[] HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
			if(HayStack!=null)
				foreach (var attr in HayStack)
					if (l.Contains(attr.Token))
						return true;
            return false;
        }
        public static bool ContainsAttribute(List<DAttribute> HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
            foreach (var attr in HayStack)
                if (l.Contains(attr.Token))
                    return true;
            return false;
        }

        public static bool ContainsAttribute(Stack<DAttribute> HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
            foreach (var attr in HayStack)
                if (l.Contains(attr.Token))
                    return true;
            return false;
        }


        public bool IsStorageClass
        {
            get
            {
                return DTokens.StorageClass[Token];
            }
        }

		public bool IsProperty
		{
			get { return Token == DTokens.PropertyAttribute; }
		}
    }

	public class DeclarationCondition : DAttribute, ICloneable
	{
		public bool IsStaticIfCondition
		{
			get { return Token == DTokens.If; }
		}
		public bool IsDebugCondition
		{
			get { return Token == DTokens.Debug; }
		}
		public bool IsVersionCondition
		{
			get { return Token == DTokens.Version; }
		}

		/// <summary>
		/// Alias for LiteralContent
		/// </summary>
		public IExpression Condition
		{
			get { return LiteralContent as IExpression; }
			set { LiteralContent = value; } 
		}

		public bool IsNegated { get; protected set; }

		public void Negate()
		{
			if(IsNegated)
				return;
			IsNegated=true;

			if(!(Condition is SurroundingParenthesesExpression) && IsStaticIfCondition)
				Condition = new SurroundingParenthesesExpression { 
					Expression=Condition, 
					Location= Condition==null? CodeLocation.Empty : Condition.Location,
					EndLocation= Condition==null? CodeLocation.Empty : Condition.EndLocation
				};

			Condition = new UnaryExpression_Not { 
				UnaryExpression=Condition, 
				Location= Condition.Location
			};
		}

		public DeclarationCondition(int Token)
			: base(Token)
		{
		}

		public object Clone()
		{
			return new DeclarationCondition(Token)
			{
				Condition = Condition,
				IsNegated=IsNegated
			};
		}
	}

	public class PragmaAttribute : DAttribute
	{
		/// <summary>
		/// Alias for LiteralContent.
		/// </summary>
		public string Identifier
		{
			get { return LiteralContent as string; }
			set { LiteralContent = value; }
		}

		public IExpression[] Arguments;

		public PragmaAttribute() : base(DTokens.Pragma) { }

		public override string ToString()
		{
			var r= "pragma(" + Identifier;

			if(Arguments!=null && Arguments.Length>0)
				foreach (var e in Arguments)
					r += "," + e!=null ? e.ToString() : "";

			return r + ")";
		}
	}
}