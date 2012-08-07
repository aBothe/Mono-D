using System.Linq;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateAliasParameter p, ISemantic arg)
		{
			#region Handle parameter defaults
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = Evaluation.EvaluateValue(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p, eval);
				}
				else if (p.DefaultType != null)
				{
					var res = TypeDeclarationResolver.Resolve(p.DefaultType, ctxt);

					if (res == null)
						return false;

					bool ret = false;
					foreach(var r in res)
						if (!Set(p, r))
						{
							ret = true;
						}

					if (ret)
						return false;
				}
				return false;
			}
			#endregion

			#region Given argument must be a symbol - so no built-in type but a reference to a node or an expression
			var t=TypeDeclarationResolver.Convert(arg);

			if (t == null)
				return false;

			while (t != null)
			{
				if (t is PrimitiveType) // arg must not base on a primitive type.
					return false;

				if (t is DerivedDataType)
					t = ((DerivedDataType)t).Base;
				else
					break;
			}
			#endregion

			#region Specialization check
			if (p.SpecializationExpression != null)
			{
				// LANGUAGE ISSUE: Can't do anything here - dmd won't let you use MyClass!(2) though you have class MyClass(alias X:2)
				return false;
			}
			else if (p.SpecializationType != null)
			{
				// ditto
				return false;
			}
			#endregion

			return Set(p,arg);
		}
	}
}
