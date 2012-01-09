using System.Collections.Generic;
using D_Parser.Parser;

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

        public new string ToString()
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
}