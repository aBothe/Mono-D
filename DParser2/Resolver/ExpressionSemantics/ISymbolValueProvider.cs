using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public abstract class AbstractSymbolValueProvider
	{
		public ResolverContextStack ResolutionContext { get; protected set; }

		public ISymbolValue this[IdentifierExpression id]
		{
			get
			{
				return this[GetLocal(id)];
			}
			set
			{
				this[GetLocal(id)] = value;
			}
		}

		public ISymbolValue this[string LocalName]
		{
			get
			{
				return this[GetLocal(LocalName)];
			}
			set
			{
				this[GetLocal(LocalName)] = value;
			}
		}

		public abstract ISymbolValue this[DVariable variable] { get; set; }

		public DVariable GetLocal(IdentifierExpression id)
		{
			return GetLocal(id.Value as string, id);
		}

		/// <summary>
		/// Searches a local/parameter variable and returns the node
		/// </summary>
		public abstract DVariable GetLocal(string LocalName, IdentifierExpression id=null);

		public abstract bool ConstantOnly { get; set; }
		public void LogError(ISyntaxRegion involvedSyntaxObject, string msg, bool isWarning = false)
		{
			//TODO: Handle semantic errors that occur during analysis
		}

		/*
		 * TODO:
		 * -- Execution stack and model
		 * -- Virtual memory allocation
		 *		(e.g. class instance will contain a dictionary with class properties etc.)
		 *		-- when executing a class' member method, the instance will be passed as 'this' reference etc.
		 */

		/// <summary>
		/// Used for $ operands inside index/slice expressions.
		/// </summary>
		public int CurrentArrayLength;
	}

	/// <summary>
	/// This provider is used for constant values evaluation.
	/// 'Locals' aren't provided whereas requesting a variable's constant
	/// </summary>
	public class StandardValueProvider : AbstractSymbolValueProvider
	{
		public StandardValueProvider(ResolverContextStack ctxt)
		{
			ResolutionContext = ctxt;
		}

		public override bool ConstantOnly
		{
			get { return true; }
			set { }
		}

		public override ISymbolValue this[DVariable n]
		{
			get
			{
				if (n == null)
					throw new ArgumentNullException("There must be a valid variable node given in order to retrieve its value");

				if (n.IsConst)
				{
					// .. resolve it's pre-compile time value and make the returned value the given argument
					var val = Evaluation.EvaluateValue(n.Initializer, this);

					// If it's null, then the initializer is null - which is equal to e.g. 0 or null !;

					if (val != null)
						return val;
				}

				throw new ArgumentException(n+" must have a constant initializer");
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public override DVariable GetLocal(string LocalName, IdentifierExpression id=null)
		{
			var res = TypeDeclarationResolver.ResolveIdentifier(LocalName, ResolutionContext, id);

			if (res == null || res.Length == 0)
				return null;

			var r = res[0];

			if (r is MemberSymbol)
			{
				var mr = (MemberSymbol)r;

				if (mr.Definition is DVariable)
					return(DVariable)mr.Definition;
			}

			throw new EvaluationException(id ?? new IdentifierExpression(LocalName), LocalName + " must represent a local variable or a parameter", res);
		}
	}
}
