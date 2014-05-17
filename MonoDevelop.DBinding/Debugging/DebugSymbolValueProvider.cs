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
		Dictionary<IDBacktraceSymbol,DebugVariable> cache = new Dictionary<IDBacktraceSymbol,DebugVariable>();

		public class DebugVariable : DVariable
		{
			public IDBacktraceSymbol Symbol;
		}

		public void ResetCache()
		{
			cache.Clear();
		}

		public DebugSymbolValueProvider(DLocalExamBacktrace b, ResolutionContext ctxt)
			: base(ctxt)
		{
			this.Backtrace = b;
		}

		public override DVariable GetLocal(string LocalName, IdentifierExpression id = null)
		{
			Backtrace.TryUpdateStackFrameInfo();
			var symb = Backtrace.BacktraceHelper.FindSymbol(LocalName);

			if (symb != null)
			{
				DebugVariable dv;
				if(!cache.TryGetValue(symb, out dv))
					cache[symb] = dv = new DebugVariable { Symbol = symb, Name = LocalName };
				return dv;
			}

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
				var dv = n as DebugVariable;
				if (dv != null)
					return Backtrace.EvaluateSymbol(dv.Symbol);

				return base[n];
			}
			set
			{
				//TODO?
				base[n] = value;
			}
		}
	}
}
