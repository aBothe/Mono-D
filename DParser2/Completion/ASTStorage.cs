using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using D_Parser.Dom;
using D_Parser.Parser;
using System.Diagnostics;

namespace D_Parser.Completion
{
	/// <summary>
	/// Class which is responsible for caching all parsed D source files.
	/// There are two cache types:
	///		- Locally and
	///		- Globally cached ASTs
	///	While the local ASTs won't be saved when D-IDE exits, the global sources will be stored permanently to grant access to them everytime.
	/// </summary>
	public class ASTStorage:IEnumerable<ASTCollection>
	{
		public IEnumerable<IAbstractSyntaxTree> ParseCache
		{
			get;
			protected set;
		}
		public readonly List<ASTCollection> ParsedGlobalDictionaries = new List<ASTCollection>();
		public bool IsParsing { get; protected set; }

		/// <summary>
		/// List of all paths of all added directories
		/// </summary>
		public string[] DirectoryPaths
		{
			get {
				var ret = new List<string>();

				foreach (var dir in ParsedGlobalDictionaries)
					ret.Add(dir.BaseDirectory);

				return ret.ToArray();
			}
		}

		/* Notes:
		 *  When a single, unbound module looks up files, it's allowed only to seek within the global files.
		 *  
		 *  When a project module tries to look up imports, it can use the global cache as well as 
		 */

		public void Remove(string Dict)
		{
			foreach(var c in ParsedGlobalDictionaries.ToArray())
				if (c.BaseDirectory == Dict)
					ParsedGlobalDictionaries.Remove(c);
		}

		public bool ContainsDictionary(string Dict)
		{
			foreach (var c in ParsedGlobalDictionaries)
				if (c.BaseDirectory == Dict)
					return true;
			return false;
		}

		/// <summary>
		/// Adds a dictionary to the collection. Does NOT parse the dictionary thereafter.
		/// Returns false if directory hasn't been added.
		/// </summary>
		/// <param name="Dictionary"></param>
		public bool Add(string Dictionary, bool ParseFunctionBodies=true)
		{
			foreach (var c in ParsedGlobalDictionaries)
				if (c.BaseDirectory == Dictionary)
				{
					//c.UpdateFromBaseDirectory();
					return true;
				}

			if (!System.IO.Directory.Exists(Dictionary))
			{
				//ErrorLogger.Log("Cannot parse \"" + Dictionary + "\". Directory does not exist!",ErrorType.Error,ErrorOrigin.Parser);
				return false;
			}

			var nc = new ASTCollection(Dictionary) { ParseFunctionBodies=ParseFunctionBodies};
			ParsedGlobalDictionaries.Add(nc);
			return true;
		}

		/// <summary>
		/// Updates (reparses) all sources in all directories of this storage
		/// </summary>
		public ParsePerformanceData[] UpdateCache(bool ReturnOnError=false)
		{
			var l = new List<ParsePerformanceData>(ParsedGlobalDictionaries.Count);
			IsParsing = true;
			try
			{
				foreach (var pdir in this)
				{
					var ppd = pdir.UpdateFromBaseDirectory(ReturnOnError);
					if (ppd != null)
						l.Add(ppd);
				}

				UpdateEditorFastAccessCache();
			}
			finally
			{
				IsParsing = false;
			}
			return l.ToArray();
		}

		public void UpdateEditorFastAccessCache()
		{
			var cache = new List<IAbstractSyntaxTree>();

			foreach (var pdir in ParsedGlobalDictionaries)
				cache.AddRange(pdir);

			ParseCache = cache;
		}

		public void WriteParseLog(string outputLog)
		{
			var ms = new MemoryStream(32000);
			var sw = new StreamWriter(ms,Encoding.Unicode);

			sw.WriteLine("Parser error log");
			sw.WriteLine();

			foreach (var pdir in this)
			{
				sw.WriteLine("--- "+pdir.BaseDirectory+" ---");
				sw.WriteLine();
				bool hadErrors = false;
				foreach (var t in pdir)
				{
					if (t.ParseErrors.Count<1)
						continue;
					hadErrors = true;
					sw.WriteLine(t.ModuleName + "\t\t("+t.FileName+")");
					foreach (var err in t.ParseErrors)
						sw.WriteLine(string.Format("\t\t{0}\t{1}\t{2}",err.Location.Line, err.Location.Column,err.Message));

					sw.Flush();
				}

				if (!hadErrors)
					sw.WriteLine("No errors found.");

				sw.WriteLine();
				sw.Flush();
			}

			if (File.Exists(outputLog))
				File.Delete(outputLog);

			File.WriteAllBytes(outputLog, ms.ToArray());
			ms.Close();
		}

