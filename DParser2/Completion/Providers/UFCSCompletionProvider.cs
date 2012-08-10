using D_Parser.Resolver;

namespace D_Parser.Completion.Providers
{
	/// <summary>
	/// Adds method items to the completion list if the current expression's type is matching the methods' first parameter
	/// </summary>
	public class UFCSCompletionProvider
	{
		public static void Generate(ISemantic rr, ResolverContextStack ctxt, IEditorData ed, ICompletionDataGenerator gen)
		{
			if(ed.ParseCache!=null)
				foreach (var pc in ed.ParseCache)
					if (pc != null && pc.UfcsCache != null && pc.UfcsCache.CachedMethods != null && pc.UfcsCache.CachedMethods.Count != 0)
					{
						var r=pc.UfcsCache.FindFitting(ctxt, ed.CaretLocation, rr);
						if(r!=null)
							foreach (var m in r)
								gen.Add(m);
					}
		}
	}
}
