using System;
using System.Collections.Generic;
using System.Text;
using D_Parser.Resolver;
using D_Parser.Dom;
using MonoDevelop.D.Resolver;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.IO;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Building;
using MonoDevelop.Ide;
using D_Parser.Resolver.ExpressionSemantics;

namespace MonoDevelop.D.Refactoring
{
	public class DDocumentationLauncher
	{
		public static string DigitalMarsUrl = "http://www.dlang.org";

		public static void LaunchRelativeDUrl (string relativeUrl)
		{
			var url = DigitalMarsUrl + '/' + relativeUrl;

			if (Directory.Exists(DigitalMarsUrl))
			{
				if (OS.IsWindows)
					url = url.Replace('/','\\');

				if (!url.StartsWith("file:///"))
					url = "file:///" + url;
			}

			try
			{
				System.Diagnostics.Process.Start(url);
			}
			catch(Exception ex)
			{
				MessageService.ShowException(ex, "Exception thrown while trying to open manual webpage");
			}
		}

		/// <summary>
		/// Reads the current caret context, and opens the adequate reference site in the default browser
		/// </summary>
		public static string GetReferenceUrl ()
		{
			var caret = Ide.IdeApp.Workbench.ActiveDocument.Editor.Caret.Location;

			ResolutionContext ctxt = null;
			var rr = DResolverWrapper.ResolveHoveredCode (out ctxt, Ide.IdeApp.Workbench.ActiveDocument);

			return GetReferenceUrl (rr != null ? rr [0] : null, ctxt, new CodeLocation (caret.Column, caret.Line));
		}

		public static string GetReferenceUrl (AbstractType result, ResolutionContext ctxt, CodeLocation caret)
		{
			if (result != null) {
				var n = DResolver.GetResultMember (result);

				if (n != null && n.NodeRoot is IAbstractSyntaxTree) {
					if (IsPhobosModule (n.NodeRoot as IAbstractSyntaxTree)) {
						var phobos_url = "phobos/" + (n.NodeRoot as IAbstractSyntaxTree).ModuleName.Replace ('.', '_') + ".html";

						if (!(n is IAbstractSyntaxTree))
							phobos_url += "#" + n.Name;

						return phobos_url;
					}
				} else if (result is PrimitiveType || result is DelegateType || result is AssocArrayType) {

					if (result.DeclarationOrExpressionBase is ITypeDeclaration)
						return GetRefUrlFor ((ITypeDeclaration)result.DeclarationOrExpressionBase);
					else if (result.DeclarationOrExpressionBase is IExpression)
						return GetRefUrlFor ((IExpression)result.DeclarationOrExpressionBase);
				}
			}

			if (ctxt.ScopedStatement != null) {
				return GetRefUrlFor (ctxt.ScopedStatement, caret);
			} else if (ctxt.ScopedBlock is DClassLike) {
				var dc = ctxt.ScopedBlock as DClassLike;

				if (dc.ClassType == DTokens.Class)
					return "class.html";
				else if (dc.ClassType == DTokens.Interface)
					return "interface.html";
				else if (dc.ClassType == DTokens.Struct || dc.ClassType == DTokens.Union)
					return "struct.html";
				else if (dc.ClassType == DTokens.Template)
					return "template.html";
			} else if (ctxt.ScopedBlock is DEnum)
				return "enum.html";
			

			return null;
		}

		public static bool IsPhobosModule (IAbstractSyntaxTree Module)
		{
			return 
				Module.FileName.Contains (Path.DirectorySeparatorChar + "phobos" + Path.DirectorySeparatorChar) ||
				Module.FileName.Contains (Path.DirectorySeparatorChar + "druntime" + Path.DirectorySeparatorChar + "import" + Path.DirectorySeparatorChar);
		}

