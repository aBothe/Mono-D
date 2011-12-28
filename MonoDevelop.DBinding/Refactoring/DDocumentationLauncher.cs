using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;
using MonoDevelop.D.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace MonoDevelop.D.Refactoring
{
	public class DDocumentationLauncher
	{
		public const string DigitalMarsUrl = "http://www.d-programming-language.org";

		/// <summary>
		/// Reads the current caret context, and opens the adequate reference site in the default browser
		/// </summary>
		public static string GetReferenceUrl()
		{
			var caret=Ide.IdeApp.Workbench.ActiveDocument.Editor.Caret.Location;

			ResolverContext ctxt = null;
			var rr=DResolverWrapper.ResolveHoveredCode(out ctxt,Ide.IdeApp.Workbench.ActiveDocument);

			return GetReferenceUrl(rr != null ? rr[0] : null, ctxt, new CodeLocation(caret.Column, caret.Line));
		}

		public static string GetReferenceUrl(ResolveResult result,ResolverContext ctxt, CodeLocation caret)
		{
			if (ctxt.ScopedStatement != null)
			{
				if (ctxt.ScopedStatement is IExpressionContainingStatement)
				{
					var exprs = (ctxt.ScopedStatement as IExpressionContainingStatement).SubExpressions;
					IExpression targetExpr = null;

					if (exprs != null)
						foreach (var ex in exprs)
							if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, caret))
								!= ex)
								break;

					return GetRefUrlFor(targetExpr);
				}

				return GetRefUrlFor(ctxt.ScopedStatement);
			}

			if(result!=null)
			{
				var n = DResolverWrapper.GetResultMember(result);
			}

			return "";
		}


		public static string GetRefUrlFor(IStatement stmt)
		{
			return "";
		}

		public static string GetRefUrlFor(IExpression e)
		{
			var url = "expressions.html#";

			if (e is SimpleUnaryExpression || e is UnaryExpression_Type)
				url += "UnaryExpression";
			else if (e is AnonymousClassExpression)
				url += "NewAnonClassExpression";
			else if (e is PostfixExpression)
			{
				if (e is PostfixExpression_Index)
					url += "IndexExpression";
				else if (e is PostfixExpression_Slice)
					url += "SliceExpression";
				else
					url += "PostfixExpression";
			}
			else if (e is PrimaryExpression)
			{
				if (e is TemplateInstanceExpression)
					url = "template.html#TemplateInstance";
				else if (e is TokenExpression)
				{
					var token = (e as TokenExpression).Token;

					if (token == DTokens.This)
						url += "this";
					else if (token == DTokens.Super)
						url += "super";
					else if (token == DTokens.Null)
						url += "null";
				}
				else if (e is IdentifierExpression)
				{
					var id = e as IdentifierExpression;

					if (id.Format == LiteralFormat.Scalar)
						url = "lex.html#IntegerLiteral";
					else if (id.Format.HasFlag(LiteralFormat.FloatingPoint))
						url = "lex.html#FloatLiteral";
					else if (id.Format == LiteralFormat.CharLiteral)
						url += "CharacterLiteral";
					else if (id.Format == LiteralFormat.StringLiteral)
						url += "StringLiterals";
				}
				else if (e is ArrayLiteralExpression)
					url += "ArrayLiteral";
				else if (e is AssocArrayExpression)
					url += "AssocArrayLiteral";
				else if (e is FunctionLiteral)
					url += "FunctionLiteral";
				else if (e is AssertExpression)
					url += "AssertExpression";
				else if (e is MixinExpression)
					url += "MixinExpression";
				else if (e is ImportExpression)
					url += "ImportExpression";
				else if (e is TypeidExpression)
					url += "TypeidExpression";
				else if (e is IsExpression)
					url += "IsExpression";
				else if (e is TraitsExpression)
					url = "traits.html#TraitsExpression";
				else
					url += "PrimaryExpression";
			}
			else
				url += e.GetType().Name;

			return url;
		}
	}
}
