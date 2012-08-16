using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System.Threading;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Refactoring
{
	/// <summary>
	/// Analyses an AST and returns all Syntax Regions that represent a type
	/// </summary>
	public class TypeReferenceFinder : DeepASTVisitor
	{
		readonly Dictionary<IBlockNode, SortedDictionary<string, INode>> TypeCache = new Dictionary<IBlockNode, SortedDictionary<string, INode>>();

		/// <summary>
		/// Contains the current scope as well as the syntax region
		/// </summary>
		readonly List<ISyntaxRegion> q = new List<ISyntaxRegion>();
		int queueCount;
		int curQueueOffset = 0;
		object _lockObject = new Object();

		/// <summary>
		/// Stores the block and the count position how many syntax regions are related to that block.
		/// Is kept synchronized with the q stack.
		/// </summary>
		readonly SortedDictionary<int, IBlockNode> scopes = new SortedDictionary<int, IBlockNode>();
		readonly SortedDictionary<int, IStatement> scopes_Stmts = new SortedDictionary<int, IStatement>();

		IBlockNode curScope = null;

		readonly TypeReferencesResult result = new TypeReferencesResult();
		readonly ParseCacheList sharedParseCache;

		private TypeReferenceFinder(ParseCacheList sharedCache)
		{
			this.sharedParseCache = sharedCache;
		}

		public static TypeReferencesResult Scan(IAbstractSyntaxTree ast, ParseCacheList pcl)
		{
			var typeRefFinder = new TypeReferenceFinder(pcl);

			return typeRefFinder.result; // TODO: Implement the whole thing

			// Enum all identifiers
			typeRefFinder.S(ast);

			// Crawl through all remaining expressions by evaluating their types and check if they're actual type references.
			typeRefFinder.queueCount = typeRefFinder.q.Count;
			typeRefFinder.ResolveAllIdentifiers();

			return typeRefFinder.result;
		}

		void CreateDeeperLevelCache(IBlockNode bn)
		{
			SortedDictionary<string, INode> dd=null;

			if(!TypeCache.TryGetValue(bn, out dd))
				dd = TypeCache[bn] = new SortedDictionary<string,INode>();

			// Set the parent to null to crawl through current level only. Imports/Mixins etc. will be handled though.
			var parentBackup = bn.Parent;
			bn.Parent = null;

			var vis = ItemEnumeration.EnumAllAvailableMembers(bn, null, bn.EndLocation, sharedParseCache, MemberFilter.Types);

			if (vis != null)
				foreach (var n in vis)
				{
					if (!string.IsNullOrEmpty(n.Name))
						dd[n.Name] = n;
				}

			bn.Parent = parentBackup;
		}

		#region Preparation list generation
		protected override void OnScopeChanged(IBlockNode scopedBlock)
		{
			CreateDeeperLevelCache(curScope = scopes[q.Count] = scopedBlock);
		}

		protected override void Handle(ISyntaxRegion o)
		{
			if (o is IdentifierDeclaration || o is TemplateInstanceExpression)
			{
				if (DoPrimaryIdCheck(ExtractId(o)))
					result.TypeMatches.Add(o);
			}
			/*else if (o is IdentifierExpression)
			{
				var id = (IdentifierExpression)o;

				if ((string)id.Value != searchId)
					return;

				if (resolvedSymbol == null)
					resolvedSymbol = Evaluation.EvaluateType(id, ctxt) as DSymbol;
			}

			if (handleSingleIdentifiersOnly)
				return;

			if (o is PostfixExpression_Access)
			{
				var acc = (PostfixExpression_Access)o;

				if ((acc.AccessExpression is IdentifierExpression &&
				(string)((IdentifierExpression)acc.AccessExpression).Value != searchId) ||
				(acc.AccessExpression is TemplateInstanceExpression &&
				(string)((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id != searchId))
				{
					Handle(acc.PostfixForeExpression, null);
					return;
				}
				else if (acc.AccessExpression is NewExpression)
				{
					var nex = (NewExpression)acc.AccessExpression;

					if ((nex.Type is IdentifierDeclaration &&
						((IdentifierDeclaration)nex.Type).Id != searchId) ||
						(nex.Type is TemplateInstanceExpression &&
						(string)((TemplateInstanceExpression)acc.AccessExpression).TemplateIdentifier.Id != searchId))
					{
						Handle(acc.PostfixForeExpression, null);
						return;
					}
					// Are there other types to test for?
				}

				var s = resolvedSymbol ?? Evaluation.EvaluateType(acc, ctxt) as DerivedDataType;

				if (s is DSymbol)
				{
					if (((DSymbol)s).Definition == symbol)
						l.Add(acc.AccessExpression);
				}
				else if (s == null || !(s.Base is DSymbol))
					return;

				// Scan down for other possible symbols
				Handle(acc.PostfixForeExpression, s.Base as DSymbol);
				return;
			}

			q.Add(o);*/
		}
		#endregion

		/// <summary>
		/// Returns true if a type called 'id' exists in the current scope
		/// </summary>
		bool DoPrimaryIdCheck(string id)
		{
			var bn = curScope;

			while (bn != null)
				foreach (var m in bn)
				{
					if (m.Name == id)
						return true;

					if (bn.Parent == null || bn.Parent == bn)
						return bn.Name == id;

					bn = bn.Parent as IBlockNode;
				}
			
			return false;
		}

		List<IExpression> DoPrimaryIdCheck(PostfixExpression_Access acc)
		{
			var r = new List<IExpression>();
			while(acc != null){
				if (DoPrimaryIdCheck(ExtractId(acc)))
					r.Add(acc);

				// Scan down the access expression for other, deeper expressions
				if (acc.PostfixForeExpression is PostfixExpression_Access)
					acc = (PostfixExpression_Access)acc.PostfixForeExpression;
				else
				{
					if (DoPrimaryIdCheck(ExtractId(acc.PostfixForeExpression)))
						r.Add(acc.PostfixForeExpression);
					break;
				}
			}
			return r;
		}

		public static string ExtractId(ISyntaxRegion o)
		{
			if (o is IdentifierDeclaration)
				return ((IdentifierDeclaration)o).Id;
			else if (o is IdentifierExpression)
				return (string)((IdentifierExpression)o).Value;
			else if (o is PostfixExpression_Access)
				return ExtractId(((PostfixExpression_Access)o).AccessExpression);
			else if (o is TemplateInstanceExpression)
				return ((TemplateInstanceExpression)o).TemplateIdentifier.Id;
			else if (o is NewExpression)
				return ExtractId(((NewExpression)o).Type);
			return null;
		}

		#region Threaded id analysis
		void ResolveAllIdentifiers()
		{
			var threads = new Thread[ThreadedDirectoryParser.numThreads];
			for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
			{
				var th = threads[i] = new Thread(_th)
				{
					IsBackground = true,
					Priority = ThreadPriority.Lowest,
					Name = "Type reference analysis thread #" + i
				};
				th.Start(sharedParseCache);
			}

			for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
				if (threads[i].IsAlive)
					threads[i].Join(10000);
		}

		void _th(object pcl_shared)
		{
			var pcl = (ParseCacheList)pcl_shared;
			var ctxt = new ResolverContextStack(pcl, new ResolverContext());

			// Make it as most performing as possible by avoiding unnecessary base types. 
			// Aliases should be analyzed deeper though.
			ctxt.CurrentContext.ContextDependentOptions |= 
				ResolutionOptions.StopAfterFirstOverloads | 
				ResolutionOptions.DontResolveBaseClasses | 
				ResolutionOptions.DontResolveBaseTypes | //TODO: Exactly find out which option can be enabled here. Resolving variables' types is needed sometimes - but only, when highlighting a variable reference is wanted explicitly.
				ResolutionOptions.NoTemplateParameterDeduction | 
				ResolutionOptions.ReturnMethodReferencesOnly;

			IBlockNode bn = null;
			IStatement stmt = null;
			ISyntaxRegion sr = null;
			int i = 0;
			int k = 0;

			while (curQueueOffset < queueCount)
			{
				// Avoid race condition runtime errors
				lock (_lockObject)
				{
					i = curQueueOffset;
					curQueueOffset++;
				}

				// Try to get an updated scope
				for (k = i; k > 0; k--)
					if (scopes.TryGetValue(k, out bn))
					{
						ctxt.CurrentContext.ScopedBlock = bn;
						break;
					}
				for (k = i; k > 0; k--)
					if (scopes_Stmts.TryGetValue(k, out stmt))
					{
						ctxt.CurrentContext.ScopedStatement = stmt;
						break;
					}

				// Resolve gotten syntax object
				sr = q[i];

				if (sr is PostfixExpression_Access)
					HandleAccessExpressions((PostfixExpression_Access)sr, ctxt);
				else
				{
					AbstractType t = null;
					if (sr is IExpression)
						t = DResolver.StripAliasSymbol(Evaluation.EvaluateType((IExpression)sr, ctxt));
					else if (sr is ITypeDeclaration)
						t = DResolver.StripAliasSymbol(TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)sr, ctxt));

					// Enter into the result lists
					//HandleResult(t, sr);
				}
			}
		}

		AbstractType HandleAccessExpressions(PostfixExpression_Access acc, ResolverContextStack ctxt)
		{
			AbstractType pfType = null;
			if (acc.PostfixForeExpression is PostfixExpression_Access)
				pfType = HandleAccessExpressions((PostfixExpression_Access)acc.PostfixForeExpression, ctxt);
			else
			{
				pfType = DResolver.StripAliasSymbol(Evaluation.EvaluateType(acc.PostfixForeExpression, ctxt));

				if (acc.PostfixForeExpression is IdentifierExpression ||
					acc.PostfixForeExpression is TemplateInstanceExpression ||
					acc.PostfixForeExpression is PostfixExpression_Access) return null;
				//HandleResult(pfType, acc.PostfixForeExpression);
			}
			
			bool ufcs=false;
			var accessedMembers = Evaluation.GetAccessedOverloads(acc, ctxt, out ufcs, pfType);
			ctxt.CheckForSingleResult(accessedMembers, acc);

			if (accessedMembers != null && accessedMembers.Length != 0)
			{
				//HandleResult(accessedMembers[0], acc);
				return accessedMembers[0];
			}
			else
				//HandleResult(null, acc);

			return null;
		}

		#endregion
	}

	public class TypeReferencesResult
	{
		public List<ISyntaxRegion> TypeMatches = new List<ISyntaxRegion>();
	}
}
