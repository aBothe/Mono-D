using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Debugging
{
	class DebugSymbolValueProvider : StandardValueProvider
	{
		public readonly DLocalExamBacktrace Backtrace;

		public void ResetCache()
		{

		}

		public DebugSymbolValueProvider(DLocalExamBacktrace b, ResolutionContext ctxt)
			: base(ctxt)
		{
			this.Backtrace = b;
		}

		public override DVariable GetLocal(string LocalName, IdentifierExpression id = null)
		{
			return base.GetLocal(LocalName, id);
		}

		public override bool ConstantOnly
		{
			get { return false; }
			set { }
		}

		public override ISymbolValue this[DVariable n]
		{
			get
			{
				return base[n];
			}
			set
			{
				base[n] = value;
			}
		}
	}
}
