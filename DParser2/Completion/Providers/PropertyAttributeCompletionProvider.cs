
namespace D_Parser.Completion
{
	public class PropertyAttributeCompletionProvider : AbstractCompletionProvider
	{
		public static bool CompletesEnteredText(string EnteredText)
		{
			return EnteredText == "@";
		}

		public PropertyAttributeCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			foreach (var propAttr in new[] {
					"disable",
					"property",
					"safe",
					"system",
					"trusted"
				})
				CompletionDataGenerator.AddPropertyAttribute(propAttr);
		}
	}
}
