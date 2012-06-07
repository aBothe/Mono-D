using System;
using System.Collections.Generic;
using System.Diagnostics;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver.TypeResolution;
using System.Linq;
using D_Parser.Completion;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// Contains resolution results of methods.
	/// </summary>
	public class UFCSCache
	{
		#region Properties
		public readonly Dictionary<DMethod, ResolveResult> CachedMethods = new Dictionary<DMethod, ResolveResult>();
		/// <summary>
		/// Returns time span needed to resolve all first parameters. Seconds.
		/// </summary>
		public TimeSpan CachingDuration { get; private set; }
		#endregion

		public void Clear()
		{
			CachedMethods.Clear();
		}

		public void Update(ParseCacheList pcList, ParseCache subCacheToUpdate=null)
		{
			var sw = new Stopwatch();
			sw.Start();

			var ctxt = new ResolverContextStack(pcList, new ResolverContext()) { ContextIndependentOptions = ResolutionOptions.StopAfterFirstOverloads };

			// Enum through all modules of the parse cache
			if (subCacheToUpdate != null)
				foreach (var module in subCacheToUpdate)
					CacheModuleMethods(module, ctxt);
			else
				foreach (var pc in pcList)
					foreach (var module in pc)
						CacheModuleMethods(module,ctxt);

			sw.Stop();
			CachingDuration = sw.Elapsed;
		}

		public void CacheModuleMethods(IAbstractSyntaxTree module, IEditorData ed)
		{
			var ctxt = new ResolverContextStack(ed.ParseCache, new ResolverContext()) { ContextIndependentOptions = ResolutionOptions.StopAfterFirstOverloads };

			CacheModuleMethods(module,ctxt);
		}

		public void CacheModuleMethods(IAbstractSyntaxTree module,ResolverContextStack ctxt)
		{
			if (module != null)
				foreach (var n in module)
				{
					var dm = n as DMethod;

					// UFCS only allows free function that contain at least one parameter
					if (dm == null || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					ctxt.ScopedBlock = dm;
					ctxt.ScopedStatement = null;

					var firstParam = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);

					if (firstParam != null && firstParam.Length != 0)
						CachedMethods[dm] = firstParam[0];
				}
		}

		/// <summary>
		/// Cleans the cache from items related to the passed syntax tree.
		/// Used for incremental update.
		/// </summary>
		public void RemoveModuleItems(IAbstractSyntaxTree ast)
		{
			var remList = new List<DMethod>();

			foreach (var kv in CachedMethods)
				if (kv.Key.NodeRoot == ast)
					remList.Add(kv.Key);

			foreach (var i in remList)
				CachedMethods.Remove(i);
		}

		public IEnumerable<DMethod> FindFitting(ResolverContextStack ctxt, CodeLocation currentLocation,ResolveResult firstArgument,string nameFilter=null)
		{
			var preMatchList = new List<DMethod>();

			bool dontUseNameFilter = nameFilter == null;

			foreach (var kv in CachedMethods)
			{
				// First test if arg is matching the parameter
				if ((dontUseNameFilter || kv.Key.Name == nameFilter) &&
					ResultComparer.IsImplicitlyConvertible(firstArgument, kv.Value, ctxt))
					preMatchList.Add(kv.Key);
			}

			// Then filter out methods which cannot be accessed in the current context 
			// (like when the method is defined in a module that has not been imported)
			var mv = new MatchFilterVisitor<DMethod>(ctxt) {
				rawList=preMatchList
			};

			mv.IterateThroughScopeLayers(currentLocation);

			return mv.filteredList;
		}
	}
}
