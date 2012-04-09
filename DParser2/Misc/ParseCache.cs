using System;
using System.Collections.Generic;
using System.IO;
using D_Parser.Dom;

namespace D_Parser.Misc
{
	/// <summary>
	/// Stores syntax trees while regarding their package hierarchy.
	/// </summary>
	public class ParseCache : IEnumerable<IAbstractSyntaxTree>, IEnumerable<ModulePackage>
	{
		#region Properties
		public bool IsParsing { get; private set; }

		public RootPackage Root = new RootPackage ();
		/// <summary>
		/// If a parse directory is relative, like ../ or similar, use this path as base path
		/// </summary>
		public string FallbackPath;
		public List<string> ParsedDirectories = new List<string> ();

		public Exception LastParseException { get; private set; }
		#endregion

		#region Parsing management
		public ParsePerformanceData[] Parse ()
		{
			return Parse (ParsedDirectories,FallbackPath);
		}

		/// <summary>
		/// Parses all directories and updates the cache contents
		/// </summary>
		public ParsePerformanceData[] Parse (IEnumerable<string> directoriesToParse,string fallbackAbsolutePath)
		{
			var performanceLogs = new List<ParsePerformanceData> ();

			FallbackPath = fallbackAbsolutePath;

			if (directoriesToParse == null) {
				ParsedDirectories.Clear ();
				return null;
			}

			IsParsing = true;

			var parsedDirs = new List<string> ();
			var newRoot = new RootPackage ();
			foreach (var dir in directoriesToParse) {
				parsedDirs.Add (dir);

				var dir_abs = dir;
				if (!Path.IsPathRooted(dir))
					dir_abs = Path.Combine(fallbackAbsolutePath, dir_abs);

				var ppd = ThreadedDirectoryParser.Parse(dir_abs, newRoot);
				
				if (ppd != null)
					performanceLogs.Add(ppd);
			}

			IsParsing = false;
			ParsedDirectories = parsedDirs;
			Root = newRoot;

			return performanceLogs.ToArray ();
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
