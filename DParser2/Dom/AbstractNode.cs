using System;

namespace D_Parser.Dom
{
	public abstract class AbstractNode:INode
	{
		ITypeDeclaration _Type;
		string _Name="";
		INode _Parent;
		string _Description="";
		CodeLocation _StartLocation;
		CodeLocation _EndLocation;

		public CodeLocation EndLocation
		{
			get { return _EndLocation; }
			set { _EndLocation = value; }
		}

		public CodeLocation StartLocation
		{
			get { return _StartLocation; }
			set { _StartLocation = value; }
		}

		public virtual string Description
		{
			get { return _Description; }
			set { _Description = value; }
		}

		public virtual ITypeDeclaration Type
		{
			get { return _Type; }
			set { _Type = value; }
		}

		public virtual string Name
		{
			get { return _Name; }
			set { _Name = value; }
		}

		public bool IsAnonymous { get { return string.IsNullOrEmpty(Name); } }

		public INode Parent
		{
			get { return _Parent; }
			set { _Parent = value; }
		}

		public override string ToString()
		{
			return ToString(true,true);
		}

		public string ToString(bool IncludePath)
		{
			return ToString(true, IncludePath);
		}

		public static string GetNodePath(INode n,bool includeActualNodesName)
		{
			string path = "";
			var curParent = includeActualNodesName?n:n.Parent;
			while (curParent != null)
			{
				// Also include module path
				if (curParent is IAbstractSyntaxTree)
					path = (curParent as IAbstractSyntaxTree).ModuleName + "." + path;
				else
					path = curParent.Name + "." + path;

				curParent = curParent.Parent;
			}
			return path.Trim('.');
		}

		public virtual string ToString(bool Attributes,bool IncludePath)
		{
			string s = "";
			// Type
			if (Type != null)
				s += Type.ToString() + " ";

			// Path + Name
			if (IncludePath)
				s += GetNodePath(this, true);
			else
				s += Name;

			return s.Trim();
		}

		public virtual void AssignFrom(INode other)
		{
			Type = other.Type;
			Name = other.Name;

			Parent = other.Parent;
			Description = other.Description;
			StartLocation = other.StartLocation;
			EndLocation = other.EndLocation;
		}

		public INode NodeRoot
		{
			get
			{
				if (Parent == null)
					return this;
				else return Parent.NodeRoot;
			}
			set
			{
				if (Parent == null)
					Parent = value;
				else Parent.NodeRoot = value;
			}
		}
	}
}
