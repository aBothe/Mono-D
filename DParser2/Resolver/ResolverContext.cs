using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace D_Parser.Resolver
{
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public IEnumerable<IAbstractSyntaxTree> ParseCache;
		public IEnumerable<IAbstractSyntaxTree> ImportCache;
		public bool ResolveBaseTypes = true;
		public bool ResolveAliases = true;

		Dictionary<object, Dictionary<string, ResolveResult[]>> resolvedTypes = new Dictionary<object, Dictionary<string, ResolveResult[]>>();

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;
			ParseCache = other.ParseCache;
			ImportCache = other.ImportCache;

			ResolveBaseTypes = other.ResolveBaseTypes;
			ResolveAliases = other.ResolveAliases;

			resolvedTypes = other.resolvedTypes;
		}

		/// <summary>
		/// Stores scoped-block dependent type dictionaries, which store all types that were already resolved once
		/// </summary>
		public Dictionary<object, Dictionary<string, ResolveResult[]>> ResolvedTypes
		{
			get { return resolvedTypes; }
		}

		public void TryAddResults(string TypeDeclarationString, ResolveResult[] NodeMatches, IBlockNode ScopedType = null)
		{
			if (ScopedType == null)
				ScopedType = ScopedBlock;

			Dictionary<string, ResolveResult[]> subDict = null;

			if (!ResolvedTypes.TryGetValue(ScopedType, out subDict))
				ResolvedTypes.Add(ScopedType, subDict = new Dictionary<string, ResolveResult[]>());

			if (!subDict.ContainsKey(TypeDeclarationString))
				subDict.Add(TypeDeclarationString, NodeMatches);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="TypeDeclarationString"></param>
		/// <param name="NodeMatches"></param>
		/// <param name="ScopedType">If null, ScopedBlock variable will be assumed</param>
		/// <returns></returns>
		public bool TryGetAlreadyResolvedType(string TypeDeclarationString, out ResolveResult[] NodeMatches, object ScopedType = null)
		{
			if (ScopedType == null)
				ScopedType = ScopedBlock;

			Dictionary<string, ResolveResult[]> subDict = null;

			if (ScopedType != null && !ResolvedTypes.TryGetValue(ScopedType, out subDict))
			{
				NodeMatches = null;
				return false;
			}

			if (subDict != null)
				return subDict.TryGetValue(TypeDeclarationString, out NodeMatches);

			NodeMatches = null;
			return false;
		}
	}

}
