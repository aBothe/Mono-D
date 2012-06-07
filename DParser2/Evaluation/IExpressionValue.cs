using D_Parser.Dom.Expressions;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public interface IExpressionValue
	{
		PrimitiveType Type { get; }
		ITypeDeclaration RepresentedType { get; }
		object Value { get; }
		IExpression BaseExpression { get; }
	}
}
