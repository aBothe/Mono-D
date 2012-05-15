using System.Collections.Generic;
using System.IO;
using D_Parser.Dom;

namespace D_Parser.Resolver.TypeResolution
{
	public class ResultCache
	{
		#region Properties
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
		public SortedDictionary<string, List<IBlockNode>> Types = new SortedDictionary<string, List<IBlockNode>>();

		/// <summary>
		/// variable/alias/method name -> symbol [+ overloads/defintions in other modules]
		/// </summary>
		public SortedDictionary<string, List<INode>> GloballyScopedSymbols = new SortedDictionary<string, List<INode>>();
		#endregion

		public virtual void Clear()
		{
			ModulePackageNames.Clear();

			Types.Clear();
			GloballyScopedSymbols.Clear();
			Modules.Clear();
		}

		public void Add(IAbstractSyntaxTree ast, string modulePackage=null)
		{
			if (modulePackage == null)
			{
				int lastDot=ast.ModuleName.LastIndexOf('.');

				if (lastDot < 0)
					modulePackage = "";
				else
					modulePackage = ast.ModuleName.Substring(0, lastDot);
			}

			Modules[ast.ModuleName] = ast;

			// Handle its package origin
			List<IAbstractSyntaxTree> modPackageList = null;
			if (!ModulePackages.TryGetValue(modulePackage, out modPackageList))
				ModulePackages[modulePackage] = new List<IAbstractSyntaxTree> { ast };
			else
				modPackageList.Add(ast);

			// Put public root symbols to the types/globalsymbols dictionaries
			HandleDictEntries(ast);
		}

		public void Add(string rootDirectory, IAbstractSyntaxTree ast)
		{
			// Handle module packages
			var relativeDirectory = Path.GetDirectoryName(ast.FileName.Substring(rootDirectory.Length));
			var modulePackage = relativeDirectory.Replace(Path.DirectorySeparatorChar, '.');

			if (!ModulePackageNames.ContainsKey(modulePackage))
				ModulePackageNames.Add(modulePackage, Path.GetDirectoryName(ast.FileName));

			// Overwrite its module name
			ast.ModuleName = DModule.GetModuleName(rootDirectory,ast);

			Add(ast, modulePackage);
		}

		protected void HandleDictEntries(IAbstractSyntaxTree ast)
		{
			foreach (var def in ast)
			{
				if (def != null && !HandleTypeEntry(def))
					HandleGlobalMemberEntry(def);
			}
		}

		protected bool HandleTypeEntry(INode n)
		{
			if ((n is DEnum || n is DClassLike) && !string.IsNullOrEmpty(n.Name))
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

		protected void HandleGlobalMemberEntry(INode n)
		{
			if (!string.IsNullOrEmpty(n.Name))
			{
				List<INode> entries = null;

				if (!GloballyScopedSymbols.TryGetValue(n.Name, out entries))
					GloballyScopedSymbols[n.Name] = new List<INode> { n };
				else
					entries.Add(n);
			}
		}
	}
}
