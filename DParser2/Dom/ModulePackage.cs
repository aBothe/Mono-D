using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Misc;

namespace D_Parser.Dom
{
	public class RootPackage : ModulePackage
	{
		public override string ToString()
		{
			return "<Root>";
		}
	}

	public class ModulePackage : IEnumerable<IAbstractSyntaxTree>, IEnumerable<ModulePackage>
	{
		public ModulePackage Parent { get; internal set; }

		public string Name = "";
		public Dictionary<string, ModulePackage> Packages = new Dictionary<string, ModulePackage>();
		public Dictionary<string, IAbstractSyntaxTree> Modules = new Dictionary<string, IAbstractSyntaxTree>();

		public string Path
		{
			get
			{
				return ((Parent == null || Parent is RootPackage) ? "" : (Parent.Path + ".")) + Name;
			}
		}

		public override string ToString()
		{
			return Path;
		}

		public IEnumerator<IAbstractSyntaxTree> GetEnumerator()
		{
			lock(Modules)
				foreach (var kv in Modules)
					yield return kv.Value;

			lock(Packages)
				foreach (var kv in Packages)
					foreach (var ast in kv.Value)
						yield return ast;
		}

		IEnumerator<ModulePackage> IEnumerable<ModulePackage>.GetEnumerator()
		{
			foreach (var kv in Packages)
			{
				yield return kv.Value;

				foreach (var p in (IEnumerable<ModulePackage>)kv.Value)
					yield return p;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public ModulePackage GetOrCreateSubPackage(string package, bool create = false)
		{
			if (string.IsNullOrEmpty(package))
				return this;

			var currentPackage = this;

			foreach (var p in ModuleNameHelper.SplitModuleName(package))
			{
				ModulePackage returnValue = null;

				if (!currentPackage.Packages.TryGetValue(p, out returnValue))
				{
					if (create)
						returnValue = currentPackage.Packages[p] =
							new ModulePackage
							{
								Name = p,
								Parent = currentPackage
							};
					else
						return null;
				}

				currentPackage = returnValue;
			}

			return currentPackage;
		}

		public static ModulePackage GetOrCreatePackage(ModulePackage root, string package, bool create = false)
		{
			return root.GetOrCreateSubPackage(package, create);
		}
	}
}
