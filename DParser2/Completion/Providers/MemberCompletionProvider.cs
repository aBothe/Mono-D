using System.Collections.Generic;
using D_Parser.Completion.Providers;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Completion
{
	public class MemberCompletionProvider : AbstractCompletionProvider
	{
		public PostfixExpression_Access AccessExpression;
		public IStatement ScopedStatement;
		public IBlockNode ScopedBlock;

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
			var ctxt = ResolverContextStack.Create(Editor);
			var ex = AccessExpression.AccessExpression == null ? AccessExpression.PostfixForeExpression : AccessExpression;

			var r = Evaluation.EvaluateType(ex, ctxt);

			if (r == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
				return;

			BuildCompletionData(r, ScopedBlock);

			if(Editor.Options.ShowUFCSItems)
				UFCSCompletionProvider.Generate(r, ctxt, Editor, CompletionDataGenerator);
		}

		void BuildCompletionData(
			AbstractType rr,
			IBlockNode currentlyScopedBlock,
			bool isVariableInstance = false,
			AbstractType resultParent = null)
		{
			if (rr == null)
				return;

			if(rr.DeclarationOrExpressionBase is ITypeDeclaration)
				isVariableInstance |= (rr.DeclarationOrExpressionBase as ITypeDeclaration).ExpressesVariableAccess;

			if (rr is MemberSymbol)
				BuildCompletionData((MemberSymbol)rr, currentlyScopedBlock, isVariableInstance);

			// A module path has been typed
			else if (!isVariableInstance && rr is ModuleSymbol)
				BuildCompletionData((ModuleSymbol)rr);

			else if (rr is PackageSymbol)
				BuildCompletionData((PackageSymbol)rr);

			#region A type was referenced directly
			else if (rr is EnumType)
			{
				var en = (EnumType)rr;

				foreach (var e in en.Definition)
					CompletionDataGenerator.Add(e);
			}

			else if (rr is TemplateIntermediateType)
			{
				var tr = (TemplateIntermediateType)rr;
				var vis = ItemVisibility.All;

				bool HasSameAncestor = HaveSameAncestors(currentlyScopedBlock, tr.Definition);
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
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, tr.Definition);

				StaticTypePropertyProvider.AddClassTypeProperties(CompletionDataGenerator, tr.Definition);
			}
			#endregion

			#region Things like int. or char.
			else if (rr is PrimitiveType)
			{
				var primType = (PrimitiveType)rr;

				if (resultParent == null)
					StaticTypePropertyProvider.AddGenericProperties(rr, CompletionDataGenerator, null);

				if (primType.TypeToken > 0)
				{
					// Determine whether float by the var's base type
					bool isFloat = DTokens.BasicTypes_FloatingPoint[primType.TypeToken];

					// Float implies integral props
					if (DTokens.BasicTypes_Integral[primType.TypeToken] || isFloat)
						StaticTypePropertyProvider.AddIntegralTypeProperties(primType.TypeToken, rr, CompletionDataGenerator, null, isFloat);

					if (isFloat)
						StaticTypePropertyProvider.AddFloatingTypeProperties(primType.TypeToken, rr, CompletionDataGenerator, null);
				}
			}
			#endregion

			else if (rr is PointerType)
			{
				var pt = (PointerType)rr;
				if (!(pt.Base is PrimitiveType && pt.Base.DeclarationOrExpressionBase is PointerDecl))
					BuildCompletionData(pt.Base, currentlyScopedBlock, true, pt);
			}

			else if (rr is AssocArrayType)
			{
				var ar = (AssocArrayType)rr;

				if (ar is ArrayType)
					StaticTypePropertyProvider.AddArrayProperties(rr, CompletionDataGenerator, ar.DeclarationOrExpressionBase as ArrayDecl);
				else
					StaticTypePropertyProvider.AddAssocArrayProperties(rr, CompletionDataGenerator, ar.DeclarationOrExpressionBase as ArrayDecl);
			}
		}

		void BuildCompletionData(MemberSymbol mrr, IBlockNode currentlyScopedBlock, bool isVariableInstance = false)
		{
			if (mrr.Base != null)
					BuildCompletionData(mrr.Base, 
						currentlyScopedBlock,
						mrr is AliasedType ? isVariableInstance : true, // True if we obviously have a variable handled here. Otherwise depends on the samely-named parameter..
						mrr); 
			else
				StaticTypePropertyProvider.AddGenericProperties(mrr, CompletionDataGenerator, mrr.Definition, false);
		}

		void BuildCompletionData(PackageSymbol mpr)
		{
			foreach (var kv in mpr.Package.Packages)
				CompletionDataGenerator.Add(kv.Key);

			foreach (var kv in mpr.Package.Modules)
				CompletionDataGenerator.Add(kv.Key, kv.Value);
		}

		void BuildCompletionData(ModuleSymbol tr)
		{
			foreach (var i in tr.Definition)
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

		void BuildCompletionData(UserDefinedType tr, ItemVisibility visMod)
		{
			var n = tr.Definition;
			if (n is DClassLike) // Add public static members of the class and including all base classes
			{
				var propertyMethodsToIgnore = new List<string>();

				var curlevel = tr;
				var tvisMod = visMod;
				while (curlevel != null)
				{
					foreach (var i in curlevel.Definition as IBlockNode)
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
					curlevel = curlevel.Base as UserDefinedType;

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
