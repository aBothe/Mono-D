using D_Parser.Dom;

namespace D_Parser.Completion
{
	public interface ICompletionDataGenerator
	{
		/// <summary>
		/// Adds a token entry
		/// </summary>
		void Add(int Token);

		/// <summary>
		/// Adds a property attribute
		/// </summary>
		void AddPropertyAttribute(string AttributeText);

		void AddTextItem(string Text, string Description);

		/// <summary>
		/// Adds a node to the completion data
		/// </summary>
		/// <param name="Node"></param>
		void Add(INode Node);

		/// <summary>
		/// Adds a module (name stub) to the completion data
		/// </summary>
		/// <param name="ModuleName"></param>
		/// <param name="AssocModule"></param>
		void Add(string ModuleName, IAbstractSyntaxTree Module = null, string PathOverride = null);
	}
}
