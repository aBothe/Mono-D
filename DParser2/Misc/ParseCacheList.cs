using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver;

namespace D_Parser.Misc
{
	public class ParseCacheList : List<ParseCache>
	{
		public static ParseCacheList Create(params ParseCache[] caches)
		{
			var pcl = new ParseCacheList();

			pcl.AddRange(caches);

			return pcl;
		}

		public IEnumerable<IAbstractSyntaxTree> LookupModuleName(string moduleName)
		{
			foreach (var pc in this)
			{
				var r = pc.GetModule(moduleName);

				if (r != null)
					yield return r;
			}
		}

		public IEnumerable<ModulePackage> LookupPackage(string packageName)
		{
			foreach (var pc in this)
			{
				var r = pc.Root.GetOrCreateSubPackage(packageName);

				if (r != null)
					yield return r;
			}
		}

		public DClassLike[] ObjectClass
		{
			get
			{
				var l = new List<DClassLike>();
				foreach (var pc in this)
					if (pc.IsObjectClassDefined)
						l.Add(pc.ObjectClass);
				return l.ToArray();
			}
		}

		/// <summary>
		/// Will always return a non-null value!
		/// </summary>
		public TypeResult[] ObjectClassResult
		{
			get
			{
				var l = new List<TypeResult>();
				foreach (var pc in this)
					if (pc.IsObjectClassDefined)
						l.Add(pc.ObjectClassResult);
				return l.ToArray();
			}
		}
	}
}
