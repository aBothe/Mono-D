using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
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
		public readonly bool Is64Bit;
		readonly uint PointerSize;

		public DebugSymbolTypeEvalVisitor(DLocalExamBacktrace b, IDBacktraceSymbol s)
		{
			this.Backtrace = b;
			this.Symbol = s;
			Is64Bit = (PointerSize = (uint)b.BacktraceHelper.PointerSize) == 8;
		}

		public ISymbolValue VisitPrimitiveType(PrimitiveType t)
		{
			var sz = ExamHelpers.SizeOf(t.TypeToken, Is64Bit);
			var bytes = Backtrace.BacktraceHelper.ReadBytes(Symbol.Offset, (ulong)sz);
			return new PrimitiveValue(ExamHelpers.GetNumericValue(bytes, 0, t.TypeToken), t);
		}

		public ISymbolValue VisitPointerType(PointerType t)
		{
			throw new NotImplementedException();
		}

		public ISymbolValue VisitArrayType(ArrayType t)
		{
			var valueType = DResolver.StripMemberSymbols(t.ValueType);
			var primitiveValueType = valueType as PrimitiveType;

			if (Symbol.Offset == 0)
				return new NullValue(t);

			byte[] arrayInfo = Backtrace.BacktraceHelper.ReadBytes(Symbol.Offset, PointerSize * 2);
			ulong arraySize = PointerSize == 8 ? BitConverter.ToUInt64(arrayInfo,0) : (ulong)BitConverter.ToUInt32(arrayInfo,0); 
			ulong firstElement = PointerSize == 8 ? BitConverter.ToUInt64(arrayInfo, 8) : BitConverter.ToUInt32(arrayInfo, 4);

			arraySize = Math.Min(arraySize, DLocalExamBacktrace.MaximumArrayChildrenDisplayCount);

			if (firstElement == 0)
				return new NullValue(t);

			var values = new List<ISymbolValue>();

			if (primitiveValueType != null)
			{
				var tt = primitiveValueType.TypeToken;
				var sz = ExamHelpers.SizeOf(tt, Is64Bit);
				var charBytes = Backtrace.BacktraceHelper.ReadBytes(firstElement, sz * arraySize);

				if (DTokens.IsBasicType_Character(tt))
					return new ArrayValue(t, ExamHelpers.GetStringValue(charBytes, tt));

				for (uint i = 0; i < arraySize; i++)
					values.Add(new PrimitiveValue(ExamHelpers.GetNumericValue(charBytes, (int)i * sz, tt), primitiveValueType));
			}

			return new ArrayValue(t, values.ToArray());
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
