using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface IAbstractSyntaxTree: IBlockNode
	{
		string FileName { get; set; }
		string ModuleName { get; set; }
		ReadOnlyCollection<ParserError> ParseErrors { get; set; }
	}

	public class ParserError
	{
		public readonly bool IsSemantic;
		public readonly string Message;
		public readonly int Token;
		public readonly CodeLocation Location;

		public ParserError(bool IsSemanticError, string Message, int KeyToken, CodeLocation ErrorLocation)
		{
			IsSemantic = IsSemanticError;
			this.Message = Message;
			this.Token = KeyToken;
			this.Location = ErrorLocation;
		}
	}

	public interface IBlockNode: INode, IEnumerable<INode>
	{
		CodeLocation BlockStartLocation { get; set; }
		NodeDictionary Children { get; }

		void Add(INode Node);
		void AddRange(IEnumerable<INode> Nodes);
		int Count { get; }
		void Clear();

		ReadOnlyCollection<INode> this[string Name] { get; }
	}

	public interface INode : ISyntaxRegion
	{
		string Name { get; set; }
		CodeLocation NameLocation { get; set; }
		string Description { get; set; }
		ITypeDeclaration Type { get; set; }

		new CodeLocation Location { get; set; }
		new CodeLocation EndLocation { get; set; }

		/// <summary>
		/// Assigns a node's properties
		/// </summary>
		/// <param name="Other"></param>
		void AssignFrom(INode Other);

		INode Parent { get; set; }
		INode NodeRoot { get; set; }
	}
}
