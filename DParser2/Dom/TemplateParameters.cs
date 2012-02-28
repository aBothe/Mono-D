using D_Parser.Dom.Expressions;

namespace D_Parser.Dom
{
	public interface ITemplateParameter
	{
		string Name { get; }
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }
	}

	/// <summary>
	/// void foo(U) (U u) {
	///		u. -- now the type of u is needed. A ITemplateParameterDeclaration will be returned which holds U.
	/// }
	/// </summary>
	public class ITemplateParameterDeclaration : ITypeDeclaration
	{
		public ITemplateParameter TemplateParameter;

		public CodeLocation Location
		{
			get			{				return TemplateParameter.Location;			}
			set			{}
		}

		public CodeLocation EndLocation
		{
			get			{				return TemplateParameter.EndLocation;			}
			set			{}
		}

		public ITypeDeclaration InnerDeclaration
		{
			get
			{
				return null;
			}
			set
			{
				
			}
		}

		public ITypeDeclaration InnerMost
		{
			get
			{
				return this;
			}
			set
			{}
		}

		public bool ExpressesVariableAccess
		{
			get
			{
				return false;
			}
			set
			{}
		}

		public string ToString(bool IncludesBase)
		{
			return TemplateParameter.ToString();
		}
	}

	public class TemplateParameterNode : AbstractNode
	{
		public readonly ITemplateParameter TemplateParameter;

		public TemplateParameterNode(ITemplateParameter param)
		{
			TemplateParameter = param;

			Name = param.Name;

			StartLocation = NameLocation = param.Location;
			EndLocation = param.EndLocation;
		}

		public sealed override string ToString()
		{
			return TemplateParameter.ToString();
		}

		public sealed override string ToString(bool Attributes, bool IncludePath)
		{
			return (GetNodePath(this, false) + "." + ToString()).TrimEnd('.');
		}
	}

	public class TemplateTypeParameter : ITemplateParameter
	{
		public string Name { get; set; }

		public ITypeDeclaration Specialization;
		public ITypeDeclaration Default;

		public sealed override string ToString()
		{
			var ret = Name;

			if (Specialization != null)
				ret += ":" + Specialization.ToString();

			if (Default != null)
				ret += "=" + Default.ToString();

			return ret;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateThisParameter : ITemplateParameter
	{
		public string Name { get { return FollowParameter.Name; } }

		public ITemplateParameter FollowParameter;

		public sealed override string ToString()
		{
			return "this" + (FollowParameter != null ? (" " + FollowParameter.ToString()) : "");
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateValueParameter : ITemplateParameter
	{
		public string Name { get; set; }
		public ITypeDeclaration Type;

		public IExpression SpecializationExpression;
		public IExpression DefaultExpression;

		public override string ToString()
		{
			return (Type != null ? (Type.ToString() + " ") : "") + Name/*+ (SpecializationExpression!=null?(":"+SpecializationExpression.ToString()):"")+
				(DefaultExpression!=null?("="+DefaultExpression.ToString()):"")*/;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}

	public class TemplateAliasParameter : TemplateValueParameter
	{
		public ITypeDeclaration SpecializationType;
		public ITypeDeclaration DefaultType;

		public sealed override string ToString()
		{
			return "alias " + base.ToString();
		}
	}

	public class TemplateTupleParameter : ITemplateParameter
	{
		public string Name { get; set; }

		public sealed override string ToString()
		{
			return Name + " ...";
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }
	}
}