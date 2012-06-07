using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateTypeParameter p, ResolveResult arg)
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
						b = Set(p.Name, defaultTypeRes.First());
					ctxt.Pop();
					return b;
				}
			}

			// If no spezialization given, assign argument immediately
			if (p.Specialization == null)
				return Set(p.Name, arg);

			bool handleResult= HandleDecl(p.Specialization,arg);

			if (!handleResult)
				return false;

			// Apply the entire argument to parameter p if there hasn't been no explicit association yet
			if (!TargetDictionary.ContainsKey(p.Name) || TargetDictionary[p.Name] == null || TargetDictionary[p.Name].Length == 0)
				TargetDictionary[p.Name] = new[]{ arg };

			return true;
		}

		bool HandleDecl(ITypeDeclaration td, ResolveResult rr)
		{
			if (td is ArrayDecl)
				return HandleDecl((ArrayDecl)td, rr);
			else if (td is IdentifierDeclaration)
				return HandleDecl((IdentifierDeclaration)td, rr);
			else if (td is DTokenDeclaration)
				return HandleDecl((DTokenDeclaration)td, rr);
			else if (td is DelegateDeclaration)
				return HandleDecl((DelegateDeclaration)td, rr);
			else if (td is PointerDecl)
				return HandleDecl((PointerDecl)td, rr);
			else if (td is MemberFunctionAttributeDecl)
				return HandleDecl((MemberFunctionAttributeDecl)td, rr);
			else if (td is TypeOfDeclaration)
				return HandleDecl((TypeOfDeclaration)td, rr);
			else if (td is VectorDeclaration)
				return HandleDecl((VectorDeclaration)td, rr);
			else if (td is TemplateInstanceExpression)
				return HandleDecl((TemplateInstanceExpression)td,rr);
			return false;
		}

		bool HandleDecl(IdentifierDeclaration id, ResolveResult r)
		{
			// Bottom-level reached
			if (id.InnerDeclaration == null && Contains(id.Id) && !id.ModuleScoped)
			{
				// Associate template param with r
				return Set(id.Id, r);
			}

			/*
			 * If not stand-alone identifier or is not required as template param, resolve the id and compare it against r
			 */
			var _r = TypeDeclarationResolver.Resolve(id, ctxt);
			return _r == null || _r.Length == 0 || 
				ResultComparer.IsImplicitlyConvertible(r,_r[0]);
		}

		bool HandleDecl(TemplateInstanceExpression tix, ResolveResult r)
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

					var result_Given = ExpressionTypeResolver.Resolve(argEnum_given.Current as IExpression, ctxt);

					if (result_Given == null || result_Given.Length == 0)
						return false;

					if (!HandleDecl(param_Expected, result_Given[0]))
						return false;
				}

				// Too many params passed..
				if (argEnum_given.MoveNext())
					return false;

				return true;
			}

			return false;
		}

		ITypeDeclaration ConvertToTypeDeclarationRoughly(IExpression p)
		{
			if (p is IdentifierExpression)
				return new IdentifierDeclaration(((IdentifierExpression)p).Value as string) { Location = p.Location, EndLocation = p.EndLocation };
			else if (p is TypeDeclarationExpression)
				return ((TypeDeclarationExpression)p).Declaration;
			return null;
		}

		bool HandleDecl(DTokenDeclaration tk, ResolveResult r)
		{
			if (r is StaticTypeResult && r.DeclarationOrExpressionBase is DTokenDeclaration)
				return tk.Token == ((DTokenDeclaration)r.DeclarationOrExpressionBase).Token;

			return false;
		}

		bool HandleDecl(ArrayDecl ad, ResolveResult r)
		{
			if (r is ArrayResult)
			{
				var ar = (ArrayResult)r;

				// Handle key type
				if((ad.KeyType != null || ad.KeyExpression!=null)&& (ar.KeyType == null || ar.KeyType.Length == 0))
					return false;
				bool result = false;

				if (ad.KeyExpression != null)
				{
					if (ar.ArrayDeclaration.KeyExpression != null)
						result = Evaluation.ExpressionEvaluator.IsEqual(ad.KeyExpression, ar.ArrayDeclaration.KeyExpression, ctxt);
				}
				else if(ad.KeyType!=null)
					result = HandleDecl(ad.KeyType, ar.KeyType[0]);

				if (!result)
					return false;

				// Handle inner type
				return HandleDecl(ad.InnerDeclaration, ar.ResultBase);
			}

			return false;
		}

		bool HandleDecl(DelegateDeclaration d, ResolveResult r)
		{
			if (r is DelegateResult)
			{
				var dr = (DelegateResult)r;

				// Delegate literals or other expressions are not allowed
				if(!dr.IsDelegateDeclaration)
					return false;

				var dr_decl = (DelegateDeclaration)dr.DeclarationOrExpressionBase;

				// Compare return types
				if(	d.IsFunction == dr_decl.IsFunction &&
					dr.ReturnType!=null && 
					dr.ReturnType.Length!=0 && 
					HandleDecl(d.ReturnType,dr.ReturnType[0]))
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

							if (dr_resolvedParamType == null ||
								dr_resolvedParamType.Length == 0 ||
								!HandleDecl(p.Type, dr_resolvedParamType[0]))
								return false;
						}
						else
							return false;
					}
				}
			}

			return false;
		}

		bool HandleDecl(PointerDecl p, ResolveResult r)
		{
			if (r is StaticTypeResult && r.DeclarationOrExpressionBase is PointerDecl)
			{
				return HandleDecl(p.InnerDeclaration, r.ResultBase);
			}

			return false;
		}

		bool HandleDecl(MemberFunctionAttributeDecl m, ResolveResult r)
		{
			if (r is StaticTypeResult && r.DeclarationOrExpressionBase is MemberFunctionAttributeDecl)
			{
				var r_m = (MemberFunctionAttributeDecl)r.DeclarationOrExpressionBase;

				if (m.Modifier != r_m.Modifier ||
					(m.InnerType == null && r_m.InnerType != null) ||
					(m.InnerType != null && r_m.InnerType == null))
					return false;

				if (m.InnerType != null)
				{
					var r_m_innerTypeResult = TypeDeclarationResolver.Resolve(r_m.InnerType, ctxt);
					if (r_m_innerTypeResult == null || r_m_innerTypeResult.Length == 0)
						return false;

					if (!HandleDecl(m.InnerType, r_m_innerTypeResult[0]))
						return false;
				}

				return m.InnerDeclaration!=null ? HandleDecl(m.InnerDeclaration, r.ResultBase) : true;
			}

			return false;
		}

		bool HandleDecl(TypeOfDeclaration t, ResolveResult r)
		{
			// Can I enter some template parameter referencing id into a typeof specialization!?
			// class Foo(T:typeof(1)) {} ?
			var t_res = TypeDeclarationResolver.Resolve(t,ctxt);

			if (t_res == null || t_res.Length == 0)
				return false;

			return ResultComparer.IsImplicitlyConvertible(r,t_res[0]);
		}

		bool HandleDecl(VectorDeclaration v, ResolveResult r)
		{
			if (r.DeclarationOrExpressionBase is VectorDeclaration)
			{
				var v_res = ExpressionTypeResolver.Resolve( v.Id,ctxt);
				var r_res = ExpressionTypeResolver.Resolve(((VectorDeclaration)r.DeclarationOrExpressionBase).Id,ctxt);

				if (v_res == null || v_res.Length == 0 || r_res == null || r_res.Length == 0)
					return false;
				else
					return ResultComparer.IsImplicitlyConvertible(r_res[0], v_res[0]);
			}
			return false;
		}
	}
}
