using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Class for scanning symbols inside a parsed code file.
	/// </summary>
	public partial class CodeSymbolsScanner
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

			if (lastResCtxt.ScopedBlock != null)
				resCache.Add(lastResCtxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree);

			foreach (var importedAST in lastResCtxt.ImportCache)
				resCache.Add(importedAST);

			var typeObjects = IdentifierScan.ScanForTypeIdentifiers(lastResCtxt.ScopedBlock.NodeRoot);

			foreach (var o in typeObjects)
			{
				if (o is ITypeDeclaration)
					FindAndEnlistType(csr, o as ITypeDeclaration, lastResCtxt, resCache);
				else if (o is IExpression)
					FindAndEnlistType(csr, (o as IExpression).ExpressionTypeRepresentation, lastResCtxt, resCache);
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

										if(tr.BaseClass!=null)
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
				csr.ResolvedIdentifiers.Add(typeId as IdentifierDeclaration, module);

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
	}
}
