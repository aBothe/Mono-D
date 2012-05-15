using D_Parser.Resolver;

namespace D_Parser.Completion.Providers
{
	/// <summary>
	/// Adds method items to the completion list if the current expression's type is matching the methods' first parameter
	/// </summary>
	public class UFCSCompletionProvider
	{
		public static void Generate(ResolveResult rr, ResolverContextStack ctxt, IEditorData ed, ICompletionDataGenerator gen)
		{
			foreach (var pc in ed.ParseCache)
			{
				foreach (var m in pc.UfcsCache.FindFitting(ctxt, ed.CaretLocation, rr))
					gen.Add(m);
			}
		}
	}
}
