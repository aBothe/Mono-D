using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ExpressionSemantics;
using System.Collections.Generic;
using D_Parser.Resolver.ASTScanner;

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
			if (td is IdentifierDeclaration)
				return HandleDecl(p,(IdentifierDeclaration)td, rr);

			//HACK Ensure that no information gets lost by using this function 
			// -- getting a value but requiring an abstract type and just extract it from the value - is this correct behaviour?
			var at = AbstractType.Get(rr);

			if (td is ArrayDecl)
				return HandleDecl(p,(ArrayDecl)td, at as AssocArrayType);
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
			 * TODO: Scan down r for having at least one templateinstanceexpression as declaration base.
			 * If a tix was found, check if the definition of the respective result base level 
			 * and the un-aliased identifier of the 'tix' parameter match.
			 * Attention: if the alias represents an undeduced type (i.e. a type bundle of equally named type nodes),
			 * it is only important that the definition is inside this bundle.
			 * Therefore, it's needed to manually resolve the identifier, and look out for aliases or such unprecise aliases..confusing as s**t!
			 * 
			 * If the param tix id is part of the template param list, the behaviour is currently undefined! - so instantly return false, I'll leave it as TODO/FIXME
			 */
			var paramTix_TemplateMatchPossibilities = ResolveTemplateInstanceId(tix);
			TemplateIntermediateType tixBasedArgumentType = null;
			var r_ = r as DSymbol;
			while (r_ != null)
			{
				if (r_.DeclarationOrExpressionBase is TemplateInstanceExpression)
				{
					var tit = r_ as TemplateIntermediateType;
					if (tit != null && CheckForTixIdentifierEquality(paramTix_TemplateMatchPossibilities, tit.Definition))
					{
						tixBasedArgumentType = tit;
						break;
					}
				}

				r_ = r_.Base as DSymbol;
			}

			/*
			 * This part is very tricky:
			 * I still dunno what is allowed over here--
			 * 
			 * class Foo(T:Bar!E[],E) {}
			 * ...
			 * Foo!(Bar!string[]) f; -- E will be 'string' then
			 * 
			 * class DerivateBar : Bar!string[] {} -- new Foo!DerivateBar() is also allowed, but now DerivateBar
			 *		obviously is not a template instance expression - it's a normal identifier only. 
			 */
			if (tixBasedArgumentType != null)
			{
				var argEnum_given = ((TemplateInstanceExpression)tixBasedArgumentType.DeclarationOrExpressionBase).Arguments.GetEnumerator();

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

		DNode[] ResolveTemplateInstanceId(TemplateInstanceExpression tix)
		{
			/*
			 * Again a very unclear/buggy situation:
			 * When having a cascaded tix as parameter, it uses the left-most part (i.e. the inner most) of the typedeclaration construct.
			 * 
			 * class C(A!X.SubClass, X) {} can be instantiated via C!(A!int), but not via C!(A!int.SubClass) - totally confusing
			 * (dmd v2.060)
			 */
			if (tix.InnerDeclaration != null)
			{
				if (tix.InnerMost is TemplateInstanceExpression)
					tix = (TemplateInstanceExpression)tix.InnerMost;
				else
					return new DNode[0];
			}

			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.DontResolveBaseClasses | ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.StopAfterFirstOverloads;

			var initialResults = TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdentifier.Id, ctxt, tix);
			var l = _handleResStep(initialResults);

			ctxt.CurrentContext.ContextDependentOptions = optBackup;

			return l.ToArray();
		}

		List<DNode> _handleResStep(AbstractType[] res)
		{
			var l = new List<DNode>();

			if(res!=null)
				foreach (var r in res)
				{
					if (r is AliasedType)
					{
						var alias = (AliasedType)r;
						AbstractType[] next=null;

						ctxt.CurrentContext.ScopedBlock = alias.Definition.Parent as IBlockNode;
						ctxt.CurrentContext.ScopedStatement = null;

						if (alias.Definition.Type is IdentifierDeclaration)
							next = TypeDeclarationResolver.Resolve((IdentifierDeclaration)alias.Definition.Type, ctxt, null, false);
						else
							next = TypeDeclarationResolver.Resolve(alias.Definition.Type, ctxt);

						l.AddRange(_handleResStep(next));
					}
					else if (r is DSymbol)
						l.Add(((DSymbol)r).Definition);
				}

			return l;
		}

		/// <summary>
		/// Returns true if both template instance identifiers are matching each other or if the parameterSpeci
		/// </summary>
		bool CheckForTixIdentifierEquality(
			DNode[] expectedTemplateTypes, 
			INode controllee)
		{
			/*
			 * Note: This implementation is not 100% correct or defined in the D spec:
			 * class A(T){}
			 * class A(S:string) {}
			 * class C(U: A!W, W){ W item; }
			 * 
			 * C!(A!int) -- is possible
			 * C!(A!string) -- is not allowed somehow - because there are probably two 'matching' template types.
			 * (dmd version 2.060, August 2012)
			 */
			return expectedTemplateTypes != null && expectedTemplateTypes.Contains(controllee);
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

		bool HandleDecl(TemplateTypeParameter parameterRef,ArrayDecl arrayDeclToCheckAgainst, AssocArrayType argumentArrayType)
		{
			if (argumentArrayType == null)
				return false;

			// Handle key type
			if((arrayDeclToCheckAgainst.KeyType != null || arrayDeclToCheckAgainst.KeyExpression!=null) && argumentArrayType.KeyType == null)
				return false;
			bool result = false;

			if (arrayDeclToCheckAgainst.KeyExpression != null)
			{
				// Remove all surrounding parentheses from the expression
				var x_param = arrayDeclToCheckAgainst.KeyExpression;

				while(x_param is SurroundingParenthesesExpression)
					x_param = ((SurroundingParenthesesExpression)x_param).Expression;

				var ad_Argument = argumentArrayType.DeclarationOrExpressionBase as ArrayDecl;

				/*
				 * This might be critical:
				 * the [n] part in class myClass(T:char[n], int n) {}
				 * will be seen as an identifier expression, not as an identifier declaration.
				 * So in the case the parameter expression is an identifier,
				 * test if it's part of the parameter list
				 */
				var id = x_param as IdentifierExpression;
				if(id!=null && id.IsIdentifier && Contains((string)id.Value))
				{
					// If an expression (the usual case) has been passed as argument, evaluate its value, otherwise is its type already resolved.
					var finalArg = ad_Argument.KeyExpression != null ?
						Evaluation.EvaluateValue(ad_Argument.KeyExpression, new StandardValueProvider(ctxt)) as ISemantic :
						argumentArrayType.KeyType;

					//TODO: Do a type convertability check between the param type and the given argument's type.
					// The affected parameter must also be a value parameter then, if an expression was given.

					// and handle it as if it was an identifier declaration..
					result = Set(parameterRef, finalArg, (string)id.Value); 
				}
				else if (ad_Argument.KeyExpression != null)
				{
					// Just test for equality of the argument and parameter expression, e.g. if both param and arg are 123, the result will be true.
					result = SymbolValueComparer.IsEqual(arrayDeclToCheckAgainst.KeyExpression, ad_Argument.KeyExpression, new StandardValueProvider(ctxt));
				}
			}
			else if (arrayDeclToCheckAgainst.KeyType != null)
			{
				// If the array we're passing to the decl check that is static (i.e. has a constant number as key 'type'),
				// pass that number instead of type 'int' to the check.
				var at = argumentArrayType as ArrayType;
				if (argumentArrayType != null && at.IsStaticArray)
					result = HandleDecl(parameterRef, arrayDeclToCheckAgainst.KeyType,
						new PrimitiveValue(D_Parser.Parser.DTokens.Int, (decimal)at.FixedLength, null)); 
				else
					result = HandleDecl(parameterRef, arrayDeclToCheckAgainst.KeyType, argumentArrayType.KeyType);
			}

			// Handle inner type
			return result && HandleDecl(parameterRef,arrayDeclToCheckAgainst.InnerDeclaration, argumentArrayType.Base);
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
