using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Completion
{
	public class MemberCompletionProvider : AbstractCompletionProvider
	{
		public string lastResultPath;
		List<string> alreadyAddedModuleNameParts = new List<string>();

		public MemberCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		public static bool CompletesEnteredText(string EnteredText)
		{
			return EnteredText == ".";
		}

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
			alreadyAddedModuleNameParts.Clear();

			IStatement curStmt = null;
			var curBlock = DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt);

			if (curBlock == null)
				return;

			var resolveResults = DResolver.ResolveType(
				Editor,
				new ResolverContext
				{
					ScopedBlock = curBlock,
					ParseCache = Editor.ParseCache,
					ImportCache = Editor.ImportCache,
					ScopedStatement = curStmt
				}
				);

			if (resolveResults == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			/*
			 * Note: When having entered a module name stub only (e.g. "std." or "core.") it's needed to show all packages that belong to that root namespace
			 */

			foreach (var rr in resolveResults)
			{
				lastResultPath = rr.ResultPath;
				BuildCompletionData(rr, curBlock);
			}
		}

		void BuildCompletionData(
			ResolveResult rr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false,
			ResolveResult resultParent = null)
		{
			isVariableInstance |= rr.TypeDeclarationBase.ExpressesVariableAccess;

			if (rr is MemberResult)
				BuildMemberCompletionData(rr as MemberResult, currentlyScopedBlock, isVariableInstance);

			// A module path has been typed
			else if (!isVariableInstance && rr is ModuleResult)
				BuildModuleCompletionData(rr as ModuleResult, 0, alreadyAddedModuleNameParts);

			#region A type was referenced directly
			else if (rr is TypeResult)
			{
				var tr = rr as TypeResult;
				var vis = ItemVisibility.All;

				bool HasSameAncestor = HaveSameAncestors(currentlyScopedBlock, tr.ResolvedTypeDefinition);
				bool IsThis = false, IsSuper = false;

				if (tr.TypeDeclarationBase is DTokenDeclaration)
				{
					int token = (tr.TypeDeclarationBase as DTokenDeclaration).Token;
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

				BuildTypeCompletionData(tr, vis);
				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, tr.ResolvedTypeDefinition);
				StaticTypePropertyProvider.AddClassTypeProperties(CompletionDataGenerator, tr.ResolvedTypeDefinition);
			}
			#endregion

			#region Things like int. or char.
			else if (rr is StaticTypeResult)
			{
				var srr = rr as StaticTypeResult;
				
				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null);

				var type = srr.TypeDeclarationBase;

				// on things like immutable(char), pass by the surrounding attribute..
				while (type is MemberFunctionAttributeDecl)
					type = (type as MemberFunctionAttributeDecl).InnerType;

				if (type is ArrayDecl)
				{
					var ad = type as ArrayDecl;

					if(ad.IsAssociative)
						StaticTypePropertyProvider.AddAssocArrayProperties(rr, CompletionDataGenerator, ad);
					else
						StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, ad);
				}
				// Direct pointer accessing - only generic props are available
				else if (type is PointerDecl)
				{
					// Do nothing
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

			#region "abcd" , (200), (0.123) //, [1,2,3,4], [1:"asdf", 2:"hey", 3:"yeah"]
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
			#endregion
		}

		void BuildMemberCompletionData(
			MemberResult mrr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false)
		{
			if (mrr.MemberBaseTypes != null)
				foreach (var i in mrr.MemberBaseTypes)
				{
					BuildCompletionData(i, currentlyScopedBlock,
						(mrr.ResolvedMember is DVariable && (mrr.ResolvedMember as DVariable).IsAlias) ?
							isVariableInstance : true, mrr); // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
				}

			if (mrr.ResultBase == null)
				StaticTypePropertyProvider.AddGenericProperties(mrr, CompletionDataGenerator, mrr.ResolvedMember, false);

		}

		void BuildModuleCompletionData(ModuleResult tr, ItemVisibility visMod,
			List<string> alreadyAddedModuleNames)
		{
			if (!tr.IsOnlyModuleNamePartTyped())
				foreach (var i in tr.ResolvedModule)
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
			else
			{
				var modNameParts = tr.ResolvedModule.ModuleName.Split('.');

				string packageDir = modNameParts[0];
				for (int i = 1; i <= tr.AlreadyTypedModuleNameParts; i++)
					packageDir += "." + modNameParts[i];

				if (tr.AlreadyTypedModuleNameParts < modNameParts.Length - 1)
				{
					// Don't add a package name that already has been added before.. so e.g. show only the first module of package "std.c."
					if (alreadyAddedModuleNames.Contains(packageDir))
						return;

					alreadyAddedModuleNames.Add(packageDir);

					CompletionDataGenerator.Add(modNameParts[tr.AlreadyTypedModuleNameParts], PathOverride: packageDir);
				}
				else
					CompletionDataGenerator.Add(modNameParts[modNameParts.Length - 1], tr.ResolvedModule);
			}
		}

		void BuildTypeCompletionData(TypeResult tr, ItemVisibility visMod)
		{
			var n = tr.ResolvedTypeDefinition;
			if (n is DClassLike) // Add public static members of the class and including all base classes
			{
				var propertyMethodsToIgnore = new List<string>();

				var curlevel = tr;
				var tvisMod = visMod;
				while (curlevel != null)
				{
					foreach (var i in curlevel.ResolvedTypeDefinition)
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
							else if (dn is DEnum && dn.Name == "")
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
