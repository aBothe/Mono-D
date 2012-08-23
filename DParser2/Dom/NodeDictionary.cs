using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace D_Parser.Dom
{
	/// <summary>
	/// Stores node children.
	/// Not thread safe.
	/// </summary>
	public class NodeDictionary : IEnumerable<INode>
	{
		Dictionary<string, List<INode>> nameDict = new Dictionary<string, List<INode>>();
		/// <summary>
		/// For faster enum access, store a separate list of INodes
		/// </summary>
		List<INode> children = new List<INode>();
		public readonly INode ParentNode;

		public NodeDictionary() { }
		public NodeDictionary(INode parent)
		{
			ParentNode = parent;
		}

		public void Add(INode Node)
		{
			// Alter the node's parent
			if (ParentNode != null)
				Node.Parent = ParentNode;

			var n = Node.Name ?? "";
			List<INode> l = null;

			lock (nameDict)
				if (!nameDict.TryGetValue(n, out l))
					nameDict[n] = l = new List<INode>();

			l.Add(Node);
			children.Add(Node);
		}

		public void AddRange(IEnumerable<INode> nodes)
		{
			if(nodes!=null)
				foreach (var n in nodes)
					Add(n);
		}

		public bool Remove(string Name)
		{
			if (Name == null)
				Name = "";

			var l = this[Name];

			if (l != null)
			{
				foreach (var i in l)
					children.Remove(i);

				nameDict[Name] = null;
				return true;
			}
			return false;
		}

		public bool Remove(INode n)
		{
			var gotRemoved = children.Remove(n);
			
			var Name = n.Name ?? "";
			List<INode> l = null;
			if(nameDict.TryGetValue(Name, out l))
			{
				gotRemoved = l.Remove(n) || gotRemoved;
				if (l.Count == 0)
					nameDict[Name] = null;
			}

			return gotRemoved;
		}

		public void Clear()
		{
			nameDict.Clear();
			children.Clear();
		}

		public int Count
		{
			get
			{
				return children.Count;
			}
		}

		public bool HasMultipleOverloads(string Name)
		{
			List<INode> l = null;

			if (nameDict.TryGetValue(Name ?? "", out l))
				return l.Count > 1;

			return false;
		}

		public IEnumerator<INode> GetEnumerator()
		{
			return children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return children.GetEnumerator();
		}

		public ReadOnlyCollection<INode> this[string Name]
		{
			get
			{
				List<INode> l = null;
				if(nameDict.TryGetValue(Name ?? "", out l))
					return new ReadOnlyCollection<INode>(l);
				return null;
			}
		}
	}
}
