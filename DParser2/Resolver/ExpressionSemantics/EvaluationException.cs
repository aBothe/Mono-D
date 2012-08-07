using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Resolver
{
	public class DParserException : Exception {
		public DParserException() { }
		public DParserException(string Msg) : base(Msg) { }
	}

	public class ResolutionException : DParserException
	{
		public ISyntaxRegion ObjectToResolve { get; protected set; }
		public ISemantic[] LastSubResults { get; protected set; }

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, IEnumerable<ISemantic> LastSubresults)
			: base(Message)
		{
			this.ObjectToResolve=ObjToResolve;
			this.LastSubResults = LastSubresults.ToArray();
		}

		public ResolutionException(ISyntaxRegion ObjToResolve, string Message, params ISemantic[] LastSubresult)
			: base(Message)
		{
			this.ObjectToResolve=ObjToResolve;
			this.LastSubResults = LastSubresult;
		}
	}

	public class EvaluationException : ResolutionException
	{
		public IExpression EvaluatedExpression
		{
			get { return ObjectToResolve as IExpression; }
		}

		public EvaluationException(IExpression EvaluatedExpression, string Message, IEnumerable<ISemantic> LastSubresults)
			: base(EvaluatedExpression, Message, LastSubresults) { }

		public EvaluationException(IExpression EvaluatedExpression, string Message, params ISemantic[] LastSubresults)
			: base(EvaluatedExpression, Message, LastSubresults)
		{ }
	}

	public class NoConstException : EvaluationException
	{
		public NoConstException(IExpression x) : base(x, "Expression must resolve to constant value") { }
	}

	public class InvalidStringException : EvaluationException
	{
		public InvalidStringException(IExpression x) : base(x, "Expression must be a valid string") { }
	}

	public class AssertException : EvaluationException
	{
		public AssertException(AssertExpression ae, string optAssertMessage="") : base(ae, "Assert returned false. "+optAssertMessage) { }
	}

	public class WrongEvaluationArgException : Exception
	{
		public WrongEvaluationArgException() : base("Wrong argument type for expression evaluation given") {}
	}
}
