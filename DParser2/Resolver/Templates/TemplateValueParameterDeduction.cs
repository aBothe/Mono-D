using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateValueParameter p, ISemantic arg)
		{
			// Handle default arg case
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = Evaluation.EvaluateValue(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p, eval);
				}
				else
					return false;
			}

			var valueArgument = arg as ISymbolValue;

			// There must be a constant expression given!
			if (valueArgument == null)
				return false;

			// Check for param type <-> arg expression type match
			var paramType = TypeDeclarationResolver.Resolve(p.Type, ctxt);

			if (paramType == null || paramType.Length == 0)
				return false;

			if (valueArgument.RepresentedType == null ||
				!ResultComparer.IsImplicitlyConvertible(paramType[0], valueArgument.RepresentedType))
				return false;

			// If spec given, test for equality (only ?)
			if (p.SpecializationExpression != null) 
			{
				var specVal = Evaluation.EvaluateValue(p.SpecializationExpression, ctxt);

				if (specVal == null || !SymbolValueComparer.IsEqual(specVal, valueArgument))
					return false;
			}

			return Set(p, arg);
		}
	}
}
