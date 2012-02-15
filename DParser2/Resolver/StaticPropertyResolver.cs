using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;

namespace D_Parser.Resolver
{
	public class StaticPropertyResolver
	{
		/// <summary>
		/// Tries to resolve a static property's name.
		/// Returns a result describing the theoretical member (".init"-%gt;MemberResult; ".typeof"-&gt;TypeResult etc).
		/// Returns null if nothing was found.
		/// </summary>
		/// <param name="InitialResult"></param>
		/// <returns></returns>
		public static ResolveResult TryResolveStaticProperties(ResolveResult InitialResult, IdentifierDeclaration Identifier, ResolverContext ctxt = null)
		{
			if (InitialResult == null || Identifier == null || InitialResult is ModuleResult)
			{
				return null;
			}

			var propertyName = Identifier.Value as string;
			if (propertyName == null)
				return null;

			INode relatedNode = null;

			if (InitialResult is MemberResult)
				relatedNode = (InitialResult as MemberResult).ResolvedMember;
			else if (InitialResult is TypeResult)
				relatedNode = (InitialResult as TypeResult).ResolvedTypeDefinition;

			#region init
			if (propertyName == "init")
			{
				var prop_Init = new DVariable
				{
					Name = "init",
					Description = "Initializer"
				};

				if (relatedNode != null)
				{
					if (!(relatedNode is DVariable))
					{
						prop_Init.Parent = relatedNode.Parent;
						prop_Init.Type = new IdentifierDeclaration(relatedNode.Name);
					}
					else
					{
						prop_Init.Parent = relatedNode;
						prop_Init.Initializer = (relatedNode as DVariable).Initializer;
						prop_Init.Type = relatedNode.Type;
					}
				}

				return new MemberResult
				{
					ResultBase = InitialResult,
					MemberBaseTypes = new[] { InitialResult },
					TypeDeclarationBase = Identifier,
					ResolvedMember = prop_Init
				};
			}
			#endregion

			#region sizeof
			if (propertyName == "sizeof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					TypeDeclarationBase = Identifier,
					ResolvedMember = new DVariable
					{
						Name = "sizeof",
						Type = new DTokenDeclaration(DTokens.Int),
						Initializer = new IdentifierExpression(4),
						Description = "Size in bytes (equivalent to C's sizeof(type))"
					}
				};
			#endregion

			#region alignof
			if (propertyName == "alignof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					TypeDeclarationBase = Identifier,
					ResolvedMember = new DVariable
					{
						Name = "alignof",
						Type = new DTokenDeclaration(DTokens.Int),
						Description = "Alignment size"
					}
				};
			#endregion

			#region mangleof
			if (propertyName == "mangleof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					TypeDeclarationBase = Identifier,
					ResolvedMember = new DVariable
					{
						Name = "mangleof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the ‘mangled’ representation of the type"
					},
					MemberBaseTypes = DResolver.ResolveType(new IdentifierDeclaration("string"), ctxt)
				};
			#endregion

			#region stringof
			if (propertyName == "stringof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					TypeDeclarationBase = Identifier,
					ResolvedMember = new DVariable
					{
						Name = "stringof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the source representation of the type"
					},
					MemberBaseTypes = DResolver.ResolveType(new IdentifierDeclaration("string"), ctxt)
				};
			#endregion

			bool
				isArray = false,
				isAssocArray = false,
				isInt = false,
				isFloat = false;

			#region See AbsractCompletionSupport.StaticPropertyAddition
			if (InitialResult is StaticTypeResult)
			{
				var srr = InitialResult as StaticTypeResult;

				var type = srr.TypeDeclarationBase;

				// on things like immutable(char), pass by the surrounding attribute..
				while (type is MemberFunctionAttributeDecl)
					type = (type as MemberFunctionAttributeDecl).InnerType;

				if (type is ArrayDecl)
				{
					var ad = type as ArrayDecl;

					isAssocArray = ad.IsAssociative;
					isArray = !ad.IsAssociative;
				}
				else if (!(type is PointerDecl))
				{
					int TypeToken = srr.BaseTypeToken;

					if (TypeToken <= 0 && type is DTokenDeclaration)
						TypeToken = (type as DTokenDeclaration).Token;

					if (TypeToken > 0)
					{
						// Determine whether float by the var's base type
						isInt = DTokens.BasicTypes_Integral[srr.BaseTypeToken];
						isFloat = DTokens.BasicTypes_FloatingPoint[srr.BaseTypeToken];
					}
				}
			}
			else if (InitialResult is ExpressionResult)
			{
				var err = InitialResult as ExpressionResult;
				var expr = err.Expression;

				// 'Skip' surrounding parentheses
				while (expr is SurroundingParenthesesExpression)
					expr = (expr as SurroundingParenthesesExpression).Expression;

				var idExpr = expr as IdentifierExpression;
				if (idExpr != null)
				{
					// Char literals, Integrals types & Floats
					if ((idExpr.Format & LiteralFormat.Scalar) == LiteralFormat.Scalar || idExpr.Format == LiteralFormat.CharLiteral)
					{
						// Floats also imply integral properties
						isInt = true;
						isFloat = (idExpr.Format & LiteralFormat.FloatingPoint) == LiteralFormat.FloatingPoint;
					}
					// String literals
					else if (idExpr.Format == LiteralFormat.StringLiteral || idExpr.Format == LiteralFormat.VerbatimStringLiteral)
					{
						isArray = true;
					}
				}
				// Pointer conversions (e.g. (myInt*).sizeof)
			}
			#endregion

			//TODO: Resolve static [assoc] array props
			if (isArray || isAssocArray)
			{

			}

			return null;
		}
	}
}
