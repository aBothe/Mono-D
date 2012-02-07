using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Completion
{
	public class AbstractCompletionSupport
	{
		public readonly ICompletionDataGenerator CompletionDataGenerator;
		readonly List<string> alreadyAddedModuleNameParts = new List<string>();

		public AbstractCompletionSupport(ICompletionDataGenerator CompletionDataGenerator)
		{
			this.CompletionDataGenerator = CompletionDataGenerator;
		}

		#region Helper Methods
		public static bool IsIdentifierChar(char key)
		{
			return char.IsLetterOrDigit(key) || key == '_';
		}

		enum ItemVisibility
		{
			All=1,
			StaticMembers=2,
			PublicStaticMembers=4,
			PublicMembers=8,
			ProtectedMembers=16,
			ProtectedStaticMembers=32
		}

		public static bool CanItemBeShownGenerally(INode dn)
		{
			if (dn == null || string.IsNullOrEmpty(dn.Name))
				return false;

			if (dn is DMethod)
			{
				var dm = dn as DMethod;

				if (dm.SpecialType == DMethod.MethodType.Unittest ||
					dm.SpecialType == DMethod.MethodType.Destructor ||
					dm.SpecialType == DMethod.MethodType.Constructor)
					return false;
			}

			return true;
		}

		public static bool HaveSameAncestors(INode higherLeveledNode, INode lowerLeveledNode)
		{
			var curPar = higherLeveledNode;

			while (curPar != null)
			{
				if (curPar == lowerLeveledNode)
					return true;

				curPar = curPar.Parent;
			}
			return false;
		}

		static bool IsTypeNode(INode n)
		{
			return n is DEnum || n is DClassLike;
		}

		/// <summary>
		/// Returns C:\fx\a\b when PhysicalFileName was "C:\fx\a\b\c\Module.d" , ModuleName= "a.b.c.Module" and WantedDirectory= "a.b"
		/// 
		/// Used when formatting package names in BuildCompletionData();
		/// </summary>
		public static string GetModulePath(string PhysicalFileName, string ModuleName, string WantedDirectory)
		{
			return GetModulePath(PhysicalFileName, ModuleName.Split('.').Length, WantedDirectory.Split('.').Length);
		}

		public static string GetModulePath(string PhysicalFileName, int ModuleNamePartAmount, int WantedDirectoryNamePartAmount)
		{
			var ret = "";

			var physFileNameParts = PhysicalFileName.Split('\\');
			for (int i = 0; i < physFileNameParts.Length - ModuleNamePartAmount + WantedDirectoryNamePartAmount; i++)
				ret += physFileNameParts[i] + "\\";

			return ret.TrimEnd('\\');
		}
		#endregion

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Editor"></param>
		/// <param name="EnteredText"></param>
		/// <param name="lastResultPath"></param>
		public void BuildCompletionData(IEditorData Editor,
			string EnteredText,
			out string lastResultPath)
		{
			lastResultPath = null;

			IStatement curStmt = null;
			var curBlock = DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt);

			if (curBlock == null)
				return;

			// If typing a begun identifier, return immediately
			if ((EnteredText!=null && EnteredText.Length>0 ? IsIdentifierChar(EnteredText[0]):true) &&
				Editor.CaretOffset > 0 &&
				IsIdentifierChar(Editor.ModuleCode[Editor.CaretOffset - 1]))
				return;

			if (CaretContextAnalyzer.IsInCommentAreaOrString(Editor.ModuleCode, Editor.CaretOffset))
				return;

			IEnumerable<INode> listedItems = null;

			// Usually shows variable members
			#region DotCompletion
			if (EnteredText == ".")
			{
				alreadyAddedModuleNameParts.Clear();

				var resolveResults = DResolver.ResolveType(
					Editor,
					new ResolverContext
					{
						ScopedBlock = curBlock,
						ParseCache = Editor.ParseCache,
						ImportCache = Editor. ImportCache,
						ScopedStatement=curStmt
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
			#endregion

			else if (
				string.IsNullOrWhiteSpace(EnteredText) ||
				IsIdentifierChar(EnteredText[0]))
			{
				// 1) Get current context the caret is at
				ParserTrackerVariables trackVars = null;

				var parsedBlock = DResolver.FindCurrentCaretContext(
					Editor.ModuleCode,
					curBlock,
					Editor.CaretOffset,
					Editor.CaretLocation,
					out trackVars);

				var visibleMembers = DResolver.MemberTypes.All;

				// 2) If in declaration and if node identifier is expected, do not show any data
				if (trackVars == null)
				{
					// --> Happens if no actual declaration syntax given --> Show types/imports/keywords anyway
					visibleMembers = DResolver.MemberTypes.Imports | DResolver.MemberTypes.Types | DResolver.MemberTypes.Keywords;

					listedItems = DResolver.EnumAllAvailableMembers(curBlock, null, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
				}
				else
				{
					if (trackVars.LastParsedObject is INode && 
						string.IsNullOrEmpty((trackVars.LastParsedObject as INode).Name) &&
						trackVars.ExpectingIdentifier)
						return;

					if (trackVars.LastParsedObject is TokenExpression &&
						DTokens.BasicTypes[(trackVars.LastParsedObject as TokenExpression).Token] &&
						!string.IsNullOrEmpty(EnteredText) &&
						IsIdentifierChar(EnteredText[0]))
						return;

					if (trackVars.LastParsedObject is DAttribute)
					{
						var attr = trackVars.LastParsedObject as DAttribute;

						if (attr.IsStorageClass && attr.Token != DTokens.Abstract)
							return;
					}

					if (trackVars.LastParsedObject is ImportStatement /*&& !CaretAfterLastParsedObject*/)
						visibleMembers = DResolver.MemberTypes.Imports;
					else if (trackVars.LastParsedObject is NewExpression && (trackVars.IsParsingInitializer/* || !CaretAfterLastParsedObject*/))
						visibleMembers = DResolver.MemberTypes.Imports | DResolver.MemberTypes.Types;
					else if (EnteredText == " ")
						return;
					// In class bodies, do not show variables
					else if (!(parsedBlock is BlockStatement || trackVars.IsParsingInitializer))
						visibleMembers = DResolver.MemberTypes.Imports | DResolver.MemberTypes.Types | DResolver.MemberTypes.Keywords;

					// In a method, parse from the method's start until the actual caret position to get an updated insight
					if (visibleMembers.HasFlag(DResolver.MemberTypes.Variables) && curBlock is DMethod)
					{
						if (parsedBlock is BlockStatement)
						{
							var blockStmt = parsedBlock as BlockStatement;

							// Insert the updated locals insight.
							// Do not take the caret location anymore because of the limited parsing of our code.
							var scopedStmt = blockStmt.SearchStatementDeeply(blockStmt.EndLocation /*Editor.CaretLocation*/);

							var decls = BlockStatement.GetItemHierarchy(scopedStmt, Editor.CaretLocation);

							foreach (var n in decls)
								CompletionDataGenerator.Add(n);
						}
					}

					if (visibleMembers != DResolver.MemberTypes.Imports) // Do not pass the curStmt because we already inserted all updated locals a few lines before!
						listedItems = DResolver.EnumAllAvailableMembers(curBlock, null/*, curStmt*/, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
				}

				//TODO: Split the keywords into such that are allowed within block statements and non-block statements
				// Insert typable keywords
				if (visibleMembers.HasFlag(DResolver.MemberTypes.Keywords))
					foreach (var kv in DTokens.Keywords)
						CompletionDataGenerator.Add(kv.Key);

				#region Add module name stubs of importable modules
				if (visibleMembers.HasFlag(DResolver.MemberTypes.Imports))
				{
					var nameStubs = new Dictionary<string, string>();
					var availModules = new List<IAbstractSyntaxTree>();
					foreach (var mod in Editor.ParseCache)
					{
						if (string.IsNullOrEmpty(mod.ModuleName))
							continue;

						var parts = mod.ModuleName.Split('.');

						if (!nameStubs.ContainsKey(parts[0]) && !availModules.Contains(mod))
						{
							if (parts[0] == mod.ModuleName)
								availModules.Add(mod);
							else
								nameStubs.Add(parts[0], GetModulePath(mod.FileName, parts.Length, 1));
						}
					}

					foreach (var kv in nameStubs)
						CompletionDataGenerator.Add(kv.Key, PathOverride: kv.Value);

					foreach (var mod in availModules)
						CompletionDataGenerator.Add(mod.ModuleName, mod);
				}
				#endregion
			}
			else if (EnteredText == "@")
				foreach (var propAttr in new[] {
					"disable",
					"property",
					"safe",
					"system",
					"trusted"
				})
					CompletionDataGenerator.AddPropertyAttribute(propAttr);


			// Add all found items to the referenced list
			if (listedItems != null)
				foreach (var i in listedItems)
				{
					if (CanItemBeShownGenerally(i))
						CompletionDataGenerator.Add(i);
				}
		}

		void BuildCompletionData(
			ResolveResult rr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false,
			ResolveResult resultParent = null)
		{
			if (rr is MemberResult)
				BuildMemberCompletionData(rr as MemberResult,currentlyScopedBlock,isVariableInstance,resultParent);

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
			}
			#endregion

			#region Things like int. or char.
			else if (rr is StaticTypeResult)
			{
				var srr = rr as StaticTypeResult;
				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null, true);

				var type = srr.TypeDeclarationBase;

				// on things like immutable(char), pass by the surrounding attribute..
				while (type is MemberFunctionAttributeDecl)
					type = (type as MemberFunctionAttributeDecl).InnerType;

				if (type is ArrayDecl)
				{
					var ad = type as ArrayDecl;

					// Normal array
					if (ad.KeyType is DTokenDeclaration && DTokens.BasicTypes_Integral[(ad.KeyType as DTokenDeclaration).Token])
					{
						StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, ad);
					}
					// Associative array
					else
					{
						StaticTypePropertyProvider.AddAssocArrayProperties(rr, CompletionDataGenerator, ad);
					}
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
					if ((idExpr.Format & LiteralFormat.Scalar) == LiteralFormat.Scalar || idExpr.Format == LiteralFormat.CharLiteral)
					{
						StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null, true);
						bool isFloat = (idExpr.Format & LiteralFormat.FloatingPoint) == LiteralFormat.FloatingPoint;
						// Floats also imply integral properties
						StaticTypePropertyProvider.AddIntegralTypeProperties(DTokens.Int, rr, CompletionDataGenerator, null, isFloat);

						// Float-exclusive props
						if (isFloat)
							StaticTypePropertyProvider.AddFloatingTypeProperties(DTokens.Float, rr, CompletionDataGenerator);
					}
					// String literals
					else if (idExpr.Format == LiteralFormat.StringLiteral || idExpr.Format == LiteralFormat.VerbatimStringLiteral)
					{
						StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, DontAddInitProperty: true);
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
			bool isVariableInstance = false,
			ResolveResult resultParent = null)
		{
			bool dontAddInit = false;
			if (mrr.MemberBaseTypes != null)
				foreach (var i in mrr.MemberBaseTypes)
				{
					if (i is StaticTypeResult || i is ExpressionResult)
						dontAddInit = true;

					BuildCompletionData(i, currentlyScopedBlock,
						(mrr.ResolvedMember is DVariable && (mrr.ResolvedMember as DVariable).IsAlias) ?
							isVariableInstance : true, mrr); // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
				}

			if (mrr.ResultBase == null)
				StaticTypePropertyProvider.AddGenericProperties(mrr, CompletionDataGenerator, mrr.ResolvedMember, DontAddInitProperty: dontAddInit);

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

					CompletionDataGenerator.Add(modNameParts[tr.AlreadyTypedModuleNameParts], PathOverride:packageDir);
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
								if(dn is DMethod && dn.ContainsPropertyAttribute())
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
					if(i is DEnumValue)
						CompletionDataGenerator.Add(i);
			}
		}
	}
}
