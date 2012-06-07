using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Resolver.TypeResolution
{
	public partial class TypeDeclarationResolver
	{
		public static ResolveResult Resolve(DTokenDeclaration token)
		{
			var tk = (token as DTokenDeclaration).Token;

			if (DTokens.BasicTypes[tk])
				return new StaticTypeResult
						{
							BaseTypeToken = tk,
							DeclarationOrExpressionBase = token
						};

			return null;
		}

		public static ResolveResult[] ResolveIdentifier(string id, ResolverContextStack ctxt, object idObject, bool ModuleScope = false)
		{
			var loc = idObject is ISyntaxRegion ? ((ISyntaxRegion)idObject).Location:CodeLocation.Empty;
			ResolveResult[] res = null;

			if (ModuleScope)
				ctxt.PushNewScope(ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree);

			// If there are symbols that must be preferred, take them instead of scanning the ast
			else
			{
				var tstk = new Stack<ResolverContext>();

				while (!ctxt.CurrentContext.DeducedTemplateParameters.TryGetValue(id, out res))
				{
					if (ctxt.PrevContextIsInSameHierarchy)
						tstk.Push(ctxt.Pop());
					else
						break;
				}

				while (tstk.Count > 0)
					ctxt.Push(tstk.Pop());

				if (res != null)
					return res;
			}

			var matches = NameScan.SearchMatchesAlongNodeHierarchy(ctxt, loc, id);

			res= HandleNodeMatches(matches, ctxt, null, idObject);

			if (ModuleScope)
				ctxt.Pop();

			return res;
		}

		public static ResolveResult[] Resolve(IdentifierDeclaration id, ResolverContextStack ctxt, ResolveResult[] resultBases = null, bool filterForTemplateArgs = true)
		{
			ResolveResult[] res = null;

			if (id.InnerDeclaration == null && resultBases == null)
				res= ResolveIdentifier(id.Id, ctxt, id, id.ModuleScoped);
			else
			{
				var rbases = resultBases ?? Resolve(id.InnerDeclaration, ctxt);

				if (rbases == null || rbases.Length == 0)
					return null;

				res= ResolveFurtherTypeIdentifier(id.Id, rbases, ctxt, id);
			}

			return (filterForTemplateArgs && !ctxt.Options.HasFlag(ResolutionOptions.NoTemplateParameterDeduction)) ? 
				TemplateInstanceHandler.EvalAndFilterOverloads(res, null, false, ctxt) : res;
		}

		/// <summary>
		/// Used for searching further identifier list parts.
		/// 
		/// a.b -- nextIdentifier would be 'b' whereas <param name="resultBases">resultBases</param> contained the resolution result for 'a'
		/// </summary>
		public static ResolveResult[] ResolveFurtherTypeIdentifier(string nextIdentifier,
			IEnumerable<ResolveResult> resultBases,
			ResolverContextStack ctxt,
			object typeIdObject=null)
		{
			if((resultBases = DResolver.TryRemoveAliasesFromResult(resultBases))==null)
				return null;

			var r = new List<ResolveResult>();

			var nextResults = new List<ResolveResult>();
			foreach (var b in resultBases)
			{
				IEnumerable<ResolveResult> scanResults = new[]{ b };

				do
				{
					foreach (var scanResult in scanResults)
					{
						// First filter out all alias and member results..so that there will be only (Static-)Type or Module results left..
						if (scanResult is MemberResult)
						{
							var mr = scanResult as MemberResult;

							if (mr.MemberBaseTypes != null)
								nextResults.AddRange(mr.MemberBaseTypes);
						}

						else if (scanResult is TypeResult)
						{
							var bn = ((TypeResult)scanResult).Node as IBlockNode;
							var nodeMatches = NameScan.ScanNodeForIdentifier(bn, nextIdentifier, ctxt);

							ctxt.PushNewScope(bn);

							var results = HandleNodeMatches(nodeMatches, ctxt, b, typeIdObject);

							if (results != null)
								r.AddRange(results);

							ctxt.Pop();
						}
						else if (scanResult is ModulePackageResult)
						{
							var pack=((ModulePackageResult)scanResult).Package;

							IAbstractSyntaxTree accessedModule=null;
							if (pack.Modules.TryGetValue(nextIdentifier, out accessedModule))
								r.Add(new ModuleResult(accessedModule)
								{
									ResultBase = scanResult,
									DeclarationOrExpressionBase = typeIdObject
								});
							else if (pack.Packages.TryGetValue(nextIdentifier, out pack))
								r.Add(new ModulePackageResult(pack)
								{
									ResultBase=scanResult,
									DeclarationOrExpressionBase=typeIdObject
								});
						}
						else if (scanResult is ModuleResult)
						{
							var modRes = (ModuleResult)scanResult;

							var matches = NameScan.ScanNodeForIdentifier(modRes.Module, nextIdentifier, ctxt);

							var results = HandleNodeMatches(matches, ctxt, b, typeIdObject);

							if (results != null)
								r.AddRange(results);
						}
					}

					scanResults = DResolver.FilterOutByResultPriority(ctxt, nextResults);
					nextResults = new List<ResolveResult>();
				}
				while (scanResults != null);
			}

			return r.Count == 0 ? null : r.ToArray();
		}

		public static ResolveResult[] Resolve(TypeOfDeclaration typeOf, ResolverContextStack ctxt)
		{
			// typeof(return)
			if (typeOf.InstanceId is TokenExpression && (typeOf.InstanceId as TokenExpression).Token == DTokens.Return)
			{
				var m = HandleNodeMatch(ctxt.ScopedBlock, ctxt, null, typeOf);
				if (m != null)
					return new[] { m };
			}
			// typeOf(myInt)  =>  int
			else if (typeOf.InstanceId != null)
			{
				var wantedTypes = ExpressionTypeResolver.Resolve(typeOf.InstanceId, ctxt);

				if (wantedTypes == null)
					return null;

				// Scan down for variable's base types
				var c1 = new List<ResolveResult>(wantedTypes);
				var c2 = new List<ResolveResult>();

				var ret = new List<ResolveResult>();

				while (c1.Count > 0)
				{
					foreach (var t in c1)
					{
						if (t is MemberResult)
						{
							if ((t as MemberResult).MemberBaseTypes != null)
								c2.AddRange((t as MemberResult).MemberBaseTypes);
						}
						else
						{
							t.DeclarationOrExpressionBase = typeOf;
							ret.Add(t);
						}
					}

					c1.Clear();
					c1.AddRange(c2);
					c2.Clear();
				}

				return ret.ToArray();
			}

			return null;
		}

		public static ResolveResult[] Resolve(MemberFunctionAttributeDecl attrDecl, ResolverContextStack ctxt)
		{
			var ret = Resolve(attrDecl.InnerType, ctxt);

			if (ret != null)
				foreach (var r in ret)
					if(r!=null)
						r.DeclarationOrExpressionBase = attrDecl;

			return ret;
		}

		public static ResolveResult[] Resolve(ArrayDecl ad, ResolverContextStack ctxt)
		{
			var valueTypes = Resolve(ad.ValueType, ctxt);

			ResolveResult[] keyTypes = null;

			if (ad.KeyExpression != null)
				keyTypes = ExpressionTypeResolver.Resolve(ad.KeyExpression, ctxt);
			else
				keyTypes = Resolve(ad.KeyType, ctxt);

			if (valueTypes == null)
				return new[] { new ArrayResult { 
					ArrayDeclaration = ad,
					KeyType=keyTypes
				}};

			var r = new List<ResolveResult>(valueTypes.Length);

			foreach (var valType in valueTypes)
				r.Add(new ArrayResult { 
					ArrayDeclaration = ad,
					ResultBase=valType,
					KeyType=keyTypes
				});

			return r.ToArray();
		}

		public static ResolveResult[] Resolve(PointerDecl pd, ResolverContextStack ctxt)
		{
			var ptrBaseTypes = Resolve(pd.InnerDeclaration, ctxt);

			if (ptrBaseTypes == null)
				return new[] { 
					new StaticTypeResult{ DeclarationOrExpressionBase=pd}
				};

			var r = new List<ResolveResult>();

			foreach (var t in ptrBaseTypes)
				r.Add(new StaticTypeResult { 
					DeclarationOrExpressionBase=pd,
					ResultBase=t
				});

			return r.ToArray();
		}

		public static ResolveResult[] Resolve(DelegateDeclaration dg, ResolverContextStack ctxt)
		{
			var r = new DelegateResult { DeclarationOrExpressionBase=dg };

			if(!ctxt.Options.HasFlag(ResolutionOptions.DontResolveBaseTypes))
				r.ReturnType = Resolve(dg.ReturnType, ctxt);

			return new[] { r };
		}

		public static ResolveResult[] Resolve(ITypeDeclaration declaration, ResolverContextStack ctxt)
		{
			if (declaration is DTokenDeclaration)
			{
				var r = Resolve(declaration as DTokenDeclaration);

				if (r != null)
					return new[] { r };
			}
			else if (declaration is IdentifierDeclaration)
				return Resolve(declaration as IdentifierDeclaration, ctxt);
			else if (declaration is TemplateInstanceExpression)
				return ExpressionTypeResolver.Resolve(declaration as TemplateInstanceExpression, ctxt);
			else if (declaration is TypeOfDeclaration)
				return Resolve(declaration as TypeOfDeclaration, ctxt);
			else if (declaration is MemberFunctionAttributeDecl)
				return Resolve(declaration as MemberFunctionAttributeDecl, ctxt);
			else if (declaration is ArrayDecl)
				return Resolve(declaration as ArrayDecl, ctxt);
			else if (declaration is PointerDecl)
				return Resolve(declaration as PointerDecl, ctxt);
			else if (declaration is DelegateDeclaration)
				return Resolve(declaration as DelegateDeclaration, ctxt);
			
			//TODO: VarArgDeclaration
			else if (declaration is ITemplateParameterDeclaration)
			{
				var tpd = declaration as ITemplateParameterDeclaration;

				var templateParameter = tpd.TemplateParameter;

				//TODO: Is this correct handling?
				while (templateParameter is TemplateThisParameter)
					templateParameter = (templateParameter as TemplateThisParameter).FollowParameter;

				if (tpd.TemplateParameter is TemplateValueParameter)
				{
					// Return a member result -- it's a static variable
				}
				else
				{
					// Return a type result?
				}
			}

			return null;
		}







		#region Intermediate methods
		/// <summary>
		/// The variable's or method's base type will be resolved (if auto type, the intializer's type will be taken).
		/// A class' base class will be searched.
		/// etc..
		/// </summary>
		public static ResolveResult HandleNodeMatch(
			INode m,
			ResolverContextStack ctxt,
			ResolveResult resultBase = null,
			object typeBase = null)
		{
			stackNum_HandleNodeMatch++;

			bool popAfterwards = false;
			if (popAfterwards = (m.Parent != ctxt.ScopedBlock && m.Parent is IBlockNode))
				ctxt.PushNewScope((IBlockNode)m.Parent);

			//HACK: Really dirty stack overflow prevention via manually counting call depth
			var DoResolveBaseType = 
				!(m is DClassLike && m.Name == "Object") && 
				!ctxt.Options.HasFlag(ResolutionOptions.DontResolveBaseClasses) &&
				stackNum_HandleNodeMatch <= 5;

			// Prevent infinite recursion if the type accidently equals the node's name
			if (m.Type != null && m.Type.ToString(false) == m.Name)
				DoResolveBaseType = false;

			ResolveResult ret = null;
			ResolveResult[] memberbaseTypes = null;

			// To support resolving type parameters to concrete types if the context allows this, introduce all deduced parameters to the current context
			if (DoResolveBaseType && resultBase is TemplateInstanceResult)
				ctxt.CurrentContext.IntroduceTemplateParameterTypes((TemplateInstanceResult)resultBase);

			// Only import symbol aliases are allowed to search in the parse cache
			if (m is ImportSymbolAlias)
			{
				var isa = (ImportSymbolAlias)m;

				if (isa.IsModuleAlias ? isa.Type != null : isa.Type.InnerDeclaration != null)
				{
					var alias = new AliasResult	{ Node = m };

					var mods = new List<ResolveResult>();
					foreach (var mod in ctxt.ParseCache.LookupModuleName(isa.IsModuleAlias ?
						isa.Type.ToString() :
						isa.Type.InnerDeclaration.ToString()))
						mods.Add(new ModuleResult(mod));

					if (isa.IsModuleAlias)
						alias.MemberBaseTypes = mods.ToArray();
					else
						alias.MemberBaseTypes = ResolveFurtherTypeIdentifier(isa.Type.ToString(false), mods, ctxt, isa.Type);

					ret = alias;
				}
			}
			else if (m is DVariable)
			{
				var v = (DVariable)m;

				if (DoResolveBaseType)
				{
					memberbaseTypes = TypeDeclarationResolver.Resolve(v.Type, ctxt);

					// For auto variables, use the initializer to get its type
					if (memberbaseTypes == null && v.Initializer != null)
						memberbaseTypes = ExpressionTypeResolver.Resolve(v.Initializer, ctxt);

					// Check if inside an foreach statement header
					if (memberbaseTypes == null && ctxt.ScopedStatement != null)
						memberbaseTypes = GetForeachIteratorType(v, ctxt);
				}

				// Note: Also works for aliases! In this case, we simply try to resolve the aliased type, otherwise the variable's base type
				var r=v.IsAlias ? new AliasResult() : new MemberResult();
				ret = r;
				
				r.Node = m;
				r.MemberBaseTypes = memberbaseTypes;
				r.ResultBase = resultBase;
				r.DeclarationOrExpressionBase = typeBase;
			}
			else if (m is DMethod)
			{
				if (DoResolveBaseType)
					memberbaseTypes = GetMethodReturnType(m as DMethod, ctxt);

				ret = new MemberResult()
				{
					Node = m,
					MemberBaseTypes = memberbaseTypes,
					ResultBase = resultBase,
					DeclarationOrExpressionBase = typeBase
				};
			}
			else if (m is DClassLike)
			{
				var tr = new TypeResult() {
					Node = m, ResultBase = resultBase, DeclarationOrExpressionBase = typeBase
				};
				DResolver.ResolveBaseClasses(tr, ctxt);

				ret = tr;
			}
			else if (m is IAbstractSyntaxTree)
			{
				var mod = (IAbstractSyntaxTree)m;
				if (typeBase != null && typeBase.ToString() != mod.ModuleName)
				{
					var pack = ctxt.ParseCache.LookupPackage(typeBase.ToString()).First();
					if (pack != null)
						ret = new ModulePackageResult(pack);
				}
				else
					ret = new ModuleResult((IAbstractSyntaxTree)m)
					{
						ResultBase = resultBase,
						DeclarationOrExpressionBase = typeBase
					};
			}
			else if (m is DEnum)
				ret = new TypeResult()
				{
					Node = m as IBlockNode,
					ResultBase = resultBase,
					DeclarationOrExpressionBase = typeBase
				};
			else if (m is TemplateParameterNode)
			{
				var tmp = ((TemplateParameterNode)m).TemplateParameter;

				//ResolveResult[] templateParameterType = null;

				//TODO: Resolve the specialization type
				//var templateParameterType = TemplateInstanceHandler.ResolveTypeSpecialization(tmp, ctxt);

				ret = new MemberResult()
				{
					Node = m,
					DeclarationOrExpressionBase = typeBase,
					ResultBase = resultBase,
					//MemberBaseTypes = templateParameterType
				};
			}

			if (DoResolveBaseType && resultBase is TemplateInstanceResult)
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocas((TemplateInstanceResult)resultBase);

			if (popAfterwards)
				ctxt.Pop();

			stackNum_HandleNodeMatch--;
			return ret;
		}

		static int stackNum_HandleNodeMatch = 0;
		public static ResolveResult[] HandleNodeMatches(
			IEnumerable<INode> matches,
			ResolverContextStack ctxt,
			ResolveResult resultBase = null,
			object TypeDeclaration = null)
		{
			var rl = new List<ResolveResult>();

			if (matches != null)
				foreach (var m in matches)
				{
					if (m == null)
						continue;

					var res = HandleNodeMatch(m, ctxt, resultBase, TypeDeclaration);
					if (res != null)
						rl.Add(res);
				}
			return rl.ToArray();
		}

		public static void FillMethodReturnType(MemberResult mr, ResolverContextStack ctxt)
		{
			if (mr == null || ctxt == null)
				return;

			var dm = mr.Node as DMethod;

			ctxt.CurrentContext.IntroduceTemplateParameterTypes(mr);

			if (dm != null)
				mr.MemberBaseTypes = GetMethodReturnType(dm, ctxt);

			ctxt.CurrentContext.RemoveParamTypesFromPreferredLocas(mr);
		}

		public static void FillMethodReturnType(DelegateResult dg, ResolverContextStack ctxt)
		{
			if (dg == null || ctxt == null)
				return;

			if (dg.IsDelegateDeclaration)
				dg.ReturnType = Resolve(((DelegateDeclaration)dg.DeclarationOrExpressionBase).ReturnType, ctxt);
			else
				dg.ReturnType = GetMethodReturnType(((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod,ctxt);
		}

		public static ResolveResult[] GetMethodReturnType(DMethod method, ResolverContextStack ctxt)
		{
			if (ctxt.Options.HasFlag(ResolutionOptions.DontResolveBaseTypes))
				return null;
			
			ResolveResult[] returnType = null;

			/*
			 * If a method's type equals null, assume that it's an 'auto' function..
			 * 1) Search for a return statement
			 * 2) Resolve the returned expression
			 * 3) Use that one as the method's type
			 */

			if (method.Type != null)
				returnType = TypeDeclarationResolver.Resolve(method.Type, ctxt);
			else if (method.Body != null)
			{
				ReturnStatement returnStmt = null;
				var list = new List<IStatement> { method.Body };
				var list2 = new List<IStatement>();

				bool foundMatch = false;
				while (!foundMatch && list.Count > 0)
				{
					foreach (var stmt in list)
					{
						if (stmt is ReturnStatement)
						{
							returnStmt = stmt as ReturnStatement;

							if (!(returnStmt.ReturnExpression is TokenExpression) ||
								(returnStmt.ReturnExpression as TokenExpression).Token != DTokens.Null)
							{
								foundMatch = true;
								break;
							}
						}

						if (stmt is StatementContainingStatement)
							list2.AddRange((stmt as StatementContainingStatement).SubStatements);
					}

					list = list2;
					list2 = new List<IStatement>();
				}

				if (returnStmt != null && returnStmt.ReturnExpression != null)
				{
					ctxt.PushNewScope(method);

					returnType = ExpressionTypeResolver.Resolve(returnStmt.ReturnExpression, ctxt);

					ctxt.Pop();
				}
			}

			return returnType;
		}

		/// <summary>
		/// string[] s;
		/// 
		/// foreach(i;s)
		/// {
		///		// i is of type 'string'
		///		writeln(i);
		/// }
		/// </summary>
		public static ResolveResult[] GetForeachIteratorType(DVariable i, ResolverContextStack ctxt)
		{
			var curStmt = ctxt.ScopedStatement;

            bool init = true;
            // Walk up statement hierarchy -- note that foreach loops can be nested
            while (curStmt != null)
            {
                if (init)
                    init = false;
                else
                    curStmt = curStmt.Parent;

				if (curStmt is ForeachStatement)
				{
					var fe = (ForeachStatement)curStmt;

					if(fe.ForeachTypeList==null)
						continue;

					// If the searched variable is declared in the header
					int iteratorIndex = -1;

					for(int j=0;j < fe.ForeachTypeList.Length;j++)
						if(fe.ForeachTypeList[j]==i)
						{
							iteratorIndex=j;
							break;
						}

					if (iteratorIndex == -1)
						continue;

					bool keyIsSearched = iteratorIndex == 0 && fe.ForeachTypeList.Length > 1;


					// foreach(var k, var v; 0 .. 9)
					if (keyIsSearched && fe.IsRangeStatement)
					{
						// -- it's static type int, of course(?)
						return ResolveIdentifier("size_t", ctxt, null);
					}

					var aggregateType = ExpressionTypeResolver.Resolve(fe.Aggregate, ctxt);

					bool remMember = false;
					aggregateType = DResolver.ResolveMembersFromResult(aggregateType, out remMember);

					if (aggregateType == null)
						return null;

					var r = new List<ResolveResult>();

					foreach (var rr in aggregateType)
					{
						// The most common way to do a foreach
						if (rr is ArrayResult)
						{
							var ar = (ArrayResult)rr;

							if (keyIsSearched)
							{
								if(ar.KeyType!=null)
									r.AddRange(ar.KeyType);
							}
							else
								r.Add(ar.ResultBase);
						}
						else if (rr is TypeResult)
						{
							var tr = (TypeResult)rr;

							if (keyIsSearched || !(tr.Node is IBlockNode))
								continue;

							bool foundIterPropertyMatch = false;
							#region Foreach over Structs and Classes with Ranges

							// Enlist all 'back'/'front' members
							var t_l = new List<ResolveResult>();

							foreach(var n in (IBlockNode)tr.Node)
								if (fe.IsReverse ? n.Name == "back" : n.Name == "front")
									t_l.Add(HandleNodeMatch(n, ctxt));

							// Remove aliases
							var iterPropertyTypes = DResolver.TryRemoveAliasesFromResult(t_l);

							foreach (var iterPropType in iterPropertyTypes)
								if (iterPropType is MemberResult)
								{
									foundIterPropertyMatch = true;

									var itp = (MemberResult)iterPropType;

									// Only take non-parameterized methods
									if (itp.Node is DMethod && ((DMethod)itp.Node).Parameters.Count != 0)
										continue;

									// Handle its base type [return type] as iterator type
									if (itp.MemberBaseTypes != null)
										r.AddRange(itp.MemberBaseTypes);

									foundIterPropertyMatch = true;
								}

							if (foundIterPropertyMatch)
								continue;
							#endregion

							#region Foreach over Structs and Classes with opApply
							t_l.Clear();
							r.Clear();
							
							foreach (var n in (IBlockNode)tr.Node)
								if (n is DMethod && 
									(fe.IsReverse ? n.Name == "opApplyReverse" : n.Name == "opApply"))
									t_l.Add(HandleNodeMatch(n, ctxt));

							iterPropertyTypes = DResolver.TryRemoveAliasesFromResult(t_l);

							foreach (var iterPropertyType in iterPropertyTypes)
								if (iterPropertyType is MemberResult)
								{
									var mr = (MemberResult)iterPropertyType;
									var dm = mr.Node as DMethod;

									if (dm == null || dm.Parameters.Count != 1)
										continue;

									var dg = dm.Parameters[0].Type as DelegateDeclaration;

									if (dg == null || dg.Parameters.Count != fe.ForeachTypeList.Length)
										continue;

									var paramType = Resolve(dg.Parameters[iteratorIndex].Type, ctxt);

									if(paramType!=null && paramType.Length > 0)
										r.Add(paramType[0]);

									//TODO: Inform the user about multiple matches whereas there should be one allowed only..
								}
							#endregion
						}
					}

					return r.Count == 0?null: r.ToArray();
				}
			}

			return null;
		}
		#endregion
	}
}
