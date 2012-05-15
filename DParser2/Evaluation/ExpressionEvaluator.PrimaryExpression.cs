using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public IExpressionValue Evaluate(PrimaryExpression x)
		{
			if (x is TemplateInstanceExpression)
			{
				//TODO
			}
			else if (x is IdentifierExpression)
			{
				var id = (IdentifierExpression)x;

				if (id.IsIdentifier)
				{
					//TODO
				}

				switch (id.Format)
				{
					case Parser.LiteralFormat.CharLiteral:
						return new PrimitiveValue(PrimitiveType.Char, id.Value, x);
					case Parser.LiteralFormat.FloatingPoint:
						return new PrimitiveValue(PrimitiveType.Float, id.Value, x);
					case Parser.LiteralFormat.Scalar:
						return new PrimitiveValue(PrimitiveType.Int, id.Value, x);
					case Parser.LiteralFormat.StringLiteral:
					case Parser.LiteralFormat.VerbatimStringLiteral:
						return new PrimitiveValue(PrimitiveType.String, id.Value, x);
				}
			}
			else if (x is TokenExpression)
			{
				var tkx = (TokenExpression)x;

				switch (tkx.Token)
				{
					case DTokens.This:
						//TODO
						break;
					case DTokens.Super:
						break;
					case DTokens.Null:
						return new PrimitiveValue(PrimitiveType.Reference, null, x);
					case DTokens.Dollar:
						//TODO
						break;
					case DTokens.True:
						return new PrimitiveValue(PrimitiveType.Bool, true, x);
					case DTokens.False:
						return new PrimitiveValue(PrimitiveType.Bool, false, x);
					case DTokens.__FILE__:
						return new PrimitiveValue(PrimitiveType.String, 
							ctxt==null?"":((IAbstractSyntaxTree)ctxt.ScopedBlock.NodeRoot).FileName,x);
					case DTokens.__LINE__:
						return new PrimitiveValue(PrimitiveType.Int, x.Location.Line, x);
				}
			}
			else if (x is TypeDeclarationExpression)
			{

			}
			else if (x is ArrayLiteralExpression)
			{

			}
			else if (x is AssocArrayExpression)
			{

			}
			else if (x is FunctionLiteral)
			{

			}
			else if (x is AssertExpression)
			{

			}
			else if (x is MixinExpression)
			{

			}
			else if (x is ImportExpression)
			{

			}
			else if (x is TypeidExpression)
			{

			}
			else if (x is IsExpression)
			{

			}
			else if (x is TraitsExpression)
			{

			}
			else if (x is SurroundingParenthesesExpression)
				return Evaluate(((SurroundingParenthesesExpression)x).Expression);

			return null;
		}
	}
}
