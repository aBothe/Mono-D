
namespace D_Parser.Dom
{
	public interface ISyntaxRegion
	{
		CodeLocation Location { get; }
		CodeLocation EndLocation { get; }
	}
}
