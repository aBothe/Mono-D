
namespace D_Parser.Dom
{
	public abstract class AbstractMetaDeclaration : ISyntaxRegion, IVisitable<MetaDeclarationVisitor>
	{
		public abstract CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public abstract void Accept(MetaDeclarationVisitor vis);
	}

	public interface IMetaDeclarationBlock : ISyntaxRegion, IVisitable<MetaDeclarationVisitor>
	{
		CodeLocation BlockStartLocation { get; set; }
	}

	public class AttributeMetaDeclaration : AbstractMetaDeclaration
	{
		public DAttribute[] AttributeOrCondition;

		public ElseMetaDeclaration OptionalElseBlock;

		public AttributeMetaDeclaration(params DAttribute[] attr)
		{
			this.AttributeOrCondition = attr;
		}

		/// <summary>
		/// The start location of the first given attribute
		/// </summary>
		public override CodeLocation Location
		{
			get
			{
				return AttributeOrCondition[0].Location;
			}
			set
			{
				AttributeOrCondition[0].Location = value;
			}
		}

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	public class ElseMetaDeclaration : AbstractMetaDeclaration
	{
		public override CodeLocation Location
		{
			get;
			set;
		}

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	public class ElseMetaDeclarationBlock : ElseMetaDeclaration, IMetaDeclarationBlock
	{
		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	/// <summary>
	/// Describes a meta block that begins with a colon. 'Ends' right after the colon.
	/// </summary>
	public class AttributeMetaDeclarationSection : AttributeMetaDeclaration
	{
		public AttributeMetaDeclarationSection(DAttribute attr) : base(attr) { }

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	/// <summary>
	/// Describes a meta block that is enclosed by curly braces.
	/// Examples are
	/// static if(...){
	/// }
	/// 
	/// @safe{
	/// }
	/// </summary>
	public class AttributeMetaDeclarationBlock : AttributeMetaDeclaration, IMetaDeclarationBlock
	{
		public AttributeMetaDeclarationBlock(params DAttribute[] attr) : base(attr) {}

		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}

	/// <summary>
	/// A simple block that is just used for code alignment but semantically irrelevant elsehow.
	/// {
	///		int cascadedIntDecl;
	/// }
	/// </summary>
	public class MetaDeclarationBlock : AbstractMetaDeclaration, IMetaDeclarationBlock
	{
		public CodeLocation BlockStartLocation
		{
			get;
			set;
		}

		public override CodeLocation Location
		{
			get { return BlockStartLocation; }
			set { BlockStartLocation = value; }
		}

		public override void Accept(MetaDeclarationVisitor vis)
		{
			vis.Visit(this);
		}
	}
}
