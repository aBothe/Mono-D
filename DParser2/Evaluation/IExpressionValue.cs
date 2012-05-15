using D_Parser.Dom.Expressions;

namespace D_Parser.Evaluation
{
	public interface IExpressionValue
	{
		PrimitiveType Type { get; }
		object Value { get; }
		IExpression BaseExpression { get; }
	}
}