		public static string GetRefUrlFor (IStatement s, CodeLocation caret)
		{
			if (s is IExpressionContainingStatement) {
				var exprs = (s as IExpressionContainingStatement).SubExpressions;
				IExpression targetExpr = null;

				if (exprs != null)
					foreach (var ex in exprs)
						if (caret >= ex.Location && 
							caret <= ex.EndLocation &&
							(targetExpr = ExpressionHelper.SearchExpressionDeeply (ex, caret))
							!= ex)
							break;

				if (targetExpr != null)
					return GetRefUrlFor (targetExpr);
			}

			if (s is DeclarationStatement) {
				var ds = s as DeclarationStatement;

				foreach (var decl in ds.Declarations) {
					if (caret >= decl.Location && caret <= decl.EndLocation) {
						if (decl is DVariable) {
							var dv = decl as DVariable;

							if (dv.Initializer != null &&
								caret >= dv.Location &&
								caret <= dv.EndLocation)
								return GetRefUrlFor (dv.Initializer);
						}
					}
				}
			}
			
			if (s is StatementContainingStatement) {
				var stmts = (s as StatementContainingStatement).SubStatements;

				if (stmts != null)
					foreach (var stmt in stmts) {
						if (caret >= stmt.Location && caret <= stmt.EndLocation) {
							var r = GetRefUrlFor (stmt, caret);

							if (r != null)
								return r;
						}
					}
			}


			var url = "statement.html#";

			if (s is ForeachStatement && (s as ForeachStatement).IsRangeStatement)
				url += "ForeachRangeStatement";
			else if (s is StatementCondition)
			{
				var sc = (StatementCondition) s;
				if(sc.Condition is DebugCondition)
					url = "version.html#DebugCondition";
				else if (sc.Condition is VersionCondition)
					url = "version.html#VersionCondition";
				else if (sc.Condition is StaticIfCondition)
					url = "version.html#StaticIfCondition";
			}
			else
				url += s.GetType ().Name;

			return url;
		}

		public static string GetRefUrlFor (ITypeDeclaration t)
		{
			var url = "declaration.html";

			if (t is ArrayDecl || t is PointerDecl)
				url = "arrays.html";
			else if (t is TypeOfDeclaration)
				url += "#Typeof";
			else if (t is DTokenDeclaration)
				url = "type.html";
			else if (t is DelegateDeclaration)
				url += "#BasicType2";

			return url;
		}

		public static string GetRefUrlFor (IExpression e)
		{
			var url = "expression.html#";

			if (e is SimpleUnaryExpression || e is UnaryExpression_Type)
				url += "UnaryExpression";
			else if (e is AnonymousClassExpression)
				url += "NewAnonClassExpression";
			else if (e is PostfixExpression) {
				if (e is PostfixExpression_Index)
					url += "IndexExpression";
				else if (e is PostfixExpression_Slice)
					url += "SliceExpression";
				else
					url += "PostfixExpression";
			} else if (e is PrimaryExpression) {
				if (e is TemplateInstanceExpression)
					url = "template.html#TemplateInstance";
				else if (e is TokenExpression) {
					var token = (e as TokenExpression).Token;

					if (token == DTokens.This)
						url += "this";
					else if (token == DTokens.Super)
						url += "super";
					else if (token == DTokens.Null)
						url += "null";
					else
						url += "PrimaryExpression";
				} else if (e is IdentifierExpression) {
					var id = e as IdentifierExpression;

					if (id.Format == LiteralFormat.Scalar)
						url = "lex.html#IntegerLiteral";
					else if (id.Format.HasFlag (LiteralFormat.FloatingPoint))
						url = "lex.html#FloatLiteral";
					else if (id.Format == LiteralFormat.CharLiteral)
						url += "CharacterLiteral";
					else if (id.Format == LiteralFormat.StringLiteral)
						url += "StringLiterals";
				} else if (e is ArrayLiteralExpression)
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
			} else if (e is TypeDeclarationExpression)
				return GetRefUrlFor ((e as TypeDeclarationExpression).Declaration);
			else
				url += e.GetType ().Name;

			return url;
		}
	}
}
