using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Class for scanning through code
	/// </summary>
	public class CodeScanner
	{
		public class CodeScanResult
		{
			public Dictionary<IdentifierDeclaration, INode> ResolvedIdentifiers = new Dictionary<IdentifierDeclaration, INode>();
			public List<IdentifierDeclaration> UnresolvedIdentifiers = new List<IdentifierDeclaration>();
			//public List<ITypeDeclaration> RawIdentifiers = new List<ITypeDeclaration>();

			//public List<IExpression> RawParameterActions = new List<IExpression>();
			/// <summary>
			/// Parameter actions ca be 1) method calls, 2) template instantiations and 3) ctor/ 4) opCall calls.
			/// 1) foo(a,b);
			/// 2) myTemplate!int
			/// 3) new myClass("abc",23,true);
			/// 4) auto p=Point(1,2);
			/// 
			/// Key: method call, template instance or 'new' expression.
			/// Values: best matching methods/classes(!)/ctors
			/// </summary>
			public Dictionary<IExpression, INode[]> ParameterActions = new Dictionary<IExpression,INode[]>();
		}

		/// <summary>
		/// Scans the syntax tree for all kinds of identifier declarations, 
		/// tries to resolve them,
		/// adds them to a dictionary. If not found, 
		/// they will be added to a second, special array.
		/// 
		/// Note: For performance reasons, it's recommended to disable 'ResolveAliases' in the ResolverContext parameter
		/// </summary>
		/// <param name="lastResCtxt"></param>
		/// <param name="SyntaxTree"></param>
		/// <returns></returns>
		public static CodeScanResult ScanSymbols(ResolverContext lastResCtxt)
		{
			var csr = new CodeScanResult();

			var resCache = new ResolutionCache();

			foreach (var importedAST in lastResCtxt.ImportCache)
				resCache.Add(importedAST);

			var typeObjects = CodeScanner.ScanForTypeIdentifiers(lastResCtxt.ScopedBlock.NodeRoot);
	
			foreach (var o in typeObjects)
			{
				if (o is ITypeDeclaration)
					FindAndEnlistType(csr,o as ITypeDeclaration,lastResCtxt,resCache);
				else if(o is IExpression)
					FindAndEnlistType(csr,(o as IExpression).ExpressionTypeRepresentation,lastResCtxt,resCache);
			}

			return csr;
		}

		static IEnumerable<IBlockNode> FindAndEnlistType(
			CodeScanResult csr,
			ITypeDeclaration typeId,
			ResolverContext lastResCtxt,
			ResolutionCache resCache)
		{
			if (typeId == null)
				return null;

			/*
			 * Note: For performance reasons, there is no resolution of type aliases or other contextual symbols!
			 * TODO: Check relationships between the selected block and the found types.
			 */

			if (typeId.InnerDeclaration !=null)
			{
				var res = FindAndEnlistType(csr,typeId.InnerDeclaration, lastResCtxt, resCache);

				if (res != null)
				{
					var cmpName=typeId.ToString(false);

					foreach (var t in res)
					{
						foreach (var m in t)
							if (m.Name == cmpName && (m is DEnum || m is DClassLike))
							{
								csr.ResolvedIdentifiers.Add(typeId as IdentifierDeclaration, m);
								return new[]{m as IBlockNode};
							}

						if (t is DClassLike)
						{
							var dc = t as DClassLike;

							var baseClasses=DResolver.ResolveBaseClass(dc, lastResCtxt);

							if (baseClasses != null)
							{
								var l1 = new List<TypeResult>(baseClasses);
								var l2 = new List<TypeResult>();

								while (l1.Count > 0)
								{
									foreach (var tr in l1)
									{
										foreach (var m in tr.ResolvedTypeDefinition)
											if (m.Name == cmpName && (m is DEnum || m is DClassLike))
											{
												csr.ResolvedIdentifiers.Add(typeId as IdentifierDeclaration, m);
												return new[] { m as IBlockNode };
											}

										l2.AddRange(tr.BaseClass);
									}

									l1.Clear();
									l1.AddRange(l2);
									l2.Clear();
								}
							}
						}
					}
				}
			}

			List<IBlockNode> types = null;
			if (resCache.Types.TryGetValue(typeId.ToString(false), out types))
			{
				csr.ResolvedIdentifiers.Add(typeId as IdentifierDeclaration, types[0]);

				return types;
			}

			IAbstractSyntaxTree module = null;
			if(resCache.Modules.TryGetValue(typeId.ToString(true),out module))
			{
				return new[] { module };
			}

			return null;
		}


		static bool IsAccessible(IAbstractSyntaxTree identifiersModule, INode comparedNode, bool isInBaseClass = false)
		{
			if (isInBaseClass)
				return !(comparedNode as DNode).ContainsAttribute(DTokens.Private);

			if (comparedNode.NodeRoot != identifiersModule)
			{

			}

			return true;
		}



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

				if (dm.Imports != null && dm.Imports.Length > 0)
					foreach (var imp in dm.Imports)
						SearchIn(imp, l);
			}

			while (l1.Count > 0)
			{
				foreach (var n in l1)
				{
					if (n.Type != null)
						SearchIn(n.Type, l);

					if (n is DNode)
					{
						var dn = n as DNode;

						//TODO: Template params still missing
						if(dn.TemplateParameters!=null)
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

						SearchIn(dm.TemplateConstraint,l);

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
						var impStmt = s as ImportStatement;

						SearchIn(impStmt.ModuleIdentifier,l);
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
								SearchIn(d, l);
					}

					if (s is IExpressionContainingStatement)
					{
						var exprs = (s as IExpressionContainingStatement).SubExpressions;

						if (exprs != null && exprs.Length > 0)
							foreach (var e in exprs)
								if(e!=null)
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
			if (type is IdentifierDeclaration && !(type is DTokenDeclaration))
				l.Add(type as IdentifierDeclaration);

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
				else if (type is DExpressionDecl)
				{
					SearchIn((type as DExpressionDecl).Expression, l);
				}
				else if (type is TemplateInstanceExpression)
				{
					var tie=type as TemplateInstanceExpression;

					//l.Add(tie);
					
					if (tie.TemplateIdentifier != null)
						l.Add(tie.TemplateIdentifier);

					var args=tie.Arguments;

					if(args!=null)
						foreach(var arg in args)
							SearchIn(arg, l);
				}

				/*if (type is IdentifierDeclaration && !(type is DTokenDeclaration))
					l.Add(type as IdentifierDeclaration);
				else
				{
					type = type.InnerDeclaration;
					continue;
				}*/

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

					if(e is NewExpression || e is PostfixExpression_Access)
					{
						SearchIn(e.ExpressionTypeRepresentation,l);
						continue;
					}
					else if (e is IdentifierExpression && (e as IdentifierExpression).IsIdentifier)
					{
						l.Add(e.ExpressionTypeRepresentation);
					}
					else if (e is TemplateInstanceExpression)
					{
						var tie = e as TemplateInstanceExpression;

						if (tie.TemplateIdentifier != null)
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
