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

		public static ResolveResult[] ResolveIdentifier(string id, ResolverContextStack ctxt, object idObject)
		{
			var loc = CodeLocation.Empty;

			if (idObject is ITypeDeclaration)
				loc = (idObject as ITypeDeclaration).Location;
			else if (idObject is IExpression)
				loc = (idObject as IExpression).Location;

			var matches = NameScan.SearchMatchesAlongNodeHierarchy(ctxt, loc, id);

			var r= HandleNodeMatches(matches, ctxt, null, idObject);

			if (idObject is TemplateInstanceExpression)
				return TemplateInstanceResolver.ResolveAndFilterTemplateResults(((TemplateInstanceExpression)idObject).Arguments, r, ctxt);

			return TemplateInstanceResolver.ApplyDefaultTemplateParameters(r, ctxt);
		}

		public static ResolveResult[] Resolve(IdentifierDeclaration declaration, ResolverContextStack ctxt, ResolveResult[] resultBases=null)
		{
			var id = declaration as IdentifierDeclaration;

			if (declaration.InnerDeclaration == null && resultBases==null)
				return ResolveIdentifier(id.Id, ctxt, id);

			var rbases = resultBases ?? Resolve(declaration.InnerDeclaration, ctxt);

			if (rbases == null || rbases.Length == 0)
				return null;

			return ResolveFurtherTypeIdentifier(id.Id,rbases,ctxt,declaration);
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
			var r = new List<ResolveResult>();

			var nextResults = new List<ResolveResult>();
			foreach (var b in DResolver.TryRemoveAliasesFromResult(resultBases))
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

			if (typeIdObject is TemplateInstanceExpression)
				return TemplateInstanceResolver.ResolveAndFilterTemplateResults(((TemplateInstanceExpression)typeIdObject).Arguments, r, ctxt);

			return TemplateInstanceResolver.ApplyDefaultTemplateParameters(r, ctxt);
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

			//HACK: Really dirty stack overflow prevention via manually counting call depth
			var DoResolveBaseType =
				stackNum_HandleNodeMatch > 5 ?
				false : ctxt.CurrentContext.ResolveBaseTypes;

			// Prevent infinite recursion if the type accidently equals the node's name
			if (m.Type != null && m.Type.ToString(false) == m.Name)
				DoResolveBaseType = false;

			ResolveResult ret = null;
			ResolveResult[] memberbaseTypes = null;

			// Only import symbol aliases are allowed to search in the parse cache
			if (m is ImportSymbolAlias)
			{
				var isa = (ImportSymbolAlias)m;

				if (isa.IsModuleAlias ? isa.Type == null : isa.Type.InnerDeclaration == null)
					return null;

				var alias = new MemberResult { 
					Node=m
				};

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
			else if (m is DVariable)
			{
				var v = m as DVariable;

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

				#region Resolve aliases if wished
				if (ctxt.CurrentContext.ResolveAliases && memberbaseTypes != null)
				{
					/*
					 * To ensure that absolutely all kinds of alias definitions became resolved (includes aliased alias definitions!), 
					 * loop through the resolution process again, after at least one aliased type has been found.
					 */
					while (memberbaseTypes.Length > 0)
					{
						bool hadAliasResolution = false;
						var memberBaseTypes_Override = new List<ResolveResult>();

						foreach (var type in memberbaseTypes)
						{
							var mr = type as MemberResult;
							if (mr != null && mr.Node is DVariable)
							{
								var dv = mr.Node as DVariable;
								// Note: Normally, a variable's base type mustn't be an other variable but an alias defintion...
								if (dv.IsAlias)
								{
									var newRes = TypeDeclarationResolver.Resolve(dv.Type, ctxt);
									if (newRes != null)
										memberBaseTypes_Override.AddRange(newRes);
									hadAliasResolution = true;
									continue;
								}
							}

							// If no alias found, re-add it to our override list again
							memberBaseTypes_Override.Add(type);
						}
						memberbaseTypes = memberBaseTypes_Override.ToArray();

						if (!hadAliasResolution)
							break;
					}
				}
				#endregion

				memberbaseTypes = TemplateInstanceResolver.SubstituteTemplateParameters(memberbaseTypes, resultBase);

				// Note: Also works for aliases! In this case, we simply try to resolve the aliased type, otherwise the variable's base type
				ret = new MemberResult()
				{
					Node = m,
					MemberBaseTypes = memberbaseTypes,
					ResultBase = resultBase,
					DeclarationOrExpressionBase = typeBase
				};
			}
			else if (m is DMethod)
			{
				memberbaseTypes = DoResolveBaseType ? GetMethodReturnType(m as DMethod, ctxt) : null;

				memberbaseTypes = TemplateInstanceResolver.SubstituteTemplateParameters(memberbaseTypes, resultBase);

				ret = new MemberResult()
				{
					Node = m,
					MemberBaseTypes = memberbaseTypes,
					ResultBase = resultBase,
					DeclarationOrExpressionBase = typeBase
				};
			}
			else if (m is DClassLike)
				ret = new TypeResult()
				{
					Node = (DClassLike)m,
					BaseClass = DoResolveBaseType ? DResolver.ResolveBaseClass((DClassLike)m, ctxt) : null,
					ResultBase = resultBase,
					DeclarationOrExpressionBase = typeBase
				};
			else if (m is IAbstractSyntaxTree)
			{
				var mod=(IAbstractSyntaxTree)m;
				if (typeBase != null && typeBase.ToString() != mod.ModuleName)
				{
					var pack = ctxt.ParseCache.LookupPackage(typeBase.ToString()).First();
					if(pack!=null)
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

				//FIXME: Resolve the specialization type correctly
				var templateParameterType = TemplateInstanceResolver.ResolveTypeSpecialization(tmp, ctxt);

				ret = new MemberResult()
				{
					Node = m,
					DeclarationOrExpressionBase = typeBase,
					ResultBase = resultBase,
					MemberBaseTypes = templateParameterType
				};
			}

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

		public static ResolveResult[] GetMethodReturnType(DMethod method, ResolverContextStack ctxt)
		{
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
			
			// Walk up statement hierarchy -- note that foreach loops can be nested
			while (curStmt != null)
			{
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

				curStmt = curStmt.Parent;
			}

			return null;
		}
		#endregion
	}
}
