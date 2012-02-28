using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver.ASTScanner
{
	public partial class CodeSymbolsScanner
	{
		public class IdentifierScan
		{
			/// <summary>
			/// The returned list will contain all
			/// 1) IdentifierDeclaration
			/// 2) PostfixExpression_Access
			/// 3) IdentifierExpression
			/// occurring in the given Node
			/// </summary>
			public static List<object> ScanForTypeIdentifiers(INode Node)
			{
				var l = new List<object>();

				if (Node != null)
					SearchIn(Node, l);

				return l;
			}

			static void SearchIn(INode node, List<object> l)
			{
				if (node == null)
					return;

				var l1 = new List<INode> { node };
				var l2 = new List<INode>();

				if (node is DModule)
				{
					var dm = node as DModule;

					if (dm.OptionalModuleStatement != null)
						SearchIn(dm.OptionalModuleStatement, l);
				}

				while (l1.Count > 0)
				{
					foreach (var n in l1)
					{
						if (n.Type != null)
							SearchIn(n.Type, l);

						if (n is DBlockNode)
							foreach (var stmt in ((DBlockNode)n).StaticStatements)
								SearchIn(stmt, l);

						if (n is DNode)
						{
							var dn = n as DNode;

							//TODO: Template params still missing
							if (dn.TemplateParameters != null)
								foreach (var tp in dn.TemplateParameters)
								{
									if (tp is TemplateValueParameter)
									{
										var tvp = tp as TemplateValueParameter;

										SearchIn(tvp.Type, l);
										SearchIn(tvp.DefaultExpression, l);
										SearchIn(tvp.SpecializationExpression, l);
									}
								}
						}

						if (n is DMethod)
						{
							var dm = n as DMethod;

							l2.AddRange(dm.Parameters);

							if (dm.AdditionalChildren.Count > 0)
								l2.AddRange(dm.AdditionalChildren);

							SearchIn(dm.TemplateConstraint, l);

							SearchIn(dm.In, l);
							SearchIn(dm.Out, l);
							SearchIn(dm.Body, l);
						}

						if (n is DVariable)
						{
							var dv = n as DVariable;

							SearchIn(dv.Initializer, l);
						}

						if (n is DClassLike)
						{
							var dc = n as DClassLike;
							foreach (var bc in dc.BaseClasses)
								SearchIn(bc, l);

							SearchIn(dc.TemplateConstraint, l);
						}

						if (n is IBlockNode && !(n is DMethod))
							l2.AddRange((n as IBlockNode).Children);
					}

					l1.Clear();
					l1.AddRange(l2);
					l2.Clear();
				}
			}

			static void SearchIn(IStatement stmt, List<object> l)
			{
				if (stmt == null)
					return;

				var l1 = new List<IStatement> { stmt };
				var l2 = new List<IStatement>();

				while (l1.Count > 0)
				{
					foreach (var s in l1)
					{
						if (s is ImportStatement)
						{
							var impStmt = (ImportStatement)s;

							foreach (var imp in impStmt.Imports)
								SearchIn(imp.ModuleIdentifier, l);

							if (impStmt.ImportBinding != null)
								SearchIn(impStmt.ImportBinding.Module.ModuleIdentifier, l);
						}

						if (s is StatementContainingStatement)
						{
							var sstmts = (s as StatementContainingStatement).SubStatements;

							if (sstmts != null && sstmts.Length > 0)
								l2.AddRange(sstmts);
						}

						// Don't add declarations & declaration statements twice
						if (!(s is BlockStatement) && s is IDeclarationContainingStatement)
						{
							var decls = (s as IDeclarationContainingStatement).Declarations;

							if (decls != null && decls.Length > 0)
								foreach (var d in decls)
									if (!(d is DVariable)) // Initializers are searched already
										SearchIn(d, l);
						}

						if (s is IExpressionContainingStatement)
						{
							var exprs = (s as IExpressionContainingStatement).SubExpressions;

							if (exprs != null && exprs.Length > 0)
								foreach (var e in exprs)
									if (e != null)
										SearchIn(e, l);
						}
					}

					l1.Clear();
					l1.AddRange(l2);
					l2.Clear();
				}
			}

			static void SearchIn(ITypeDeclaration type, List<object> l)
			{
				while (type != null)
				{
					if (type is DelegateDeclaration)
						foreach (var p in (type as DelegateDeclaration).Parameters)
							SearchIn(p, l);
					else if (type is ArrayDecl)
					{
						var ad = type as ArrayDecl;

						if (ad.KeyExpression != null)
							SearchIn(ad.KeyExpression, l);
						if (ad.KeyType != null)
							SearchIn(ad.KeyType, l);
					}
					else if (type is TemplateInstanceExpression)
					{
						var tie = type as TemplateInstanceExpression;

						if (tie.TemplateIdentifier != null && !l.Contains(tie.TemplateIdentifier))
							l.Add(tie.TemplateIdentifier);

						var args = tie.Arguments;

						if (args != null)
							foreach (var arg in args)
								SearchIn(arg, l);
					}

					if (type is IdentifierDeclaration && !(type is DTokenDeclaration))
					{
						if(!l.Contains(type))
							l.Add(type as IdentifierDeclaration);
						break;
					}

					type = type.InnerDeclaration;
				}
			}

			static void SearchIn(IExpression ex, List<object> l)
			{
				if (ex == null)
					return;

				var l1 = new List<IExpression> { ex };
				var l2 = new List<IExpression>();

				while (l1.Count > 0)
				{
					foreach (var e in l1)
					{
						if (e is UnaryExpression_Type)
							SearchIn((e as UnaryExpression_Type).Type, l);

						if (e is NewExpression)
						{
							SearchIn((e as NewExpression).Type, l);
							continue;
						}
						else if (e is PostfixExpression_Access)
							l.Add(e);
						else if (e is IdentifierExpression && (e as IdentifierExpression).IsIdentifier)
							l.Add(e);
						else if (e is TemplateInstanceExpression)
						{
							var tie = e as TemplateInstanceExpression;
							
							if (tie.TemplateIdentifier != null && !l.Contains(tie.TemplateIdentifier))
								l.Add(tie.TemplateIdentifier);
						}

						if (e is ContainerExpression)
						{
							var ec = e as ContainerExpression;
							var subex = ec.SubExpressions;

							if (subex != null)
								l2.AddRange(subex);
						}
					}

					l1.Clear();
					l1.AddRange(l2);
					l2.Clear();
				}
			}
		}
	}
}
