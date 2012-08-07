using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateTypeParameter p, ISemantic arg)
		{
			// if no argument given, try to handle default arguments
			if (arg == null)
			{
				if (p.Default == null)
					return false;
				else
				{
					IStatement stmt = null;
					ctxt.PushNewScope(DResolver.SearchBlockAt(ctxt.ScopedBlock.NodeRoot as IBlockNode, p.Default.Location, out stmt));
					ctxt.ScopedStatement = stmt;
					var defaultTypeRes = TypeDeclarationResolver.Resolve(p.Default, ctxt);
					bool b = false;
					if (defaultTypeRes != null)
						b = Set(p, defaultTypeRes.First());
					ctxt.Pop();
					return b;
				}
			}

			// If no spezialization given, assign argument immediately
			if (p.Specialization == null)
				return Set(p, arg);

			bool handleResult= HandleDecl(p,p.Specialization,arg);

			if (!handleResult)
				return false;

			// Apply the entire argument to parameter p if there hasn't been no explicit association yet
			if (!TargetDictionary.ContainsKey(p.Name) || TargetDictionary[p.Name] == null)
				TargetDictionary[p.Name] = new TemplateParameterSymbol(p, arg);

			return true;
		}

		bool HandleDecl(TemplateTypeParameter p ,ITypeDeclaration td, ISemantic rr)
		{
			var at = AbstractType.Get(rr);

			if (td is ArrayDecl)
				return HandleDecl(p,(ArrayDecl)td, at as AssocArrayType);
			else if (td is IdentifierDeclaration)
				return HandleDecl(p,(IdentifierDeclaration)td, at);
			else if (td is DTokenDeclaration)
				return HandleDecl((DTokenDeclaration)td, at);
			else if (td is DelegateDeclaration)
				return HandleDecl(p,(DelegateDeclaration)td, at as DelegateType);
			else if (td is PointerDecl)
				return HandleDecl(p,(PointerDecl)td, at as PointerType);
			else if (td is MemberFunctionAttributeDecl)
				return HandleDecl(p,(MemberFunctionAttributeDecl)td, at);
			else if (td is TypeOfDeclaration)
				return HandleDecl((TypeOfDeclaration)td, at);
			else if (td is VectorDeclaration)
				return HandleDecl((VectorDeclaration)td, at);
			else if (td is TemplateInstanceExpression)
				return HandleDecl(p,(TemplateInstanceExpression)td, at);
			return false;
		}

		bool HandleDecl(TemplateTypeParameter p, IdentifierDeclaration id, ISemantic r)
		{
			// Bottom-level reached
			if (id.InnerDeclaration == null && Contains(id.Id) && !id.ModuleScoped)
			{
				// Associate template param with r
				return Set(p, r, id.Id);
			}

			/*
			 * If not stand-alone identifier or is not required as template param, resolve the id and compare it against r
			 */
			var _r = TypeDeclarationResolver.Resolve(id, ctxt);

			ctxt.CheckForSingleResult(_r, id);

			return _r != null && _r.Length != 0 && 
				(EnforceTypeEqualityWhenDeducing ?
				ResultComparer.IsEqual(r,_r[0]) :
				ResultComparer.IsImplicitlyConvertible(r,_r[0]));
		}

		bool HandleDecl(TemplateTypeParameter parameter, TemplateInstanceExpression tix, AbstractType r)
		{
			/*
			 * This part is very tricky:
			 * I still dunno what is allowed over here--
			 * 
			 * class Foo(T:Bar!E[],E) {}
			 * ...
			 * Foo!(Bar!string[]) f; -- E will be 'string' then
			 */
			if (r.DeclarationOrExpressionBase is TemplateInstanceExpression)
			{
				var tix_given = (TemplateInstanceExpression)r.DeclarationOrExpressionBase;

				// Template type Ids must be equal (?)
				if (tix.TemplateIdentifier.ToString() != tix_given.TemplateIdentifier.ToString())
					return false;

				var thHandler = new TemplateParameterDeduction(TargetDictionary, ctxt);

				var argEnum_given = tix_given.Arguments.GetEnumerator();
				foreach (var p in tix.Arguments)
				{
					if (!argEnum_given.MoveNext() || argEnum_given.Current == null)
						return false;

					// Convert p to type declaration
					var param_Expected = ConvertToTypeDeclarationRoughly(p);

					if (param_Expected == null)
						return false;

					var result_Given = Evaluation.EvaluateType(argEnum_given.Current as IExpression, ctxt);

					if (result_Given == null || !HandleDecl(parameter, param_Expected, result_Given))
						return false;
				}

				// Too many params passed..
				if (argEnum_given.MoveNext())
					return false;

				return true;
			}

			return false;
		}

		static ITypeDeclaration ConvertToTypeDeclarationRoughly(IExpression p)
		{
			if (p is IdentifierExpression)
				return new IdentifierDeclaration(((IdentifierExpression)p).Value as string) { Location = p.Location, EndLocation = p.EndLocation };
			else if (p is TypeDeclarationExpression)
				return ((TypeDeclarationExpression)p).Declaration;
			return null;
		}

		static bool HandleDecl(DTokenDeclaration tk, AbstractType r)
		{
			if (r is PrimitiveType)
				return tk.Token == ((PrimitiveType)r).TypeToken;

			return false;
		}

		bool HandleDecl(TemplateTypeParameter p,ArrayDecl ad, AssocArrayType ar)
		{
			if (ar == null)
				return false;

			// Handle key type
			if((ad.KeyType != null || ad.KeyExpression!=null) && ar.KeyType == null)
				return false;
			bool result = false;

			if (ad.KeyExpression != null)
			{
				var arrayDecl_Param = ar.DeclarationOrExpressionBase as ArrayDecl;
				if (arrayDecl_Param.KeyExpression != null)
					result = SymbolValueComparer.IsEqual(ad.KeyExpression, arrayDecl_Param.KeyExpression, new StandardValueProvider(ctxt));
			}
			else if(ad.KeyType!=null)
				result = HandleDecl(p,ad.KeyType, ar.KeyType);

			// Handle inner type
			return result && HandleDecl(p,ad.InnerDeclaration, ar.Base);
		}

		bool HandleDecl(TemplateTypeParameter par, DelegateDeclaration d, DelegateType dr)
		{
			// Delegate literals or other expressions are not allowed
			if(dr==null || dr.IsFunctionLiteral)
				return false;

			var dr_decl = (DelegateDeclaration)dr.DeclarationOrExpressionBase;

			// Compare return types
			if(	d.IsFunction == dr_decl.IsFunction &&
				dr.ReturnType != null &&
				HandleDecl(par, d.ReturnType,dr.ReturnType))
			{
				// If no delegate args expected, it's valid
				if ((d.Parameters == null || d.Parameters.Count == 0) &&
					dr_decl.Parameters == null || dr_decl.Parameters.Count == 0)
					return true;

				// If parameter counts unequal, return false
				else if (d.Parameters == null || dr_decl.Parameters == null || d.Parameters.Count != dr_decl.Parameters.Count)
					return false;

				// Compare & Evaluate each expected with given parameter
				var dr_paramEnum = dr_decl.Parameters.GetEnumerator();
				foreach (var p in d.Parameters)
				{
					// Compare attributes with each other
					if (p is DNode)
					{
						if (!(dr_paramEnum.Current is DNode))
							return false;

						var dn = (DNode)p;
						var dn_arg = (DNode)dr_paramEnum.Current;

						if ((dn.Attributes == null || dn.Attributes.Count == 0) &&
							(dn_arg.Attributes == null || dn_arg.Attributes.Count == 0))
							return true;

						else if (dn.Attributes == null || dn_arg.Attributes == null ||
							dn.Attributes.Count != dn_arg.Attributes.Count)
							return false;

						foreach (var attr in dn.Attributes)
						{
							if (attr.IsProperty ?
								!dn_arg.ContainsPropertyAttribute(attr.LiteralContent as string) :
								!dn_arg.ContainsAttribute(attr.Token))
								return false;
						}
					}

					// Compare types
					if (p.Type!=null && dr_paramEnum.MoveNext() && dr_paramEnum.Current.Type!=null)
					{
						var dr_resolvedParamType = TypeDeclarationResolver.Resolve(dr_paramEnum.Current.Type, ctxt);

						ctxt.CheckForSingleResult(dr_resolvedParamType, dr_paramEnum.Current.Type);

						if (dr_resolvedParamType == null ||
							dr_resolvedParamType.Length == 0 ||
							!HandleDecl(par, p.Type, dr_resolvedParamType[0]))
							return false;
					}
					else
						return false;
				}
			}

			return false;
		}

		bool HandleDecl(TemplateTypeParameter parameter, PointerDecl p, PointerType r)
		{
			return r != null && 
				r.DeclarationOrExpressionBase is PointerDecl && 
				HandleDecl(parameter, p.InnerDeclaration, r.Base);
		}

		bool HandleDecl(TemplateTypeParameter p, MemberFunctionAttributeDecl m, AbstractType r)
		{
			if (r == null || r.Modifier == 0)
				return false;

			// Modifiers must be equal on both sides
			if (m.Modifier != r.Modifier)
				return false;
				
			// Now compare the type inside the parentheses with the given type 'r'
			return m.InnerType != null && HandleDecl(p, m.InnerType, r);
		}

		bool HandleDecl(TypeOfDeclaration t, AbstractType r)
		{
			// Can I enter some template parameter referencing id into a typeof specialization!?
			// class Foo(T:typeof(1)) {} ?
			var t_res = TypeDeclarationResolver.Resolve(t,ctxt);

			if (t_res == null)
				return false;

			return ResultComparer.IsImplicitlyConvertible(r,t_res);
		}

		bool HandleDecl(VectorDeclaration v, AbstractType r)
		{
			if (r.DeclarationOrExpressionBase is VectorDeclaration)
			{
				var v_res = Evaluation.EvaluateType(v.Id,ctxt);
				var r_res = Evaluation.EvaluateType(((VectorDeclaration)r.DeclarationOrExpressionBase).Id,ctxt);

				if (v_res == null || r_res == null)
					return false;
				else
					return ResultComparer.IsImplicitlyConvertible(r_res, v_res);
			}
			return false;
		}
	}
}
