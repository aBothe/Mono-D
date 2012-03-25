using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
{
	public class MemberCompletionProvider : AbstractCompletionProvider
	{
		public PostfixExpression_Access AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;

		public string lastResultPath;

		public MemberCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		public enum ItemVisibility
		{
			All = 1,
			StaticMembers = 2,
			PublicStaticMembers = 4,
			PublicMembers = 8,
			ProtectedMembers = 16,
			ProtectedStaticMembers = 32
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			var resolveResults = ExpressionTypeResolver.Resolve(AccessExpression, ResolverContextStack.Create(Editor));

			if (resolveResults == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			foreach (var rr in resolveResults)
			{
				lastResultPath = rr.ResultPath;
				BuildCompletionData(rr, ScopedBlock);
			}
		}

		void BuildCompletionData(
			ResolveResult rr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false,
			ResolveResult resultParent = null)
		{
			if (rr == null)
				return;

			if(rr.DeclarationOrExpressionBase is ITypeDeclaration)
				isVariableInstance |= (rr.DeclarationOrExpressionBase as ITypeDeclaration).ExpressesVariableAccess;

			if (rr is MemberResult)
				BuildCompletionData((MemberResult)rr, currentlyScopedBlock, isVariableInstance);

			// A module path has been typed
			else if (!isVariableInstance && rr is ModuleResult)
				BuildCompletionData((ModuleResult)rr);

			else if (rr is ModulePackageResult)
				BuildCompletionData((ModulePackageResult)rr);

			#region A type was referenced directly
			else if (rr is TypeResult)
			{
				var tr = rr as TypeResult;
				var vis = ItemVisibility.All;

				bool HasSameAncestor = HaveSameAncestors(currentlyScopedBlock, tr.Node);
				bool IsThis = false, IsSuper = false;

				if (tr.DeclarationOrExpressionBase is TokenExpression)
				{
					int token = ((TokenExpression)tr.DeclarationOrExpressionBase).Token;
					IsThis = token == DTokens.This;
					IsSuper = token == DTokens.Super;
				}

				// Cases:

				// myVar. (located in basetype definition)		<-- Show everything
				// this. 										<-- Show everything
				if (IsThis || (isVariableInstance && HasSameAncestor))
					vis = ItemVisibility.All;

				// myVar. (not located in basetype definition) 	<-- Show public and public static members
				else if (isVariableInstance && !HasSameAncestor)
					vis = ItemVisibility.PublicMembers | ItemVisibility.PublicStaticMembers;

				// super. 										<-- Show protected|public or protected|public static base type members
				else if (IsSuper)
					vis = ItemVisibility.ProtectedMembers | ItemVisibility.PublicMembers | ItemVisibility.PublicStaticMembers;

				// myClass. (not located in myClass)			<-- Show public static members
				else if (!isVariableInstance && !HasSameAncestor)
					vis = ItemVisibility.PublicStaticMembers;

				// myClass. (located in myClass)				<-- Show all static members
				else if (!isVariableInstance && HasSameAncestor)
					vis = ItemVisibility.StaticMembers;

				BuildCompletionData(tr, vis);
				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, tr.Node);

				if (tr.Node is DClassLike)
					StaticTypePropertyProvider.AddClassTypeProperties(CompletionDataGenerator, tr.Node);
			}
			#endregion

			#region Things like int. or char.
			else if (rr is StaticTypeResult)
			{
				var srr = rr as StaticTypeResult;

				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null);

				var type = srr.DeclarationOrExpressionBase;

				// on things like immutable(char), pass by the surrounding attribute..
				while (type is MemberFunctionAttributeDecl)
					type = (type as MemberFunctionAttributeDecl).InnerType;

				if (type is PointerDecl)
				{
					if (!(rr.ResultBase is StaticTypeResult && rr.ResultBase.DeclarationOrExpressionBase is PointerDecl))
						BuildCompletionData(rr.ResultBase, currentlyScopedBlock, true, rr);
				}
				else
				{
					int TypeToken = srr.BaseTypeToken;

					if (TypeToken <= 0 && type is DTokenDeclaration)
						TypeToken = (type as DTokenDeclaration).Token;

					if (TypeToken > 0)
					{
						// Determine whether float by the var's base type
						bool isFloat = DTokens.BasicTypes_FloatingPoint[srr.BaseTypeToken];

						// Float implies integral props
						if (DTokens.BasicTypes_Integral[srr.BaseTypeToken] || isFloat)
							StaticTypePropertyProvider.AddIntegralTypeProperties(srr.BaseTypeToken, rr, CompletionDataGenerator, null, isFloat);

						if (isFloat)
							StaticTypePropertyProvider.AddFloatingTypeProperties(srr.BaseTypeToken, rr, CompletionDataGenerator, null);
					}
				}
			}
			#endregion

			else if (rr is ArrayResult)
			{
				var ar = rr as ArrayResult;

				if (ar.ArrayDeclaration!=null && ar.ArrayDeclaration.IsAssociative)
					StaticTypePropertyProvider.AddAssocArrayProperties(rr, CompletionDataGenerator, ar.ArrayDeclaration);
				else
					StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, ar.ArrayDeclaration);
			}

			/*
			else if (rr is ExpressionResult)
			{
				var err = rr as ExpressionResult;
				var expr = err.Expression;

				// 'Skip' surrounding parentheses
				while (expr is SurroundingParenthesesExpression)
					expr = (expr as SurroundingParenthesesExpression).Expression;

				var idExpr = expr as IdentifierExpression;
				if (idExpr != null)
				{
					// Char literals, Integrals types & Floats
					if (idExpr.Format.HasFlag(LiteralFormat.Scalar) || idExpr.Format == LiteralFormat.CharLiteral)
					{
						StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null, true);
						bool isFloat = (idExpr.Format & LiteralFormat.FloatingPoint) == LiteralFormat.FloatingPoint;
						// Floats also imply integral properties
						StaticTypePropertyProvider.AddIntegralTypeProperties(DTokens.Int, rr, CompletionDataGenerator, null, resultParent == null && isFloat);

						// Float-exclusive props
						if (isFloat)
							StaticTypePropertyProvider.AddFloatingTypeProperties(DTokens.Float, rr, CompletionDataGenerator, null, resultParent==null);
					}
					// String literals
					else if (idExpr.Format == LiteralFormat.StringLiteral || idExpr.Format == LiteralFormat.VerbatimStringLiteral)
					{
						StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null, resultParent==null);
						StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, new ArrayDecl()
						{
							ValueType =
								new MemberFunctionAttributeDecl(DTokens.Immutable)
								{
									InnerType =
										new DTokenDeclaration(DTokens.Char)
								}
						});
					}
				}
				// Pointer conversions (e.g. (myInt*).sizeof)
			}
			*/
			
		}

		void BuildCompletionData(MemberResult mrr, IBlockNode currentlyScopedBlock, bool isVariableInstance = false)
		{
			if (mrr.MemberBaseTypes != null)
				foreach (var i in mrr.MemberBaseTypes)
				{
					BuildCompletionData(i, currentlyScopedBlock,
						(mrr.Node is DVariable && (mrr.Node as DVariable).IsAlias) ?
							isVariableInstance : true, mrr); // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
				}

			if (mrr.ResultBase == null)
				StaticTypePropertyProvider.AddGenericProperties(mrr, CompletionDataGenerator, mrr.Node, false);

		}

		void BuildCompletionData(ModulePackageResult mpr)
		{
			foreach (var kv in mpr.Package.Packages)
				CompletionDataGenerator.Add(kv.Key);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.Add(kv.Key, kv.Value);
		}

		void BuildCompletionData(ModuleResult tr)
		{
			foreach (var i in tr.Module)
			{
				var di = i as DNode;
				if (di == null)
				{
					if (i != null)
						CompletionDataGenerator.Add(i);
					continue;
				}

				if (di.IsPublic && CanItemBeShownGenerally(i))
					CompletionDataGenerator.Add(i);
			}
		}

		void BuildCompletionData(TypeResult tr, ItemVisibility visMod)
		{
			var n = tr.Node;
			if (n is DClassLike) // Add public static members of the class and including all base classes
			{
				var propertyMethodsToIgnore = new List<string>();

				var curlevel = tr;
				var tvisMod = visMod;
				while (curlevel != null)
				{
					foreach (var i in curlevel.Node as IBlockNode)
					{
						var dn = i as DNode;

						if (i != null && dn == null)
						{
							CompletionDataGenerator.Add(i);
							continue;
						}

						bool add = false;

						if (tvisMod.HasFlag(ItemVisibility.All))
							add = true;
						else
						{
							if (tvisMod.HasFlag(ItemVisibility.ProtectedMembers))
								add |= dn.ContainsAttribute(DTokens.Protected);
							if (tvisMod.HasFlag(ItemVisibility.ProtectedStaticMembers))
								add |= dn.ContainsAttribute(DTokens.Protected) && (dn.IsStatic || IsTypeNode(i));
							if (tvisMod.HasFlag(ItemVisibility.PublicMembers))
								add |= dn.IsPublic;
							if (tvisMod.HasFlag(ItemVisibility.PublicStaticMembers))
								add |= dn.IsPublic && (dn.IsStatic || IsTypeNode(i));
							if (tvisMod.HasFlag(ItemVisibility.StaticMembers))
								add |= dn.IsStatic || IsTypeNode(i);
						}

						if (add)
						{
							if (CanItemBeShownGenerally(dn))
							{
								// Convert @property getters&setters to one unique property
								if (dn is DMethod && dn.ContainsPropertyAttribute())
								{
									if (!propertyMethodsToIgnore.Contains(dn.Name))
									{
										var dm = dn as DMethod;
										bool isGetter = dm.Parameters.Count < 1;

										var virtPropNode = new DVariable();

										virtPropNode.AssignFrom(dn);

										if (!isGetter)
											virtPropNode.Type = dm.Parameters[0].Type;

										CompletionDataGenerator.Add(virtPropNode);

										propertyMethodsToIgnore.Add(dn.Name);
									}
								}
								else
									CompletionDataGenerator.Add(dn);
							}

							// Add members of anonymous enums
							else if (dn is DEnum && string.IsNullOrEmpty(dn.Name))
							{
								foreach (var k in dn as DEnum)
									CompletionDataGenerator.Add(k);
							}
						}
					}
					curlevel = curlevel.BaseClass != null ? curlevel.BaseClass[0] : null;

					// After having shown all items on the current node level,
					// allow showing public (static) and/or protected items in the more basic levels then
					if (tvisMod.HasFlag(ItemVisibility.All))
					{
						if ((n as DClassLike).ContainsAttribute(DTokens.Static))
							tvisMod = ItemVisibility.ProtectedStaticMembers | ItemVisibility.PublicStaticMembers;
						else
							tvisMod = ItemVisibility.ProtectedMembers | ItemVisibility.PublicMembers;
					}
				}
			}
			else if (n is DEnum)
			{
				var de = n as DEnum;

				foreach (var i in de)
					if (i is DEnumValue)
						CompletionDataGenerator.Add(i);
			}
		}
	}
}
