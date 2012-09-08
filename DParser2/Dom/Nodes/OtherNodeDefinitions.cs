using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Dom
{
	public class DVariable : DNode
	{
		public IExpression Initializer; // Variable
		public bool IsAlias = false;
		public bool IsAliasThis { get { return IsAlias && Name == "this"; } }

		public bool IsLocal
		{
			get { return Parent is DMethod; }
		}
		public bool IsParameter
		{
			get { return IsLocal && (Parent as DMethod).Parameters.Contains(this); }
		}

		public override string ToString(bool Attributes, bool IncludePath)
		{
			return (IsAlias ? "alias " : "") + base.ToString(Attributes, IncludePath) + (Initializer != null ? (" = " + Initializer.ToString()) : "");
		}

		public bool IsConst
		{
			get
			{
				return ContainsAttribute(DTokens.Const, DTokens.Enum); // TODO: Are there more tokens that indicate a const value?
			}
		}

		public override void AssignFrom(INode other)
		{
			if (other is DVariable)
			{
				var dv = other as DVariable;
				Initializer = dv.Initializer;
				IsAlias = dv.IsAlias;
			}

			base.AssignFrom(other);
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class DMethod : DNode, IBlockNode
	{
		public List<INode> Parameters = new List<INode>();
		public MethodType SpecialType = MethodType.Normal;

		BlockStatement _In;
		BlockStatement _Out;
		public IdentifierDeclaration OutResultVariable;
		BlockStatement _Body;

		readonly NodeDictionary children;
		readonly List<INode> additionalChildren = new List<INode>();

		/// <summary>
		/// Used to identify constructor methods. Since it'd be a token otherwise it cannot be used as a regular method's name.
		/// </summary>
		public const string ConstructorIdentifier = "this";

		public DMethod()
		{
			children = new NodeDictionary(this);
		}

		public BlockStatement GetSubBlockAt(CodeLocation Where)
		{
			if (_In != null && _In.Location <= Where && _In.EndLocation >= Where)
				return _In;

			if (_Out != null && _Out.Location <= Where && _Out.EndLocation >= Where)
				return _Out;

			if (_Body != null && _Body.Location <= Where && _Body.EndLocation >= Where)
				return _Body;

			return null;
		}

		public override void AssignFrom(INode other)
		{
			if (other is DMethod)
			{
				var dm = (DMethod)other;

				Parameters = dm.Parameters;
				SpecialType = dm.SpecialType;
				_In = dm._In;
				_Out = dm._Out;
				_Body = dm._Body;
				UpdateChildrenArray();
			}

			base.AssignFrom(other);
		}

		public BlockStatement In { get { return _In; } set { _In = value; UpdateChildrenArray(); } }
		public BlockStatement Out { get { return _Out; } set { _Out = value; UpdateChildrenArray(); } }
		public BlockStatement Body { get { return _Body; } set { _Body = value; UpdateChildrenArray(); } }

		public NodeDictionary Children
		{
			get { return children; }
		}

		/// <summary>
		/// Children which were added artifically via Add() or AddRange()
		/// In most cases, these are anonymous delegate/class declarations.
		/// </summary>
		public List<INode> AdditionalChildren
		{
			get { return additionalChildren; }
		}

		void UpdateChildrenArray()
		{
			lock (children)
			{
				children.Clear();

				if (additionalChildren.Count != 0)
					children.AddRange(additionalChildren);

				if (_In != null)
					children.AddRange(_In.Declarations);

				if (_Body != null)
					children.AddRange(_Body.Declarations);

				if (_Out != null)
					children.AddRange(_Out.Declarations);
			}
		}

		public enum MethodType
		{
			Normal = 0,
			Delegate,
			AnonymousDelegate,
			Constructor,
			Allocator,
			Destructor,
			Deallocator,
			Unittest,
			ClassInvariant
		}

		public DMethod(MethodType Type) : this() { SpecialType = Type; }

		public override string ToString(bool Attributes, bool IncludePath)
		{
			var s = base.ToString(Attributes, IncludePath) + "(";
			foreach (var p in Parameters)
				s += (p is AbstractNode ? (p as AbstractNode).ToString(false) : p.ToString()) + ",";
			return s.Trim(',') + ")";
		}

		public CodeLocation BlockStartLocation
		{
			get
			{
				if (_In != null && _Out != null)
					return _In.Location < _Out.Location ? _In.Location : _Out.Location;
				else if (_In != null)
					return _In.Location;
				else if (_Out != null)
					return _Out.Location;
				else if (_Body != null)
					return _Body.Location;

				return CodeLocation.Empty;
			}
			set { }
		}

		public void Add(INode Node)
		{
			Node.Parent = this;
			additionalChildren.Add(Node);

			UpdateChildrenArray();
		}

		public void AddRange(IEnumerable<INode> Nodes)
		{
			foreach (var n in Nodes)
			{
				n.Parent = this;
				additionalChildren.Add(n);
			}

			UpdateChildrenArray();
		}

		public int Count
		{
			get
			{
				if (children == null)
					return 0;
				return children.Count;
			}
		}

		public System.Collections.ObjectModel.ReadOnlyCollection<INode> this[string Name]
		{
			get
			{
				return children[Name];
			}
		}

		public IEnumerator<INode> GetEnumerator()
		{
			if (children == null)
				UpdateChildrenArray();

			return children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			if (children == null)
				UpdateChildrenArray();

			return children.GetEnumerator();
		}


		public void Clear()
		{
			children.Clear();
			additionalChildren.Clear();
			UpdateChildrenArray();
		}

		/// <summary>
		/// Returns true if the function has got at least one parameter and is a direct child of an abstract syntax tree.
		/// </summary>
		public bool IsUFCSReady
		{
			get
			{
				return Parameters != null && Parameters.Count != 0 && Parent is IAbstractSyntaxTree;
			}
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class DClassLike : DBlockNode
	{
		public bool IsAnonymousClass = false;

		public List<ITypeDeclaration> BaseClasses = new List<ITypeDeclaration>();
		public int ClassType = DTokens.Class;

		public DClassLike() { }
		public DClassLike(int ClassType)
		{
			this.ClassType = ClassType;
		}

		public override string ToString(bool Attributes, bool IncludePath)
		{
			var ret = (Attributes ? (AttributeString + " ") : "") + DTokens.GetTokenString(ClassType) + " ";

			if (IncludePath)
				ret += GetNodePath(this, true);
			else
				ret += Name;

			if (TemplateParameters != null && TemplateParameters.Length > 0)
			{
				ret += "(";
				foreach (var tp in TemplateParameters)
					ret += tp.ToString() + ",";
				ret = ret.TrimEnd(',') + ")";
			}

			if (BaseClasses.Count > 0)
				ret += ":";
			foreach (var c in BaseClasses)
				ret += c.ToString() + ", ";

			return ret.Trim().TrimEnd(',');
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class DEnum : DBlockNode
	{
		public override string ToString(bool Attributes, bool IncludePath)
		{
			return (Attributes ? (AttributeString + " ") : "") + "enum " + (IncludePath ? GetNodePath(this, true) : Name);
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class DEnumValue : DVariable
	{
		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}
