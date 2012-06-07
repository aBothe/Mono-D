using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;

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
		public static bool IsEqual(ResolveResult r1, ResolveResult r2)
		{
			if (r1 is TemplateInstanceResult && r2 is TemplateInstanceResult)
			{
				var tr1 = (TemplateInstanceResult)r1;
				var tr2 = (TemplateInstanceResult)r2;

				if (tr1.Node != tr2.Node)
					return false;

				//TODO: Compare deduced types
				return true;
			}
			else if (r1 is StaticTypeResult && r2 is StaticTypeResult)
				return ((StaticTypeResult)r1).BaseTypeToken == ((StaticTypeResult)r2).BaseTypeToken;
			else if (r1 is ArrayResult && r2 is ArrayResult)
			{
				var ar1 = (ArrayResult)r1;
				var ar2 = (ArrayResult)r2;

				if (!IsEqual(ar1.KeyType[0], ar2.KeyType[0]))
					return false;

				return IsEqual(ar1.ResultBase, ar2.ResultBase);
			}

			//TODO: Handle other types

			return false;
		}

		/// <summary>
		/// Checks results for implicit type convertability 
		/// </summary>
		public static bool IsImplicitlyConvertible(ResolveResult resultToCheck, ResolveResult targetType, ResolverContextStack ctxt=null)
		{
			// Initially remove aliases from results
			bool resMem = false;
			var _r=DResolver.ResolveMembersFromResult(new[]{resultToCheck},out resMem);
			if(_r==null || _r.Length==0)
				return IsEqual(resultToCheck,targetType);
			resultToCheck = _r[0];

			_r=DResolver.ResolveMembersFromResult(new[]{targetType}, out resMem);
			if(_r==null || _r.Length == 0)
				return false;
			targetType = _r[0];


			if (targetType is MemberResult)
			{
				var mr2 = (MemberResult)targetType;

				if (mr2.Node is TemplateParameterNode)
				{
					var tpn = (TemplateParameterNode)mr2.Node;

					var dedParam=new Dictionary<string, ResolveResult[]>();
					foreach(var tp in tpn.Owner.TemplateParameters)
						dedParam[tp.Name]=null;

					return new TemplateParameterDeduction(dedParam, ctxt).Handle(tpn.TemplateParameter, resultToCheck);
				}
			}

			if (resultToCheck is StaticTypeResult && targetType is StaticTypeResult)
			{
				var sr1 = (StaticTypeResult)resultToCheck;
				var sr2 = (StaticTypeResult)targetType;

				if (sr1.BaseTypeToken == sr2.BaseTypeToken)
					return true;

				switch (sr2.BaseTypeToken)
				{
					case DTokens.Int:
						return sr1.BaseTypeToken == DTokens.Uint;
					case DTokens.Uint:
						return sr1.BaseTypeToken == DTokens.Int;
					//TODO: Further types that can be converted into each other implicitly
				}
			}
			else if (resultToCheck is TypeResult && targetType is TypeResult)
				return IsImplicitlyConvertible((TypeResult)resultToCheck, (TypeResult)targetType);
			else if (resultToCheck is DelegateResult && targetType is DelegateResult)
			{
				//TODO
			}
			else if (resultToCheck is ArrayResult && targetType is ArrayResult)
			{
				var ar1 = (ArrayResult)resultToCheck;
				var ar2 = (ArrayResult)targetType;

				// Key as well as value types must be matching!
				var ar1_n= ar1.KeyType==null || ar1.KeyType.Length == 0;
				var ar2_n=ar2.KeyType==null || ar2.KeyType.Length == 0;

				if (ar1_n != ar2_n)
					return false;

				if(ar1_n || IsImplicitlyConvertible(ar1.KeyType[0], ar2.KeyType[0], ctxt))
					return IsImplicitlyConvertible(ar1.ResultBase, ar2.ResultBase, ctxt);
			}

			else if (resultToCheck is TypeTupleResult && targetType is TypeTupleResult)
			{
				return true;
			}
			else if (resultToCheck is ExpressionTupleResult && targetType is ExpressionTupleResult)
			{
				return true;
			}
			else if (resultToCheck is ExpressionValueResult && targetType is ExpressionValueResult)
			{
				return Evaluation.ExpressionEvaluator.IsEqual(((ExpressionValueResult)resultToCheck).Value, ((ExpressionValueResult)targetType).Value);
			}

			// http://dlang.org/type.html
			//TODO: Pointer to non-pointer / vice-versa checkability? -- Can it really be done implicitly?

			return false;
		}

		public static bool IsImplicitlyConvertible(TypeResult r, TypeResult target)
		{
			if (r.Node == target.Node)
				return true;

			if (r.BaseClass != null && r.BaseClass.Length != 0)
			{
				if (IsImplicitlyConvertible(r.BaseClass[0], target))
					return true;
			}

			if (r.ImplementedInterfaces != null && 
				r.ImplementedInterfaces.Length != 0 &&
				target.Node is DClassLike &&
				((DClassLike)target.Node).ClassType == DTokens.Interface)
			{
				foreach(var I in r.ImplementedInterfaces)
					if(IsImplicitlyConvertible(I[0], target))
						return true;
			}

			return false;
		}
	}
}
