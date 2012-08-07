using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver
{
	public class ResolutionError
	{
		public readonly ISyntaxRegion SyntacticalContext;
		public readonly string Message;

		public ResolutionError(ISyntaxRegion syntacticalObj, string message)
		{
			this.SyntacticalContext = syntacticalObj;
			this.Message = message;
		}
	}

	public class AmbiguityError : ResolutionError
	{
		public readonly ISemantic[] DetectedOverloads;

		public AmbiguityError(ISyntaxRegion syntaxObj, IEnumerable<ISemantic> results)
			: base(syntaxObj, "Resolution returned too many results")
		{
			if (results is ISemantic[])
				this.DetectedOverloads = (ISemantic[])results;
			else if(results!=null)
				this.DetectedOverloads = results.ToArray();
		}
	}

	public class NothingFoundError : ResolutionError
	{
		public NothingFoundError(ISyntaxRegion syntaxObj)
			: base(syntaxObj, (syntaxObj is IExpression ? "Expression" : "Declaration") + " could not be resolved.")
		{ }
	}

	public class TemplateParameterDeductionError : ResolutionError
	{
		public ITemplateParameter Parameter { get { return SyntacticalContext as ITemplateParameter; } }
		public readonly ISemantic Argument;

		public TemplateParameterDeductionError(ITemplateParameter parameter, ISemantic argument, string msg)
			: base(parameter, msg)
		{
			this.Argument = argument;
		}
	}

	public class AmbigousSpecializationError : ResolutionError
	{
		public readonly AbstractType[] ComparedOverloads;

		public AmbigousSpecializationError(AbstractType[] comparedOverloads)
			: base(comparedOverloads[comparedOverloads.Length - 1].DeclarationOrExpressionBase, "Could not distinguish a most specialized overload. Both overloads seem to be equal.")
		{
			this.ComparedOverloads = comparedOverloads;
		}
	}
}
