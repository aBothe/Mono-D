using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class StaticPropertyResolver
	{
		public static AbstractType getStringType(ResolverContextStack ctxt)
		{
			var str = new IdentifierDeclaration("string");
			var sType = TypeDeclarationResolver.Resolve(str, ctxt);
			ctxt.CheckForSingleResult(sType, str);

			return sType != null && sType.Length != 0 ? sType[0] : null;
		}

		/// <summary>
		/// Tries to resolve a static property's name.
		/// Returns a result describing the theoretical member (".init"-%gt;MemberResult; ".typeof"-&gt;TypeResult etc).
		/// Returns null if nothing was found.
		/// </summary>
		/// <param name="InitialResult"></param>
		/// <returns></returns>
		public static MemberSymbol TryResolveStaticProperties(
			ISemantic InitialResult, 
			string propertyIdentifier, 
			ResolverContextStack ctxt = null, 
			bool Evaluate = false,
			IdentifierDeclaration idContainter = null)
		{
			// If a pointer'ed type is given, take its base type
			if (InitialResult is PointerType)
				InitialResult = ((PointerType)InitialResult).Base;

			if (InitialResult == null || InitialResult is ModuleSymbol)
				return null;

			INode relatedNode = null;

			if (InitialResult is DSymbol)
				relatedNode = ((DSymbol)InitialResult).Definition;

			#region init
			if (propertyIdentifier == "init")
			{
				var prop_Init = new DVariable
				{
					Name = "init",
					Description = "Initializer"
				};

				if (relatedNode != null)
				{
					if (!(relatedNode is DVariable))
					{
						prop_Init.Parent = relatedNode.Parent;
						prop_Init.Type = new IdentifierDeclaration(relatedNode.Name);
					}
					else
					{
						prop_Init.Parent = relatedNode;
						prop_Init.Initializer = (relatedNode as DVariable).Initializer;
						prop_Init.Type = relatedNode.Type;
					}
				}

				return new MemberSymbol(prop_Init, DResolver.StripAliasSymbol(AbstractType.Get(InitialResult)), idContainter);
			}
			#endregion

			#region sizeof
			if (propertyIdentifier == "sizeof")
				return new MemberSymbol(new DVariable
					{
						Name = "sizeof",
						Type = new DTokenDeclaration(DTokens.Int),
						Initializer = new IdentifierExpression(4),
						Description = "Size in bytes (equivalent to C's sizeof(type))"
					}, new PrimitiveType(DTokens.Int), idContainter);
			#endregion

			#region alignof
			if (propertyIdentifier == "alignof")
				return new MemberSymbol(new DVariable
					{
						Name = "alignof",
						Type = new DTokenDeclaration(DTokens.Int),
						Description = "Alignment size"
					}, new PrimitiveType(DTokens.Int),idContainter);
			#endregion

			#region mangleof
			if (propertyIdentifier == "mangleof")
				return new MemberSymbol(new DVariable
					{
						Name = "mangleof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the ‘mangled’ representation of the type"
					}, getStringType(ctxt) , idContainter);
			#endregion

			#region stringof
			if (propertyIdentifier == "stringof")
				return new MemberSymbol(new DVariable
					{
						Name = "stringof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the source representation of the type"
					}, getStringType(ctxt), idContainter);
			#endregion

			#region classinfo
			else if (propertyIdentifier == "classinfo")
			{
				var tr = DResolver.StripMemberSymbols(AbstractType.Get(InitialResult)) as TemplateIntermediateType;

				if (tr is ClassType || tr is InterfaceType)
				{
					var ci=new IdentifierDeclaration("TypeInfo_Class")
					{
						InnerDeclaration = new IdentifierDeclaration("object"),
						ExpressesVariableAccess = true,
					};

					var ti = TypeDeclarationResolver.Resolve(ci, ctxt);

					ctxt.CheckForSingleResult(ti, ci);

					return new MemberSymbol(new DVariable { Name = "classinfo", Type = ci }, ti!=null && ti.Length!=0?ti[0]:null, idContainter);
				}
			}
			#endregion

			//TODO: Resolve the types of type-specific properties (floats, ints, arrays, assocarrays etc.)

			return null;
		}
	}
}
