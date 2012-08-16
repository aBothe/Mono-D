using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	public partial class TypeDeclarationResolver
	{
		public static PrimitiveType Resolve(DTokenDeclaration token)
		{
			var tk = (token as DTokenDeclaration).Token;

			if (DTokens.BasicTypes[tk])
				return new PrimitiveType(tk, 0, token);

			return null;
		}

		public static ISemantic[] Convert(IEnumerable<AbstractType> at)
		{
			var l = new List<ISemantic>();

			if(at!=null)
				foreach (var t in at)
					l.Add(t);

			return l.ToArray();
		}

		public static AbstractType[] Convert(IEnumerable<ISemantic> at)
		{
			var l = new List<AbstractType>();

			if (at != null)
				foreach (var t in at)
				{
					if (t is AbstractType)
						l.Add((AbstractType)t);
					else if (t is ISymbolValue)
						l.Add(((ISymbolValue)t).RepresentedType);
				}

			return l.ToArray();
		}

		public static AbstractType Convert(ISemantic s)
		{
			if (s is AbstractType)
				return (AbstractType)s;
			else if(s is ISymbolValue)
				return ((ISymbolValue)s).RepresentedType;
			return null;
		}

		public static AbstractType[] ResolveIdentifier(string id, ResolverContextStack ctxt, object idObject, bool ModuleScope = false)
		{
			var loc = idObject is ISyntaxRegion ? ((ISyntaxRegion)idObject).Location:CodeLocation.Empty;

			if (ModuleScope)
				ctxt.PushNewScope(ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree);

			// If there are symbols that must be preferred, take them instead of scanning the ast
			else
			{
				var tstk = new Stack<ResolverContext>();
				D_Parser.Resolver.Templates.TemplateParameterSymbol dedTemplateParam = null;
				while (!ctxt.CurrentContext.DeducedTemplateParameters.TryGetValue(id, out dedTemplateParam))
				{
					if (ctxt.PrevContextIsInSameHierarchy)
						tstk.Push(ctxt.Pop());
					else
						break;
				}

				while (tstk.Count > 0)
					ctxt.Push(tstk.Pop());

				if (dedTemplateParam!=null)
					return new[]{ dedTemplateParam };
			}

			var matches = NameScan.SearchMatchesAlongNodeHierarchy(ctxt, loc, id);

			var res= HandleNodeMatches(matches, ctxt, null, idObject);

			if (ModuleScope)
				ctxt.Pop();

			return res;
		}

		public static AbstractType ResolveSingle(string id, ResolverContextStack ctxt, object idObject, bool ModuleScope = false)
		{
			var r = ResolveIdentifier(id, ctxt, idObject, ModuleScope);

			ctxt.CheckForSingleResult(r, idObject as ISyntaxRegion);

			return r != null && r.Length != 0 ? r[0] : null;
		}


		public static AbstractType ResolveSingle(IdentifierDeclaration id, ResolverContextStack ctxt, AbstractType[] resultBases = null, bool filterForTemplateArgs = true)
		{
			var r = Resolve(id, ctxt, resultBases, filterForTemplateArgs);

			ctxt.CheckForSingleResult(r, id);

			return r != null && r.Length != 0 ? r[0] : null;
		}

		public static AbstractType[] Resolve(IdentifierDeclaration id, ResolverContextStack ctxt, AbstractType[] resultBases = null, bool filterForTemplateArgs = true)
		{
			AbstractType[] res = null;

			if (id.InnerDeclaration == null && resultBases == null)
				res= ResolveIdentifier(id.Id, ctxt, id, id.ModuleScoped);
			else
			{
				var rbases = resultBases ?? Resolve(id.InnerDeclaration, ctxt);

				if (rbases == null || rbases.Length == 0)
					return null;

				res= ResolveFurtherTypeIdentifier(id.Id, rbases, ctxt, id);
			}

			if(filterForTemplateArgs && !ctxt.Options.HasFlag(ResolutionOptions.NoTemplateParameterDeduction))
			{
				var l_=new List<AbstractType>();

				if(res!=null)
					foreach(var s in res)
						l_.Add(s);

				return TemplateInstanceHandler.EvalAndFilterOverloads(l_, null, false, ctxt);
			}
			else
				return res;
		}

		/// <summary>
		/// Used for searching further identifier list parts.
		/// 
		/// a.b -- nextIdentifier would be 'b' whereas <param name="resultBases">resultBases</param> contained the resolution result for 'a'
		/// </summary>
		public static AbstractType[] ResolveFurtherTypeIdentifier(string nextIdentifier,
			IEnumerable<AbstractType> resultBases,
			ResolverContextStack ctxt,
			object typeIdObject=null)
		{
			if((resultBases = DResolver.StripAliasSymbols(resultBases))==null)
				return null;

			var r = new List<AbstractType>();

			var nextResults = new List<AbstractType>();
			foreach (var b in resultBases)
			{
				IEnumerable<AbstractType> scanResults = new[]{ b };

				do
				{
					foreach (var scanResult in scanResults)
					{
						// First filter out all alias and member results..so that there will be only (Static-)Type or Module results left..
						if (scanResult is MemberSymbol)
						{
							var mr = (MemberSymbol)scanResult;

							if (mr.Base != null)
								nextResults.Add(mr.Base);
						}

						else if (scanResult is UserDefinedType)
						{
							var udt = (UserDefinedType)scanResult;
							var bn=udt.Definition as IBlockNode;
							var nodeMatches = NameScan.ScanNodeForIdentifier(bn, nextIdentifier, ctxt);

							ctxt.PushNewScope(bn);
							ctxt.CurrentContext.IntroduceTemplateParameterTypes(udt);

							var results = HandleNodeMatches(nodeMatches, ctxt, b, typeIdObject);

							if (results != null)
								foreach (var res in results)
									r.Add(AbstractType.Get(res));

							ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(udt);
							ctxt.Pop();
						}
						else if (scanResult is PackageSymbol)
						{
							var pack=((PackageSymbol)scanResult).Package;

							IAbstractSyntaxTree accessedModule=null;
							if (pack.Modules.TryGetValue(nextIdentifier, out accessedModule))
								r.Add(new ModuleSymbol(accessedModule as DModule, typeIdObject as ISyntaxRegion, (PackageSymbol)scanResult));
							else if (pack.Packages.TryGetValue(nextIdentifier, out pack))
								r.Add(new PackageSymbol(pack, typeIdObject as ISyntaxRegion));
						}
						else if (scanResult is ModuleSymbol)
						{
							var modRes = (ModuleSymbol)scanResult;

							var matches = NameScan.ScanNodeForIdentifier(modRes.Definition, nextIdentifier, ctxt);

							var results = HandleNodeMatches(matches, ctxt, b, typeIdObject);

							if (results != null)
								foreach (var res in results)
									r.Add(AbstractType.Get(res));
						}
					}

					scanResults = DResolver.FilterOutByResultPriority(ctxt, nextResults);
					nextResults = new List<AbstractType>();
				}
				while (scanResults != null);
			}

			return r.Count == 0 ? null : r.ToArray();
		}

		public static AbstractType Resolve(TypeOfDeclaration typeOf, ResolverContextStack ctxt)
		{
			// typeof(return)
			if (typeOf.InstanceId is TokenExpression && (typeOf.InstanceId as TokenExpression).Token == DTokens.Return)
			{
				var m = HandleNodeMatch(ctxt.ScopedBlock, ctxt, null, typeOf);
				if (m != null)
					return m;
			}
			// typeOf(myInt)  =>  int
			else if (typeOf.InstanceId != null)
			{
				var wantedTypes = Evaluation.EvaluateType(typeOf.InstanceId, ctxt);
				return DResolver.StripMemberSymbols(wantedTypes);
			}

			return null;
		}

		public static AbstractType Resolve(MemberFunctionAttributeDecl attrDecl, ResolverContextStack ctxt)
		{
			var ret = Resolve(attrDecl.InnerType, ctxt);

			ctxt.CheckForSingleResult(ret, attrDecl.InnerType);

			if (ret != null && ret.Length != 0 && ret[0] != null)
			{
				ret[0].Modifier = attrDecl.Modifier;
				return ret[0];
			}

			return null;
		}

		public static AssocArrayType Resolve(ArrayDecl ad, ResolverContextStack ctxt)
		{
			var valueTypes = Resolve(ad.ValueType, ctxt);

			ctxt.CheckForSingleResult(valueTypes, ad);

			AbstractType valueType = null;
			AbstractType keyType=null;
			int fixedArrayLength = -1;

			if (valueTypes == null || valueTypes.Length == 0)
				return null;
			valueType = valueTypes[0];

			if (ad.KeyExpression != null)
			{
				var keyVal = Evaluation.EvaluateValue(ad.KeyExpression, ctxt);

				if (keyVal != null)
				{
					// Take the value's type as array key type
					keyType = keyVal.RepresentedType;

					// It should be mostly a number only that points out how large the final array should be
					var pv = Evaluation.GetVariableContents(keyVal, new StandardValueProvider(ctxt)) as PrimitiveValue;
					if (pv != null)
					{
						fixedArrayLength = System.Convert.ToInt32(pv.Value);

						if (fixedArrayLength < 0)
							ctxt.LogError(ad, "Invalid array size: Length value must be greater than 0");
					}
					//TODO Is there any other type of value allowed?
				}
			}
			else
			{
				var t = Resolve(ad.KeyType, ctxt);
				ctxt.CheckForSingleResult(t, ad.KeyType);

				if (t != null && t.Length != 0)
					keyType = t[0];
			}


			if (keyType== null || (keyType is PrimitiveType && ((PrimitiveType)keyType).TypeToken == DTokens.Int))
				return fixedArrayLength == -1 ? 
					new ArrayType(valueType, ad) : 
					new ArrayType(valueType, fixedArrayLength, ad);

			return new AssocArrayType(valueType, keyType, ad);
		}

		public static PointerType Resolve(PointerDecl pd, ResolverContextStack ctxt)
		{
			var ptrBaseTypes = Resolve(pd.InnerDeclaration, ctxt);

			ctxt.CheckForSingleResult(ptrBaseTypes, pd);

			if (ptrBaseTypes == null || ptrBaseTypes.Length == 0)
				return null;

			return new PointerType(ptrBaseTypes[0], pd);
		}

		public static DelegateType Resolve(DelegateDeclaration dg, ResolverContextStack ctxt)
		{
			var returnTypes = Resolve(dg.ReturnType, ctxt);

			ctxt.CheckForSingleResult(returnTypes, dg.ReturnType);

			if (returnTypes != null && returnTypes.Length != 0)
				return new DelegateType(returnTypes[0], dg); // Parameter types will be resolved later on
			return null;
		}

		public static AbstractType ResolveSingle(ITypeDeclaration declaration, ResolverContextStack ctxt)
		{
			if (declaration is IdentifierDeclaration)
				return ResolveSingle(declaration as IdentifierDeclaration, ctxt);
			else if (declaration is TemplateInstanceExpression)
			{
				var a = Evaluation.GetOverloads(declaration as TemplateInstanceExpression, ctxt);
				ctxt.CheckForSingleResult(a, declaration);
				return a != null && a.Length != 0 ? a[0] : null;
			}

			AbstractType t = null;

			if (declaration is DTokenDeclaration)
				t = Resolve(declaration as DTokenDeclaration);
			else if (declaration is TypeOfDeclaration)
				t = Resolve(declaration as TypeOfDeclaration, ctxt);
			else if (declaration is MemberFunctionAttributeDecl)
				t = Resolve(declaration as MemberFunctionAttributeDecl, ctxt);
			else if (declaration is ArrayDecl)
				t = Resolve(declaration as ArrayDecl, ctxt);
			else if (declaration is PointerDecl)
				t = Resolve(declaration as PointerDecl, ctxt);
			else if (declaration is DelegateDeclaration)
				t = Resolve(declaration as DelegateDeclaration, ctxt);

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

			return t;
		}

		public static AbstractType[] Resolve(ITypeDeclaration declaration, ResolverContextStack ctxt)
		{
			if (declaration is IdentifierDeclaration)
				return Resolve(declaration as IdentifierDeclaration, ctxt);
			else if (declaration is TemplateInstanceExpression)
				return Evaluation.GetOverloads(declaration as TemplateInstanceExpression, ctxt);

			var t= ResolveSingle(declaration, ctxt);

			return t==null ? null : new[]{t};
		}







		#region Intermediate methods
		/// <summary>
		/// The variable's or method's base type will be resolved (if auto type, the intializer's type will be taken).
		/// A class' base class will be searched.
		/// etc..
		/// </summary>
		public static AbstractType HandleNodeMatch(
			INode m,
			ResolverContextStack ctxt,
			AbstractType resultBase = null,
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

			AbstractType ret = null;

			// To support resolving type parameters to concrete types if the context allows this, introduce all deduced parameters to the current context
			if (DoResolveBaseType && resultBase is DSymbol)
				ctxt.CurrentContext.IntroduceTemplateParameterTypes((DSymbol)resultBase);

			// Only import symbol aliases are allowed to search in the parse cache
			if (m is ImportSymbolAlias)
			{
				var isa = (ImportSymbolAlias)m;

				if (isa.IsModuleAlias ? isa.Type != null : isa.Type.InnerDeclaration != null)
				{
					var mods = new List<DModule>();
					var td=isa.IsModuleAlias ? isa.Type : isa.Type.InnerDeclaration;
					foreach (var mod in ctxt.ParseCache.LookupModuleName(td.ToString()))
						mods.Add(mod as DModule);

					

					if(mods.Count == 0)
							ctxt.LogError(new NothingFoundError(isa.Type));
					else if(mods.Count > 1)
					{
						var m__=new List<ISemantic>();

						foreach(var mod in mods)
 							m__.Add(new ModuleSymbol(mod, isa.Type));

						ctxt.LogError(new AmbiguityError(isa.Type,m__));
					}

					var bt=mods.Count != 0 ? (AbstractType)new ModuleSymbol(mods[0], td) : null;

					//TODO: Is this correct behaviour?
					if (!isa.IsModuleAlias){
						var furtherId = ResolveFurtherTypeIdentifier(isa.Type.ToString(false), new[]{ bt }, ctxt, isa.Type);

						ctxt.CheckForSingleResult(furtherId, isa.Type);

						if (furtherId != null && furtherId.Length != 0)
							bt = furtherId[0];
						else
							bt = null;
					}

					ret = new AliasedType(isa, bt, isa.Type);
				}
			}
			else if (m is DVariable)
			{
				var v = (DVariable)m;
				AbstractType bt = null;

				if (DoResolveBaseType)
				{
					var bts = TypeDeclarationResolver.Resolve(v.Type, ctxt);

					if (bts != null && bts.Length != 0 && ctxt.CheckForSingleResult(bts, v.Type))
						bt = bts[0];

					// For auto variables, use the initializer to get its type
					else if (v.Initializer != null)
						bt = ExpressionSemantics.Evaluation.EvaluateType(v.Initializer, ctxt);

					// Check if inside an foreach statement header
					if (bt == null && ctxt.ScopedStatement != null)
						bt = GetForeachIteratorType(v, ctxt);
				}

				// Note: Also works for aliases! In this case, we simply try to resolve the aliased type, otherwise the variable's base type
				ret=v.IsAlias ? 
					(DSymbol)new AliasedType(v, bt, typeBase as ISyntaxRegion) : 
					new MemberSymbol(v, bt, typeBase as ISyntaxRegion);
			}
			else if (m is DMethod)
			{
				var bt=DoResolveBaseType ? GetMethodReturnType((DMethod)m, ctxt) : null;

				ret = new MemberSymbol((DNode)m, bt, typeBase as ISyntaxRegion);
			}
			else if (m is DClassLike)
			{
				UserDefinedType udt = null;
				var dc=(DClassLike)m;

				switch (dc.ClassType)
				{
					case DTokens.Struct:
						udt = new StructType(dc, typeBase as ISyntaxRegion);
						break;
					case DTokens.Union:
						udt = new UnionType(dc, typeBase as ISyntaxRegion);
						break;
					case DTokens.Class:
						udt = new ClassType(dc, typeBase as ISyntaxRegion, null);
						break;
					case DTokens.Template:
						udt = new TemplateType(dc, typeBase as ISyntaxRegion);
						break;
					case DTokens.Interface:
						udt = new InterfaceType(dc, typeBase as ISyntaxRegion);
						break;
					default:
						ctxt.LogError(new ResolutionError(m, "Unknown type ("+DTokens.GetTokenString(dc.ClassType)+")"));
						break;
				}

				ret=DResolver.ResolveBaseClasses(udt, ctxt);
			}
			else if (m is IAbstractSyntaxTree)
			{
				var mod = (IAbstractSyntaxTree)m;
				if (typeBase != null && typeBase.ToString() != mod.ModuleName)
				{
					var pack = ctxt.ParseCache.LookupPackage(typeBase.ToString()).First();
					if (pack != null)
						ret = new PackageSymbol(pack, typeBase as ISyntaxRegion);
				}
				else
					ret = new ModuleSymbol(m as DModule, typeBase as ISyntaxRegion);
			}
			else if (m is DEnum)
				ret = new EnumType((DEnum)m, typeBase as ISyntaxRegion);
			else if (m is TemplateParameterNode)
			{
				var tmp = ((TemplateParameterNode)m).TemplateParameter;

				//ResolveResult[] templateParameterType = null;

				//TODO: Resolve the specialization type
				//var templateParameterType = TemplateInstanceHandler.ResolveTypeSpecialization(tmp, ctxt);

				ret = new MemberSymbol((DNode)m, null, typeBase as ISyntaxRegion);
			}

			if (DoResolveBaseType && resultBase is DSymbol)
				ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals((DSymbol)resultBase);

			if (popAfterwards)
				ctxt.Pop();

			stackNum_HandleNodeMatch--;
			return ret;
		}

		static int stackNum_HandleNodeMatch = 0;
		public static AbstractType[] HandleNodeMatches(
			IEnumerable<INode> matches,
			ResolverContextStack ctxt,
			AbstractType resultBase = null,
			object TypeDeclaration = null)
		{
			var rl = new List<AbstractType>();

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

		public static MemberSymbol FillMethodReturnType(MemberSymbol mr, ResolverContextStack ctxt)
		{
			if (mr == null || ctxt == null)
				return mr;

			var dm = mr.Definition as DMethod;

			ctxt.CurrentContext.IntroduceTemplateParameterTypes(mr);

			if (dm != null)
			{
				var returnType=GetMethodReturnType(dm, ctxt);
				mr = new MemberSymbol(dm, returnType, mr.DeclarationOrExpressionBase);
			}

			ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(mr);

			return mr;
		}

		public static AbstractType GetMethodReturnType(DelegateType dg, ResolverContextStack ctxt)
		{
			if (dg == null || ctxt == null)
				return null;

			if (dg.IsFunctionLiteral)
				return GetMethodReturnType(((FunctionLiteral)dg.DeclarationOrExpressionBase).AnonymousMethod, ctxt);
			else
			{
				var rt=((DelegateDeclaration)dg.DeclarationOrExpressionBase).ReturnType;
				var r = Resolve(rt, ctxt);

				ctxt.CheckForSingleResult(r,rt);

				return r[0];
			}
		}

		public static AbstractType GetMethodReturnType(DMethod method, ResolverContextStack ctxt)
		{
			if (ctxt!=null && ctxt.Options.HasFlag(ResolutionOptions.DontResolveBaseTypes))
				return null;
			
			/*
			 * If a method's type equals null, assume that it's an 'auto' function..
			 * 1) Search for a return statement
			 * 2) Resolve the returned expression
			 * 3) Use that one as the method's type
			 */

			if (method.Type != null)
			{
				var returnType = TypeDeclarationResolver.Resolve(method.Type, ctxt);

				if (ctxt.CheckForSingleResult(returnType, method.Type))
					return returnType[0];
			}
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

					var t= Evaluation.EvaluateType(returnStmt.ReturnExpression, ctxt);

					ctxt.Pop();

					return t;
				}
			}

			return null;
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
		public static AbstractType GetForeachIteratorType(DVariable i, ResolverContextStack ctxt)
		{
			var r = new List<AbstractType>();
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
						return new PrimitiveType(DTokens.Int);
					}

					var aggregateType = Evaluation.EvaluateType(fe.Aggregate, ctxt);

					aggregateType = DResolver.StripMemberSymbols(aggregateType);

					if (aggregateType == null)
						return null;

					// The most common way to do a foreach
					if (aggregateType is AssocArrayType)
					{
						var ar = (AssocArrayType)aggregateType;

						if (keyIsSearched)
							return ar.KeyType;
						else
							return ar.ValueType;
					}
					else if (aggregateType is UserDefinedType)
					{
						var tr = (UserDefinedType)aggregateType;

						if (keyIsSearched || !(tr.Definition is IBlockNode))
							continue;

						bool foundIterPropertyMatch = false;
						#region Foreach over Structs and Classes with Ranges

						// Enlist all 'back'/'front' members
						var t_l = new List<AbstractType>();

						foreach(var n in (IBlockNode)tr.Definition)
							if (fe.IsReverse ? n.Name == "back" : n.Name == "front")
								t_l.Add(HandleNodeMatch(n, ctxt));

						// Remove aliases
						var iterPropertyTypes = DResolver.StripAliasSymbols(t_l);

						foreach (var iterPropType in iterPropertyTypes)
							if (iterPropType is MemberSymbol)
							{
								foundIterPropertyMatch = true;

								var itp = (MemberSymbol)iterPropType;

								// Only take non-parameterized methods
								if (itp.Definition is DMethod && ((DMethod)itp.Definition).Parameters.Count != 0)
									continue;

								// Handle its base type [return type] as iterator type
								if (itp.Base != null)
									r.Add(itp.Base);

								foundIterPropertyMatch = true;
							}

						if (foundIterPropertyMatch)
							continue;
						#endregion

						#region Foreach over Structs and Classes with opApply
						t_l.Clear();
						r.Clear();
							
						foreach (var n in (IBlockNode)tr.Definition)
							if (n is DMethod && 
								(fe.IsReverse ? n.Name == "opApplyReverse" : n.Name == "opApply"))
								t_l.Add(HandleNodeMatch(n, ctxt));

						iterPropertyTypes = DResolver.StripAliasSymbols(t_l);

						foreach (var iterPropertyType in iterPropertyTypes)
							if (iterPropertyType is MemberSymbol)
							{
								var mr = (MemberSymbol)iterPropertyType;
								var dm = mr.Definition as DMethod;

								if (dm == null || dm.Parameters.Count != 1)
									continue;

								var dg = dm.Parameters[0].Type as DelegateDeclaration;

								if (dg == null || dg.Parameters.Count != fe.ForeachTypeList.Length)
									continue;

								var paramType = Resolve(dg.Parameters[iteratorIndex].Type, ctxt);

								if(paramType!=null && paramType.Length > 0)
									r.Add(paramType[0]);
							}
						#endregion
					}

					if (r.Count > 1)
						ctxt.LogError(new ResolutionError(curStmt, "Ambigous iterator type"));

					return r.Count != 0 ? r[0] : null;
				}
			}

			return null;
		}
		#endregion
	}
}
