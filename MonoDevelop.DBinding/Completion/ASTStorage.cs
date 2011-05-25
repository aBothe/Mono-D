using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Core;
using D_Parser;
using System.IO;
using MonoDevelop.Core;

namespace MonoDevelop.D.Completion
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
		public readonly List<ASTCollection> ParsedGlobalDictionaries = new List<ASTCollection>();

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

		public void LoadFromDatabase(string file)
		{
			//TODO: Get all these BinaryStorage procedures working
		}

		public void SaveToFile(string file)
		{

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
		/// Adds and parses a dictionary to the collection
		/// </summary>
		/// <param name="Dictionary"></param>
		public void Add(string Dictionary)
		{
			foreach (var c in ParsedGlobalDictionaries)
				if (c.BaseDirectory == Dictionary)
				{
					c.UpdateFromBaseDirectory();
					return;
				}

			if (!System.IO.Directory.Exists(Dictionary))
				throw new Exception("Cannot parse \""+Dictionary+"\". Directory does not exist!");

			var nc = new ASTCollection(Dictionary);
			ParsedGlobalDictionaries.Add(nc);
		}

		/// <summary>
		/// Updates (reparses) all sources in all directories of this storage
		/// </summary>
		public void UpdateCache()
		{
			foreach (var pdir in this)
				pdir.UpdateFromBaseDirectory();
		}

		public void WriteParseLog(string outputLog)
		{
			var ms = new MemoryStream(32000);
			var sw = new StreamWriter(ms,Encoding.Unicode);

			foreach (var pdir in this)
			{
				sw.WriteLine("--- "+pdir.BaseDirectory+" ---");
				sw.WriteLine();
				foreach (var t in pdir)
				{
					sw.WriteLine(t.ModuleName + "\t\t("+t.FileName+")");
					if (t.ParseErrors != null)
						foreach (var err in t.ParseErrors)
							sw.WriteLine(string.Format("\t\t{0}\t{1}\t{2}",err.Location.Line, err.Location.Column,err.Message));

					sw.Flush();
				}

				sw.WriteLine();
				sw.Flush();
			}
			
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
		public void UpdateFromBaseDirectory()
		{
			Clear();

			string[] files = Directory.GetFiles(BaseDirectory, "*.d?", SearchOption.AllDirectories);
			foreach (string tf in files)
			{
				if (tf.EndsWith("phobos.d")) continue; // Skip phobos.d

				try
				{
					string tmodule = Path.ChangeExtension(tf, null).Remove(0, BaseDirectory.Length + 1).Replace('\\', '.');

					var ast = DParser.ParseFile(tf);
					ast.ModuleName = tmodule;
					ast.FileName = tf;

					Add(ast);
				}
				catch (Exception ex)
				{
					LoggingService.LogError("Error while parsing "+tf,ex);
				}
			}
		}
	}
}
