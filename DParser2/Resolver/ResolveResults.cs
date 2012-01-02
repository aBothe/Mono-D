using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver
{
	public abstract class ResolveResult
	{
		/// <summary>
		/// If the entire resolution took more than one level of type searching, this field represents the resolution base that was used to find the current items.
		/// </summary>
		public ResolveResult ResultBase;

		/// <summary>
		/// The type declaration that has been used as the base for our type resolution.
		/// </summary>
		public ITypeDeclaration TypeDeclarationBase;

		public static string GetResolveResultString(ResolveResult rr)
		{
			if (rr is MemberResult)
				return DNode.GetNodePath((rr as MemberResult).ResolvedMember, true);
			if (rr is TypeResult)
				return DNode.GetNodePath((rr as TypeResult).ResolvedTypeDefinition, true);
			if (rr is StaticTypeResult)
				return (rr as StaticTypeResult).TypeDeclarationBase.ToString();
			if (rr is ModuleResult)
			{
				var mrr = rr as ModuleResult;

				var parts = mrr.ResolvedModule.ModuleName.Split('.');
				var ret = "";
				for (int i = 0; i < mrr.AlreadyTypedModuleNameParts; i++)
					ret += parts[i] + '.';

				return ret.TrimEnd('.');
			}

			return null;
		}
	}

	public class MemberResult : ResolveResult
	{
		public INode ResolvedMember;

		/// <summary>
		/// Usually there should be only one resolved member type.
		/// If the origin of ResolvedMember seems to be unclear (if there are multiple same-named types), there will be two or more items
		/// </summary>
		public ResolveResult[] MemberBaseTypes;

		public override string ToString()
		{
			return ResolvedMember.ToString();
		}
	}

	/// <summary>
	/// Encapsules basic type declarations like int, bool, void[], byte*, immutable(char)[] etc.
	/// </summary>
	public class StaticTypeResult : ResolveResult
	{
		public int BaseTypeToken;

		public override string ToString()
		{
			return TypeDeclarationBase.ToString();
		}
	}

	/// <summary>
	/// Keeps raw expressions like (1+2)
	/// </summary>
	public class ExpressionResult : ResolveResult
	{
		public IExpression Expression;

		public override string ToString()
		{
			return Expression.ToString();
		}
	}

	public class ModuleResult : ResolveResult
	{
		public IAbstractSyntaxTree ResolvedModule;
		public bool IsOnlyModuleNamePartTyped()
		{
			var modNameParts = ResolvedModule.ModuleName.Split('.');
			return AlreadyTypedModuleNameParts != modNameParts.Length;
		}

		public int AlreadyTypedModuleNameParts = 0;

		public override string ToString()
		{
			return ResolvedModule.ToString();
		}
	}

	/// <summary>
	/// Keeps class-like definitions
	/// </summary>
	public class TypeResult : ResolveResult
	{
		public IBlockNode ResolvedTypeDefinition;

		/// <summary>
		/// Only will have two or more items if there are multiple definitions of its base class - theoretically, this should be marked as a precompile error then.
		/// </summary>
		public TypeResult[] BaseClass;
		public TypeResult[] ImplementedInterfaces;

		public override string ToString()
		{
			return ResolvedTypeDefinition.ToString();
		}
	}
}
