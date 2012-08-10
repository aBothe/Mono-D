using System;
using System.Collections.Generic;
using System.Diagnostics;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver.TypeResolution;
using System.Linq;
using D_Parser.Completion;
using System.Threading;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// Contains resolution results of methods.
	/// </summary>
	public class UFCSCache
	{
		#region Properties
		public bool IsProcessing { get; private set; }

		Stack<DMethod> queue = new Stack<DMethod>();
		public readonly Dictionary<DMethod, AbstractType> CachedMethods = new Dictionary<DMethod, AbstractType>();
		/// <summary>
		/// Returns time span needed to resolve all first parameters.
		/// </summary>
		public TimeSpan CachingDuration { get; private set; }
		#endregion

		public void Clear()
		{
			if (!IsProcessing)
				CachedMethods.Clear();
		}

		/// <summary>
		/// Returns false if cache is already updating.
		/// </summary>
		public bool Update(ParseCacheList pcList, ParseCache subCacheToUpdate = null)
		{
			if (IsProcessing)
				return false;

			try
			{
				IsProcessing = true;

				var ctxt = new ResolverContextStack(pcList, new ResolverContext()) { ContextIndependentOptions = ResolutionOptions.StopAfterFirstOverloads };

				queue.Clear();

				// Prepare queue
				if (subCacheToUpdate != null)
					foreach (var module in subCacheToUpdate)
						PrepareQueue(module);
				else
					foreach (var pc in pcList)
						foreach (var module in pc)
							PrepareQueue(module);

				var sw = new Stopwatch();
				sw.Start();

				var threads = new Thread[ThreadedDirectoryParser.numThreads];
				for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
				{
					var th = threads[i] = new Thread(parseThread)
					{
						IsBackground = true,
						Priority = ThreadPriority.Lowest,
						Name = "UFCS Analysis thread #" + i
					};
					th.Start(pcList);
				}

				for (int i = 0; i < ThreadedDirectoryParser.numThreads; i++)
					if (threads[i].IsAlive)
						threads[i].Join(10000);

				sw.Stop();
				CachingDuration = sw.Elapsed;
			}
			finally
			{
				IsProcessing = false;
			}
			return true;
		}

		void PrepareQueue(IAbstractSyntaxTree module)
		{
			if (module != null)
				foreach (var n in module)
				{
					var dm = n as DMethod;

					// UFCS only allows free function that contain at least one parameter
					if (dm == null || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					queue.Push(dm);
				}
		}

		void parseThread(object pcl_shared)
		{
			DMethod dm = null;
			var pcl = (ParseCacheList)pcl_shared;
			var ctxt = new ResolverContextStack(pcl, new ResolverContext());
			ctxt.ContextIndependentOptions |= ResolutionOptions.StopAfterFirstOverloads;

			while (queue.Count != 0)
			{
				lock (queue)
				{
					if (queue.Count == 0)
						return;

					dm = queue.Pop();
				}

				ctxt.CurrentContext.ScopedBlock = dm;

				var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);

				if (firstArg_result != null && firstArg_result.Length != 0)
					lock (CachedMethods)
						CachedMethods[dm] = firstArg_result[0];
			}
		}

		/// <summary>
		/// Cleans the cache from items related to the passed syntax tree.
		/// Used for incremental update.
		/// </summary>
		public void RemoveModuleItems(IAbstractSyntaxTree ast)
		{
			if (IsProcessing)
				return;

			var remList = new List<DMethod>();

			foreach (var kv in CachedMethods)
				if (kv.Key.NodeRoot == ast)
					remList.Add(kv.Key);

			foreach (var i in remList)
				lock (CachedMethods)
					CachedMethods.Remove(i);
		}

		public void CacheModuleMethods(IAbstractSyntaxTree ast, ResolverContextStack ctxt)
		{
			foreach (var m in ast)
				if (m is DMethod)
				{
					var dm = (DMethod)m;

					if (dm.Parameters == null || dm.Parameters.Count == 0 || dm.Parameters[0].Type == null)
						continue;

					ctxt.PushNewScope(dm);
					var firstArg_result = TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, ctxt);
					ctxt.Pop();

					if (firstArg_result != null && firstArg_result.Length != 0)
						lock (CachedMethods)
							CachedMethods[dm] = firstArg_result[0];
				}
		}

		public IEnumerable<DMethod> FindFitting(ResolverContextStack ctxt, CodeLocation currentLocation, ISemantic firstArgument, string nameFilter = null)
		{
			if (IsProcessing)
				return null;

			var preMatchList = new List<DMethod>();

			bool dontUseNameFilter = nameFilter == null;

			lock(CachedMethods)
				foreach (var kv in CachedMethods)
				{
					// First test if arg is matching the parameter
					if ((dontUseNameFilter || kv.Key.Name == nameFilter) &&
						ResultComparer.IsImplicitlyConvertible(firstArgument, kv.Value, ctxt))
						preMatchList.Add(kv.Key);
				}

			// Then filter out methods which cannot be accessed in the current context 
			// (like when the method is defined in a module that has not been imported)
			var mv = new MatchFilterVisitor<DMethod>(ctxt)
			{
				rawList = preMatchList
			};

			mv.IterateThroughScopeLayers(currentLocation);

			return mv.filteredList;
		}
	}
}
