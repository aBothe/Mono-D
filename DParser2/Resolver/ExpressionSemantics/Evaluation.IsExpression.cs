using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		/// <summary>
		/// http://dlang.org/expression.html#IsExpression
		/// </summary>
		public ISemantic E(IsExpression isExpression)
		{
			if(!eval)
				return new PrimitiveType(DTokens.Bool);

			bool retTrue = false;

			if (isExpression.TestedType != null)
			{
				var typeToCheck = DResolver.StripAliasSymbol(TypeDeclarationResolver.ResolveSingle(isExpression.TestedType, ctxt));

				if (typeToCheck != null)
				{
					// case 1, 4
					if (isExpression.TypeSpecialization == null && isExpression.TypeSpecializationToken == 0)
						retTrue = typeToCheck != null;

					// The probably most frequented usage of this expression
					else if (string.IsNullOrEmpty(isExpression.TypeAliasIdentifier))
						retTrue = evalIsExpression_NoAlias(isExpression, typeToCheck);
					else
						retTrue = evalIsExpression_WithAliases(isExpression, typeToCheck);
				}
			}

			return new PrimitiveValue(DTokens.Bool, retTrue?1:0, isExpression);
		}

		private bool evalIsExpression_WithAliases(IsExpression isExpression, AbstractType typeToCheck)
		{
			/*
			 * Note: It's needed to let the abstract ast scanner also scan through IsExpressions etc.
			 * in order to find aliases and/or specified template parameters!
			 */

			var tpl_params = new DeducedTypeDictionary();
			tpl_params[isExpression.TypeAliasIdentifier] = null;
			if (isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					tpl_params[p.Name] = null;

			var tpd = new TemplateParameterDeduction(tpl_params, ctxt);
			bool retTrue = false;

			if (isExpression.EqualityTest) // 6.
			{
				// a)
				if (isExpression.TypeSpecialization != null)
				{
					tpd.EnforceTypeEqualityWhenDeducing = true;
					retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);
					tpd.EnforceTypeEqualityWhenDeducing = false;
				}
				else // b)
				{
					var r = evalIsExpression_EvalSpecToken(isExpression, typeToCheck, true);
					retTrue = r.Item1;
					tpl_params[isExpression.TypeAliasIdentifier] = new TemplateParameterSymbol((TemplateParameterNode)null, r.Item2);
				}
			}
			else // 5.
				retTrue = tpd.Handle(isExpression.ArtificialFirstSpecParam, typeToCheck);

			if (retTrue && isExpression.TemplateParameterList != null)
				foreach (var p in isExpression.TemplateParameterList)
					if (!tpd.Handle(p, tpl_params[p.Name] != null ? tpl_params[p.Name].Base : null))
						return false;

			//TODO: Put all tpl_params results into the resolver context or make a new scope or something! 

			return retTrue;
		}

		private bool evalIsExpression_NoAlias(IsExpression isExpression, AbstractType typeToCheck)
		{
			if (isExpression.TypeSpecialization != null)
			{
				var spec = DResolver.StripAliasSymbols(TypeDeclarationResolver.Resolve(isExpression.TypeSpecialization, ctxt));

				return spec != null && spec.Length != 0 && (isExpression.EqualityTest ?
					ResultComparer.IsEqual(typeToCheck, spec[0]) :
					ResultComparer.IsImplicitlyConvertible(typeToCheck, spec[0], ctxt));
			}

			return isExpression.EqualityTest && evalIsExpression_EvalSpecToken(isExpression, typeToCheck, false).Item1;
		}

		/// <summary>
		/// Item1 - True, if isExpression returns true
		/// Item2 - If Item1 is true, it contains the type of the alias that is defined in the isExpression 
		/// </summary>
		private Tuple<bool, AbstractType> evalIsExpression_EvalSpecToken(IsExpression isExpression, AbstractType typeToCheck, bool DoAliasHandling = false)
		{
			bool r = false;
			AbstractType res = null;

			switch (isExpression.TypeSpecializationToken)
			{
				/*
				 * To handle semantic tokens like "return" or "super" it's just needed to 
				 * look into the current resolver context -
				 * then, we'll be able to gather either the parent method or the currently scoped class definition.
				 */
				case DTokens.Struct:
				case DTokens.Union:
				case DTokens.Class:
				case DTokens.Interface:
					if (r = typeToCheck is UserDefinedType &&
						((TemplateIntermediateType)typeToCheck).Definition.ClassType == isExpression.TypeSpecializationToken)
						res = typeToCheck;
					break;

				case DTokens.Enum:
					if (!(typeToCheck is EnumType))
						break;
					{
						var tr = (UserDefinedType)typeToCheck;
						r = true;
						res = tr.Base;
					}
					break;

				case DTokens.Function:
				case DTokens.Delegate:
					if (typeToCheck is DelegateType)
					{
						var isFun = false;
						var dgr = (DelegateType)typeToCheck;
						if (!dgr.IsFunctionLiteral)
							r = isExpression.TypeSpecializationToken == (
								(isFun = ((DelegateDeclaration)dgr.DeclarationOrExpressionBase).IsFunction) ? DTokens.Function : DTokens.Delegate);
						// Must be a delegate otherwise
						else
							isFun = !(r = isExpression.TypeSpecializationToken == DTokens.Delegate);

						if (r)
						{
							//TODO
							if (isFun)
							{
								// TypeTuple of the function parameter types. For C- and D-style variadic functions, only the non-variadic parameters are included. 
								// For typesafe variadic functions, the ... is ignored.
							}
							else
							{
								// the function type of the delegate
							}
						}
					}
					else // Normal functions are also accepted as delegates
					{
						r = isExpression.TypeSpecializationToken == DTokens.Delegate &&
							typeToCheck is MemberSymbol &&
							((DSymbol)typeToCheck).Definition is DMethod;

						//TODO: Alias handling, same as couple of lines above
					}
					break;

				case DTokens.Super: //TODO: Test this
					var dc = DResolver.SearchClassLikeAt(ctxt.ScopedBlock, isExpression.Location) as DClassLike;

					if (dc != null)
					{
						var udt = DResolver.ResolveBaseClasses(new ClassType(dc, dc, null), ctxt, true) as ClassType;

						if (r = udt.Base != null && ResultComparer.IsEqual(typeToCheck, udt.Base))
						{
							var l = new List<AbstractType>();
							if (udt.Base != null)
								l.Add(udt.Base);
							if (udt.BaseInterfaces != null && udt.BaseInterfaces.Length != 0)
								l.AddRange(udt.BaseInterfaces);

							res = new TypeTuple(isExpression, l);
						}
					}
					break;

				case DTokens.Const:
				case DTokens.Immutable:
				case DTokens.InOut: // TODO?
				case DTokens.Shared:
					if (r = typeToCheck.Modifier == isExpression.TypeSpecializationToken)
						res = typeToCheck;
					break;

				case DTokens.Return: // TODO: Test
					IStatement _u = null;
					var dm = DResolver.SearchBlockAt(ctxt.ScopedBlock, isExpression.Location, out _u) as DMethod;

					if (dm != null)
					{
						var retType_ = TypeDeclarationResolver.GetMethodReturnType(dm, ctxt);

						if (r = retType_ != null && ResultComparer.IsEqual(typeToCheck, retType_))
							res = retType_;
					}
					break;
			}

			return new Tuple<bool, AbstractType>(r, res);
		}
	}
}
