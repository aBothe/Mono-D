using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Debugging
{
	class DebugSymbolTypeEvalVisitor : IResolvedTypeVisitor<ISymbolValue>
	{
		public readonly IDBacktraceSymbol Symbol;
		public readonly DLocalExamBacktrace Backtrace;

		public DebugSymbolTypeEvalVisitor(DLocalExamBacktrace b, IDBacktraceSymbol s)
		{
			this.Backtrace = b;
			this.Symbol = s;
		}

		public ISymbolValue VisitPrimitiveType(PrimitiveType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitPointerType(PointerType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitArrayType(ArrayType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitAssocArrayType(AssocArrayType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitDelegateType(DelegateType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitAliasedType(AliasedType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitEnumType(EnumType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitStructType(StructType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitUnionType(UnionType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitClassType(ClassType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitInterfaceType(InterfaceType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitTemplateType(TemplateType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitMixinTemplateType(MixinTemplateType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitEponymousTemplateType(EponymousTemplateType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitStaticProperty(StaticProperty t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitMemberSymbol(MemberSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitModuleSymbol(ModuleSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitPackageSymbol(PackageSymbol t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitDTuple(DTuple t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitUnknownType(UnknownType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitAmbigousType(AmbiguousType t)
		{
			throw new NotImplementedException();
		}
	}

}
