using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System;
using System.IO;

namespace D_Parser.Dom
{
    /// <summary>
    /// Encapsules an entire document and represents the root node
    /// </summary>
    public class DModule : DBlockNode, IAbstractSyntaxTree
    {
		public readonly DateTime ParseTimestamp = DateTime.Now;

        /// <summary>
        /// Applies file name, children and imports from an other module instance
         /// </summary>
        /// <param name="Other"></param>
        public override void AssignFrom(INode Other)
        {
			if (Other is IAbstractSyntaxTree)
				ParseErrors = ((IAbstractSyntaxTree)Other).ParseErrors;

			base.AssignFrom(Other);
        }

		string _FileName;

		/// <summary>
		/// Name alias
		/// </summary>
		public string ModuleName
		{
			get { return Name; }
			set { Name = value; }
		}

		public string FileName
		{
			get
			{
				return _FileName;
			}
			set
			{
				_FileName = value;
			}
		}

		/// <summary>
		/// Returns a package-module name-combination (like std.stdio) in dependency of its base directory (e.g. C:\dmd2\src\phobos)
		/// </summary>
		public static string GetModuleName(string baseDirectory, IAbstractSyntaxTree ast)
		{
			return GetModuleName(baseDirectory, ast.FileName);
		}

		/// <summary>
		/// Returns the relative module name including its packages based on the baseDirectory parameter.
		/// If the file isn't located in the base directory, the file name minus the extension is returned only.
		/// </summary>
		public static string GetModuleName(string baseDirectory, string file)
		{
			if (file!=null && baseDirectory != null && file.StartsWith(baseDirectory))
				return Path.ChangeExtension(
						file.Substring(baseDirectory.Length), null).
							Replace(Path.DirectorySeparatorChar, '.').Trim('.');
			else
				return Path.GetFileNameWithoutExtension(file);
		}

		public System.Collections.ObjectModel.ReadOnlyCollection<ParserError> ParseErrors
		{
			get;
			set;
		}

		/// <summary>
		/// A module's first statement can be a module ABC; statement. If so, this variable will keep it.
		/// </summary>
		public ModuleStatement OptionalModuleStatement;

		public override string ToString(bool Attributes, bool IncludePath)
		{
			if (!IncludePath)
			{
				var parts = ModuleName.Split('.');
				return parts[parts.Length-1];
			}

			return ModuleName;
		}
	}

	public class DBlockNode : DNode, IBlockNode
	{
		CodeLocation _BlockStart;
		protected List<INode> _Children = new List<INode>();

		/// <summary>
		/// Used for storing import statement and similar stuff
		/// </summary>
		public List<IStatement> StaticStatements = new List<IStatement>();

		public CodeLocation BlockStartLocation
		{
			get
			{
				return _BlockStart;
			}
			set
			{
				_BlockStart = value;
			}
		}

		public INode[] Children
		{
			get { return _Children.ToArray(); }
		}

		public IStatement[] Statements
		{
			get { return StaticStatements.ToArray(); }
		}

		public void Add(IStatement Statement)
		{
			StaticStatements.Add(Statement);
		}

		public void Add(INode Node)
		{
			Node.Parent = this;
			if (!_Children.Contains(Node))
				_Children.Add(Node);
		}

		public void AddRange(IEnumerable<INode> Nodes)
		{
			if(Nodes!=null)
				foreach (var Node in Nodes)
					Add(Node);
		}

		public int Count
		{
			get { return _Children.Count; }
		}

		public void Clear()
		{
			_Children.Clear();
		}

		public INode this[int i]
		{
			get { if (i >= 0 && Count > i) return _Children[i]; else return null; }
			set { if (i >= 0 && Count > i) _Children[i] = value; }
		}

		public INode this[string Name]
		{
			get
			{
				if (Count > 0)
					foreach (var n in _Children)
						if (n.Name == Name) return n;
				return null;
			}
			set
			{
				if (Count > 0)
					for (int i = 0; i < Count; i++)
						if (this[i].Name == Name) this[i] = value;
			}
		}

		public IEnumerator<INode> GetEnumerator()
		{
			return _Children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _Children.GetEnumerator();
		}

