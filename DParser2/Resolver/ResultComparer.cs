using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Provides methods to check if resolved types are matching each other and/or can be converted into each other.
	/// Used for UFCS completion, argument-parameter matching, template parameter deduction
	/// </summary>
	public class ResultComparer
	{
		/// <summary>
		/// Checks given results for type equality
		/// </summary>
		public static bool IsEqual(ISemantic r1, ISemantic r2)
		{
			if (r1 is ISymbolValue && r2 is ISymbolValue)
				return SymbolValueComparer.IsEqual((ISymbolValue)r1, (ISymbolValue)r2);

			else if (r1 is TemplateIntermediateType && r2 is TemplateIntermediateType)
			{
				var tr1 = (TemplateIntermediateType)r1;
				var tr2 = (TemplateIntermediateType)r2;

				if (tr1.Definition != tr2.Definition)
					return false;

				//TODO: Compare deduced types
				return true;
			}
			else if (r1 is PrimitiveType && r2 is PrimitiveType)
				return ((PrimitiveType)r1).TypeToken == ((PrimitiveType)r2).TypeToken;
			else if (r1 is ArrayType && r2 is ArrayType)
			{
				var ar1 = (ArrayType)r1;
				var ar2 = (ArrayType)r2;

				if (!IsEqual(ar1.KeyType, ar2.KeyType))
					return false;

				return IsEqual(ar1.Base, ar2.Base);
			}

			//TODO: Handle other types

			return false;
		}

		/// <summary>
		/// Checks results for implicit type convertability 
		/// </summary>
		public static bool IsImplicitlyConvertible(ISemantic resultToCheck, AbstractType targetType, ResolverContextStack ctxt=null)
		{
			var resToCheck = AbstractType.Get(resultToCheck);

			// Initially remove aliases from results
			var _r=DResolver.StripMemberSymbols(resToCheck);
			if(_r==null)
				return IsEqual(resToCheck,targetType);
			resToCheck = _r;

			targetType = DResolver.StripAliasSymbol(targetType);

			if (targetType is DSymbol)
			{
				var tpn = ((DSymbol)targetType).Definition as TemplateParameterNode;

				if (tpn!=null)
				{
					var par = tpn.Parent as DNode;

					if (par != null && par.TemplateParameters != null)
					{
						var dedParam = new DeducedTypeDictionary { ParameterOwner=par };
						foreach (var tp in par.TemplateParameters)
							dedParam[tp.Name] = null;

						return new TemplateParameterDeduction(dedParam, ctxt).Handle(tpn.TemplateParameter, resToCheck);
					}
				}
			}

			_r = DResolver.StripMemberSymbols(targetType);
			if (_r == null)
				return false;
			targetType = _r;

			if (resToCheck is PrimitiveType && targetType is PrimitiveType)
			{
				var sr1 = (PrimitiveType)resToCheck;
				var sr2 = (PrimitiveType)targetType;

				if (sr1.TypeToken == sr2.TypeToken && sr1.Modifier == sr2.Modifier)
					return true;

				switch (sr2.TypeToken)
				{
					case DTokens.Int:
						return sr1.TypeToken == DTokens.Uint;
					case DTokens.Uint:
						return sr1.TypeToken == DTokens.Int;
					//TODO: Further types that can be converted into each other implicitly
				}
			}
			else if (resToCheck is UserDefinedType && targetType is UserDefinedType)
				return IsImplicitlyConvertible((UserDefinedType)resToCheck, (UserDefinedType)targetType);
			else if (resToCheck is DelegateType && targetType is DelegateType)
			{
				//TODO
			}
			else if (resToCheck is ArrayType && targetType is ArrayType)
			{
				var ar1 = (ArrayType)resToCheck;
				var ar2 = (ArrayType)targetType;

				// Key as well as value types must be matching!
				var ar1_n= ar1.KeyType==null;
				var ar2_n=ar2.KeyType==null;

				if (ar1_n != ar2_n)
					return false;

				if(ar1_n || IsImplicitlyConvertible(ar1.KeyType, ar2.KeyType, ctxt))
					return IsImplicitlyConvertible(ar1.Base, ar2.Base, ctxt);
			}

			else if (resToCheck is TypeTuple && targetType is TypeTuple)
			{
				return true;
			}
			else if (resToCheck is ExpressionTuple && targetType is ExpressionTuple)
			{
				return true;
			}
			/*else if (resultToCheck is ExpressionValueResult && targetType is ExpressionValue)
			{
				return ((ExpressionValueResult)resultToCheck).Value.Equals(((ExpressionValueResult)targetType).Value);
			}*/

			// http://dlang.org/type.html
			//TODO: Pointer to non-pointer / vice-versa checkability? -- Can it really be done implicitly?

			return false;
		}

		public static bool IsImplicitlyConvertible(UserDefinedType r, UserDefinedType target)
		{
			if (r == null || target == null)
				return false;

			if (r.Definition == target.Definition)
				return true;

			if (r.Base != null && IsImplicitlyConvertible(r.Base, target))
				return true;

			if (r is TemplateIntermediateType)
			{
				var templateType = (TemplateIntermediateType)r;

				if (templateType.BaseInterfaces != null &&
					templateType.BaseInterfaces.Length != 0 &&
					target is InterfaceType)
				{
					foreach (var I in templateType.BaseInterfaces)
						if (IsImplicitlyConvertible(I, target))
							return true;
				}
			}

			return false;
		}
	}
}
