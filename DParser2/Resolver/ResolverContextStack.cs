using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Completion;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public class ResolverContextStack
	{
		#region Properties
		protected Stack<ResolverContext> stack = new Stack<ResolverContext>();

		public ParseCacheList ParseCache = new ParseCacheList();

		public IBlockNode ScopedBlock
		{
			get {
				if (stack.Count<1)
					return null;

				return CurrentContext.ScopedBlock;
			}
			set
			{
				if (stack.Count > 0)
					CurrentContext.ScopedBlock = value;
			}
		}

		public IStatement ScopedStatement
		{
			get
			{
				if (stack.Count < 1)
					return null;

				return CurrentContext.ScopedStatement;
			}
			set
			{
				if (stack.Count > 0)
					CurrentContext.ScopedStatement = value;
			}
		}

		Dictionary<object, Dictionary<string, ResolveResult[]>> resolvedTypes = new Dictionary<object, Dictionary<string, ResolveResult[]>>();

		/// <summary>
		/// Stores scoped-block dependent type dictionaries, which store all types that were already resolved once
		/// </summary>
		public Dictionary<object, Dictionary<string, ResolveResult[]>> ResolvedTypes
		{
			get { return resolvedTypes; }
		}

		public ResolverContext CurrentContext
		{
			get {
				return stack.Peek();
			}
		}
		#endregion

		public static ResolverContextStack Create(IEditorData editor)
		{
			IStatement stmt = null;
			return new ResolverContextStack(editor.ParseCache, new ResolverContext
			{
				ScopedBlock = DResolver.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation, out stmt),
				ScopedStatement = stmt
			});
		}

		public ResolverContextStack(ParseCacheList ParseCache,	ResolverContext initialContext)
		{
			this.ParseCache = ParseCache;
			
			stack.Push(initialContext);
		}

		public ResolverContext Pop()
		{
			if(stack.Count>0)
				return stack.Pop();

			return null;
		}

		public ResolverContext PushNewScope(IBlockNode scope)
		{
			var ctxtOverride = new ResolverContext();
			ctxtOverride.ApplyFrom(CurrentContext);
			ctxtOverride.ScopedBlock = scope;
			ctxtOverride.ScopedStatement = null;

			stack.Push(ctxtOverride);

			return ctxtOverride;
		}

		object GetMostFittingBlock()
		{
			if (CurrentContext == null)
				return null;

			if (CurrentContext.ScopedStatement != null)
			{
				var r = CurrentContext.ScopedStatement;

				while (r != null)
				{
					if (r is BlockStatement)
						return r;
					else
						r = r.Parent;
				}
			}
			
			return CurrentContext.ScopedBlock;
		}

		public void TryAddResults(string TypeDeclarationString, ResolveResult[] NodeMatches)
		{
			var ScopedType = GetMostFittingBlock();

			Dictionary<string, ResolveResult[]> subDict = null;

			if (!resolvedTypes.TryGetValue(ScopedType, out subDict))
				resolvedTypes.Add(ScopedType, subDict = new Dictionary<string, ResolveResult[]>());

			if (!subDict.ContainsKey(TypeDeclarationString))
				subDict.Add(TypeDeclarationString, NodeMatches);
		}

		public bool TryGetAlreadyResolvedType(string TypeDeclarationString, out ResolveResult[] NodeMatches)
		{
			var ScopedType = GetMostFittingBlock();

			Dictionary<string, ResolveResult[]> subDict = null;

			if (ScopedType != null && !resolvedTypes.TryGetValue(ScopedType, out subDict))
			{
				NodeMatches = null;
				return false;
			}

			if (subDict != null)
				return subDict.TryGetValue(TypeDeclarationString, out NodeMatches);

			NodeMatches = null;
			return false;
		}

		/// <summary>
		/// Clones the stack object and also clones the highest item on the context stack (only!)
		/// </summary>
		public ResolverContextStack Clone()
		{
			var rc=new ResolverContext();
			rc.ApplyFrom(CurrentContext);

			return new ResolverContextStack(ParseCache, rc);
		}
	}
}