		public override void AssignFrom(INode other)
		{
			var bn = other as IBlockNode;

			if (bn!=null)
			{
				BlockStartLocation = bn.BlockStartLocation;
				Clear();
				AddRange(bn);

				if (bn is DBlockNode)
				{
					StaticStatements.Clear();
					StaticStatements.AddRange(((DBlockNode)bn).StaticStatements);
				}
			}

			base.AssignFrom(other);
		}
	}

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
            return (IsAlias?"alias ":"")+base.ToString(Attributes,IncludePath)+(Initializer!=null?(" = "+Initializer.ToString()):"");
        }

		public bool IsConst
		{
			get {
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
    }

    public class DMethod : DNode,IBlockNode
    {
        public List<INode> Parameters=new List<INode>();
        public MethodType SpecialType = MethodType.Normal;

		BlockStatement _In;
		BlockStatement _Out;
		public IdentifierDeclaration OutResultVariable;
		BlockStatement _Body;

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
				var dm = other as DMethod;

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

		INode[] children;
		List<INode> additionalChildren = new List<INode>();

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
			var l = new List<INode>();

			l.AddRange(additionalChildren);

			if (_In != null)
				l.AddRange(_In.Declarations);

			if (_Body != null)
				l.AddRange(_Body.Declarations);

			if (_Out != null)
				l.AddRange(_Out.Declarations);

			children = l.ToArray();
		}

        public enum MethodType
        {
            Normal=0,
			Delegate,
            AnonymousDelegate,
            Constructor,
			Allocator,
            Destructor,
			Deallocator,
            Unittest,
            ClassInvariant
        }

        public DMethod() { }
        public DMethod(MethodType Type) { SpecialType = Type; }

		public override string ToString(bool Attributes, bool IncludePath)
        {
            var s= base.ToString(Attributes,IncludePath)+"(";
            foreach (var p in Parameters)
                s += (p is AbstractNode? (p as AbstractNode).ToString(false):p.ToString())+",";
            return s.Trim(',')+")";
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
			set{}
		}

		public INode[] Children
		{
			get { return children; }
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
			get { 
				if (children == null) 
					return 0;
				return children.Length; 
			}
		}

		public INode this[int i]
		{
			get
			{
				if (children != null)
					return children[i];
				return null;
			}
			set
			{
				if (children != null)
					children[i]=value;
			}
		}

		public INode this[string Name]
		{
			get
			{
				if(children!=null)
					foreach (var c in children)
						if (c.Name == Name)
							return c;

				return null;
			}
			set
			{
				if (children != null)
					for(int i=0;i<children.Length;i++)
						if (children[i].Name == Name)
						{
							children[i] = value;
							return;
						}
			}
		}

		public IEnumerator<INode> GetEnumerator()
		{
			if (children == null)
				UpdateChildrenArray();

			return (children as IEnumerable<INode>).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			if (children == null)
				UpdateChildrenArray();

			return children.GetEnumerator();
		}


		public void Clear()
		{
			additionalChildren.Clear();
		}

		public bool IsUFCSReady
		{
			get
			{
				return Parameters!=null && Parameters.Count != 0 && Parent is IAbstractSyntaxTree;
			}
		}
	}

    public class DClassLike : DBlockNode
    {
		public bool IsAnonymousClass = false;

        public List<ITypeDeclaration> BaseClasses=new List<ITypeDeclaration>();
        public int ClassType=DTokens.Class;

        public DClassLike() { }
        public DClassLike(int ClassType)
        {
            this.ClassType = ClassType;
        }

		public override string ToString(bool Attributes, bool IncludePath)
        {
            var ret = (Attributes? (AttributeString + " "):"") + DTokens.GetTokenString(ClassType) + " ";

			if (IncludePath)
				ret += GetNodePath(this, true);
			else
				ret += Name;

			if (TemplateParameters != null && TemplateParameters.Length>0)
			{
				ret += "(";
				foreach (var tp in TemplateParameters)
					ret += tp.ToString()+",";
				ret = ret.TrimEnd(',')+")";
			}

            if (BaseClasses.Count > 0)
                ret += ":";
            foreach (var c in BaseClasses)
                ret += c.ToString()+", ";

            return ret.Trim().TrimEnd(',');
        }
    }

    public class DEnum : DBlockNode
    {
		public override string ToString(bool Attributes, bool IncludePath)
        {
			return (Attributes ? (AttributeString + " ") : "") + "enum " + (IncludePath?GetNodePath(this,true):Name);
        }
	}

    public class DEnumValue : DVariable
    {
    }
}
