using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Debugging
{
	/// <summary>
	/// Used when child items of a value shall not be evaluated yet because their values might be too complex to evaluate 
	/// and thus must explicitly be acquired by the user.
	/// </summary>
	public class LazyEvaluationValue : ISymbolValue
	{

		public AbstractType RepresentedType
		{
			get;
			set;
		}

		public R Accept<R>(ISymbolValueVisitor<R> v)
		{
			throw new NotImplementedException();
		}

		public bool Equals(ISymbolValue other)
		{
			throw new NotImplementedException();
		}

		public string ToCode()
		{
			throw new NotImplementedException();
		}

		public void Accept(ISymbolValueVisitor vis)
		{
			throw new NotImplementedException();
		}
	}
}
