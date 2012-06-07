using System;
using System.Collections.Generic;
using System.IO;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using System.Threading;
using D_Parser.Resolver;

namespace D_Parser.Misc
{
	/// <summary>
	/// Stores syntax trees while regarding their package hierarchy.
	/// </summary>
	public class ParseCache : IEnumerable<IAbstractSyntaxTree>, IEnumerable<ModulePackage>
	{
		#region Properties
		public bool IsParsing { get { return parseThread != null && parseThread.IsAlive; } }
		Thread parseThread;

		public RootPackage Root = new RootPackage ();

		public bool EnableUfcsCaching = true;
		/// <summary>
		/// The cache which holds resolution results of the global scope's methods' first parameters - used to increase completion performance
		/// </summary>
		public readonly UFCSCache UfcsCache = new UFCSCache();
		/// <summary>
		/// If a parse directory is relative, like ../ or similar, use this path as base path
		/// </summary>
		public string FallbackPath;
		public List<string> ParsedDirectories = new List<string> ();

		public Exception LastParseException { get; private set; }

		public bool IsObjectClassDefined
		{
			get { return ObjectClass != null; }
		}

		/// <summary>
		/// To improve resolution performance, the object class that can be defined only once will be stored over here.
		/// </summary>
		public DClassLike ObjectClass
		{
			get;
			private set;
		}

		/// <summary>
		/// See <see cref="ObjectClass"/>
		/// </summary>
		public TypeResult ObjectClassResult
		{
			get;
			set;
		}
		#endregion

		#region Parsing management
		public delegate void ParseFinishedHandler(ParsePerformanceData[] PerformanceData);
		public event ParseFinishedHandler FinishedParsing;
		public event Action FinishedUfcsCaching;

		public void BeginParse ()
		{
			BeginParse (ParsedDirectories,FallbackPath);
		}

		/// <summary>
		/// Parses all directories and updates the cache contents
		/// </summary>
		public void BeginParse (IEnumerable<string> directoriesToParse,string fallbackAbsolutePath)
		{
			var performanceLogs = new List<ParsePerformanceData> ();

			AbortParsing();

			FallbackPath = fallbackAbsolutePath;

			if (directoriesToParse == null)
			{
				ParsedDirectories.Clear();

				if(FinishedParsing!=null)
					FinishedParsing(null);
				return;
			}

			parseThread = new Thread(parseDg);

			parseThread.IsBackground = true;
			parseThread.Start(new Tuple<IEnumerable<string>,List<ParsePerformanceData>>(directoriesToParse, performanceLogs));
		}

		public void WaitForParserFinish()
		{
			if (parseThread != null && parseThread.IsAlive)
				parseThread.Join();
		}

		public void AbortParsing()
		{
			if (parseThread != null && parseThread.IsAlive)
				parseThread.Abort();
		}

		void parseDg(object o)
		{
			var tup = (Tuple<IEnumerable<string>, List<ParsePerformanceData>>)o;

			var parsedDirs = new List<string>();
			var newRoot = new RootPackage();
			foreach (var dir in tup.Item1)
			{
				parsedDirs.Add(dir);

				var dir_abs = dir;
				if (!Path.IsPathRooted(dir))
					dir_abs = Path.Combine(FallbackPath, dir_abs);

				var ppd = ThreadedDirectoryParser.Parse(dir_abs, newRoot);

				if (ppd != null)
					tup.Item2.Add(ppd);
			}

			UfcsCache.Clear();
			ParsedDirectories = parsedDirs;
			Root = newRoot;

			// For performance boost, pre-resolve the object class
			HandleObjectModule(GetModule("object"));			

			if (FinishedParsing!=null)
				FinishedParsing(tup.Item2.ToArray());

			if (EnableUfcsCaching)
			{
				UfcsCache.Update(ParseCacheList.Create(this));

				if (FinishedUfcsCaching != null)
					FinishedUfcsCaching();
			}
		}
		
		public bool UpdateRequired (string[] paths)
		{
			if (paths == null)
				return false;
			
			// If current dir count != the new dir count
			bool cacheUpdateRequired = paths.Length != ParsedDirectories.Count;

			// If there's a new directory in it
			if (!cacheUpdateRequired)
				foreach (var path in paths)
					if (!ParsedDirectories.Contains (path)) {
						cacheUpdateRequired = true;
						break;
					}

			if (!cacheUpdateRequired && paths.Length != 0)
				cacheUpdateRequired = 
                    Root.Modules.Count == 0 && 
                    Root.Packages.Count == 0;
			
			return cacheUpdateRequired;
		}
		
		public void Clear (bool parseDirectories=false)
		{
			Root = null;
			if (parseDirectories)
				ParsedDirectories = null;

			Root = new RootPackage ();
		}

		void HandleObjectModule(IAbstractSyntaxTree objModule)
		{
			if (objModule != null)
				foreach (var m in objModule)
					if (m is DClassLike && m.Name == "Object")
					{
						ObjectClass = (DClassLike)m;

						ObjectClassResult = new TypeResult
						{
							DeclarationOrExpressionBase = new IdentifierDeclaration("Object"),
							Node = m
						};
						break;
					}
		}
		#endregion

		#region Tree management
		/// <summary>
		/// Use this method to add a syntax tree to the parse cache.
		/// Equally-named trees will be overwritten. 
		/// </summary>
		public void AddOrUpdate (IAbstractSyntaxTree ast)
		{
			if (ast == null)
				return;

			var packName = ModuleNameHelper.ExtractPackageName (ast.ModuleName);

			if (string.IsNullOrEmpty (packName)) {
				Root.Modules [ast.ModuleName] = ast;

				if (ast.ModuleName == "object")
					HandleObjectModule(ast);
				return;
			}

			var pack = Root.GetOrCreateSubPackage(packName, true);

			pack.Modules[ModuleNameHelper.ExtractModuleName(ast.ModuleName)] = ast;
		}

		/// <summary>
		/// Returns null if no module was found.
		/// </summary>
		public IAbstractSyntaxTree GetModule (string moduleName)
		{
			var packName = ModuleNameHelper.ExtractPackageName (moduleName);

			var pack = Root.GetOrCreateSubPackage (packName, false);

			if (pack != null) {
				IAbstractSyntaxTree ret = null;
				if (pack.Modules.TryGetValue (ModuleNameHelper.ExtractModuleName (moduleName), out ret))
					return ret;
			}

			return null;
		}

		public IAbstractSyntaxTree GetModuleByFileName (string file, string baseDirectory)
		{
			return GetModule (DModule.GetModuleName (baseDirectory, file));
		}

		public IAbstractSyntaxTree this[string ModuleName]
		{
			get
			{
				return GetModule(ModuleName);
			}
		}
		#endregion

		public IEnumerator<IAbstractSyntaxTree> GetEnumerator()
		{
			return Root.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator<ModulePackage> IEnumerable<ModulePackage>.GetEnumerator()
		{
			return ((IEnumerable<ModulePackage>)Root).GetEnumerator();
		}
	}

	public class ParsePerformanceData
	{
		public string BaseDirectory;
		public int AmountFiles = 0;

		/// <summary>
		/// Duration (in seconds)
		/// </summary>
		public double TotalDuration = 0.0;

		public double FileDuration {
			get {
				if (AmountFiles > 0)
					return TotalDuration / AmountFiles;
				return 0;
			}
		}
	}
}