		public IEnumerator<ASTCollection> GetEnumerator()
		{
			return ParsedGlobalDictionaries.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ParsedGlobalDictionaries.GetEnumerator();
		}

		/// <summary>
		/// Seeks the module named ModulePath within all parsed global directories.
		/// </summary>
		/// <param name="ModuleName"></param>
		/// <returns></returns>
		public IAbstractSyntaxTree LookUpModuleName(string ModuleName)
		{
			foreach (var dir in ParsedGlobalDictionaries)
			{
				var ret=dir[ModuleName, true];
				if (ret != null)
					return ret;
			}
			return null;
		}

		public IAbstractSyntaxTree LookUpModulePath(string ModulePath)
		{
			foreach (var dir in ParsedGlobalDictionaries)
			{
				var ret = dir[ModulePath, false];
				if (ret != null)
					return ret;
			}
			return null;
		}
	}

	public class ASTCollection:List<IAbstractSyntaxTree>
	{
		public string BaseDirectory { get; set; }

		[DefaultValue(true)]
		public bool ParseFunctionBodies { get; set; }

		public ASTCollection() { }

		public ASTCollection(string baseDir)
		{
			BaseDirectory = baseDir;
		}

		public void Remove(string file,bool ByModuleName)
		{
			foreach (var c in ToArray())
				if (ByModuleName ? c.ModuleName == file : c.FileName == file)
				{
					Remove(c);
					return;
				}
		}

		public bool ContainsDictionary(string file,bool ByModuleName)
		{
			foreach (var c in ToArray())
				if (ByModuleName ? c.ModuleName == file : c.FileName == file)
					return true;
			return false;
		}
		
		public new void Add(IAbstractSyntaxTree tree)
		{
			if (tree == null)
				return;

			//Remove(tree.FileName, false);
			base.Add(tree);
		}

		public IAbstractSyntaxTree this[string file]
		{
			get { return this[file, false]; }
			set { this[file, false] = value; }
		}

		public IAbstractSyntaxTree this[string AbsoluteFileName,bool ByModuleName]
		{
			get{
				foreach (var ast in this)
					if(ast!=null)
						if (ByModuleName ? ast.ModuleName == AbsoluteFileName : ast.FileName == AbsoluteFileName)
							return ast;
				return null;
			}
			set
			{
				Remove(AbsoluteFileName, ByModuleName);
				base.Add(value);
			}
		}

		/// <summary>
		/// Parse the base directory.
		/// </summary>
		public ParsePerformanceData UpdateFromBaseDirectory(bool ReturnOnException=false)
		{
			Clear();
			// wild card character ? seems to behave differently across platforms
			// msdn: -> Exactly zero or one character.
			// monodocs: -> Exactly one character.
			string[] dFiles = Directory.GetFiles(BaseDirectory, "*.d", SearchOption.AllDirectories);
			string[] diFiles = Directory.GetFiles(BaseDirectory, "*.di", SearchOption.AllDirectories);
			string[] files = new string[dFiles.Length + diFiles.Length];			
			Array.Copy(dFiles, 0, files, 0, dFiles.Length);
			Array.Copy(diFiles, 0, files, dFiles.Length, diFiles.Length);

			var sw = new Stopwatch();				

			var ppd = new ParsePerformanceData { BaseDirectory=BaseDirectory };
			
			foreach (string tf in files)
			{
				if (tf.EndsWith("phobos"+Path.DirectorySeparatorChar+ "index.d") ||
					tf.EndsWith("phobos" + Path.DirectorySeparatorChar + "phobos.d")) continue; // Skip index.d (D2) || phobos.d (D2|D1)

					string tmodule = Path.ChangeExtension(tf, null).Remove(0, BaseDirectory.Length + 1).Replace(Path.DirectorySeparatorChar, '.');

					sw.Start();
				try
				{
					var ast = DParser.ParseFile(tf,!ParseFunctionBodies);
					sw.Stop();
					ppd.AmountFiles++;
					ppd.TotalDuration += sw.Elapsed.TotalSeconds;
					sw.Reset();
					ast.ModuleName = tmodule;
					ast.FileName = tf;
					Add(ast);
				}
				catch (Exception)
				{
					if (ReturnOnException)
						return ppd;
					/*
					ErrorLogger.Log(ex);
					if (MessageBox.Show("Continue Parsing?", "Parsing exception", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
						return;*/
				}
			}

			return ppd;
			/*
			ErrorLogger.Log("Parsed "+files.Length+" files in "+BaseDirectory+" in "+Math.Round(duration,2).ToString()+"s (~"+Math.Round(duration/files.Length,3).ToString()+"s per file)",
				ErrorType.Information,ErrorOrigin.Parser);*/
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
		public double FileDuration { get {
			if (AmountFiles > 0)
				return TotalDuration / AmountFiles;
			return 0;
		} }
	}
}
