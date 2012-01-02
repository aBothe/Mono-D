using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Completion;
using System.Collections.Specialized;

namespace D_Parser.Misc
{
	public class ParseCache
	{
		#region Properties
		public List<string> Directories = new List<string>();

		public readonly List<ParsePerformanceData> PerformanceData=new List<ParsePerformanceData>();
		public Exception LastException { get; protected set; }

		/// <summary>
		/// package name -> physical directory path
		/// </summary>
		public SortedList<string, string> ModulePackageNames = new SortedList<string, string>();

		/// <summary>
		/// package name -> modules in that package
		/// </summary>
		public SortedList<string, List<IAbstractSyntaxTree>> ModulePackages = new SortedList<string, List<IAbstractSyntaxTree>>();

		/// <summary>
		/// module name -> module definition
		/// </summary>
		public SortedDictionary<string, IAbstractSyntaxTree> Modules = new SortedDictionary<string, IAbstractSyntaxTree>();

		/// <summary>
		/// type name -> type [+ possible overloads/definitions in other modules]
		/// </summary>
		public SortedDictionary <string, List<IBlockNode>> Types= new SortedDictionary<string,List<IBlockNode>>();

		/// <summary>
		/// variable/alias/method name -> symbol [+ overloads/defintions in other modules]
		/// </summary>
		public SortedDictionary<string, List<INode>> GloballyScopedSymbols=new SortedDictionary<string,List<INode>>();
		#endregion

		public void Clear()
		{
			LastException = null;
			PerformanceData.Clear();
			ModulePackageNames.Clear();
			
			Types.Clear();
			GloballyScopedSymbols.Clear();
			Modules.Clear();
		}

		public void Parse(bool ParseFunctionBodies=true)
		{
			Clear();

			var sw = new Stopwatch();

			foreach (var dir in Directories)
			{
				// wild card character ? seems to behave differently across platforms
				// msdn: -> Exactly zero or one character.
				// monodocs: -> Exactly one character.
				string[] dFiles = Directory.GetFiles(dir, "*.d", SearchOption.AllDirectories);
				string[] diFiles = Directory.GetFiles(dir, "*.di", SearchOption.AllDirectories);
				string[] files = new string[dFiles.Length + diFiles.Length];
				Array.Copy(dFiles, 0, files, 0, dFiles.Length);
				Array.Copy(diFiles, 0, files, dFiles.Length, diFiles.Length);
				int c = 0;

				sw.Restart();

				foreach (var file in files)
					if (Parse(dir,file,ParseFunctionBodies))
						c++;
				
				sw.Stop();

				PerformanceData.Add(new ParsePerformanceData {  
					AmountFiles=c, 
					BaseDirectory=dir, 
					TotalDuration=sw.Elapsed.TotalSeconds
				});
			}
		}

		bool Parse(string rootDirectory,string file, bool ParseFunctionBodies)
		{
			// Skip index.d (D2) || phobos.d (D2|D1)
				if (file.EndsWith("phobos" + Path.DirectorySeparatorChar + "phobos.d") ||
					file.EndsWith("phobos"+Path.DirectorySeparatorChar+"index.d"))
					return false;

			// Handle module packages
			var relativeDirectory = Path.GetDirectoryName(file.Substring(rootDirectory.Length));
			var modulePackage=relativeDirectory.Replace(Path.DirectorySeparatorChar,'.');

			if (!ModulePackageNames.ContainsKey(modulePackage))
				ModulePackageNames.Add(modulePackage, Path.GetDirectoryName(file));

			try
			{
				// Parse the file
				var ast = DParser.ParseFile(file, !ParseFunctionBodies);

				// Overwrite its module name
				ast.ModuleName=modulePackage+(modulePackage==""?"":".")+Path.GetFileNameWithoutExtension(file);

				Modules[ast.ModuleName] = ast;

				// Handle its package origin
				List<IAbstractSyntaxTree> modPackageList=null;
				if (!ModulePackages.TryGetValue(modulePackage, out modPackageList))
					ModulePackages[modulePackage] = modPackageList = new List<IAbstractSyntaxTree> { ast };
				else
					modPackageList.Add(ast);

				// Put public root symbols to the types/globalsymbols dictionaries
				HandleDictEntries(ast);
			}
			catch (Exception ex)
			{
				LastException = ex;
				return false;
			}

			return true;
		}

		void HandleDictEntries(IAbstractSyntaxTree ast)
		{
			foreach (var def in ast)
			{
				if (def!=null && !HandleTypeEntry(def))
					HandleGlobalMemberEntry(def);				
			}
		}

		bool HandleTypeEntry(INode n)
		{
			if (n is DEnum || n is DClassLike)
			{
				List<IBlockNode> entries = null;

				var bn = n as IBlockNode;

				if (!Types.TryGetValue(n.Name, out entries))
					Types[n.Name] = new List<IBlockNode> { bn };
				else
					entries.Add(bn);

				// Search for types recursively
				foreach (var m in bn)
				{
					HandleTypeEntry(m);
				}

				return true;
			}
			return false;
		}

		void HandleGlobalMemberEntry(INode n)
		{
			List<INode> entries = null;

			if (!GloballyScopedSymbols.TryGetValue(n.Name, out entries))
				GloballyScopedSymbols[n.Name] = new List<INode> { n };
			else
				entries.Add(n);
		}
	}
}
