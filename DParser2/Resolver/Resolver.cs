using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public partial class DResolver
	{
		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where, out IStatement ScopedStatement)
		{
			ScopedStatement = null;

			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
					if (n is IBlockNode && Where >= n.StartLocation && Where <= n.EndLocation)
						return SearchBlockAt(n as IBlockNode, Where, out ScopedStatement);

			if (Parent is DMethod)
			{
				var dm = Parent as DMethod;

				var body = dm.GetSubBlockAt(Where);

				// First search the deepest statement under the caret
				if(body!=null)
					ScopedStatement = body.SearchStatementDeeply(Where);
			}

			return Parent;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
				{
					if (!(n is DClassLike)) continue;

					var b = n as IBlockNode;
					if (Where >= b.BlockStartLocation && Where <= b.EndLocation)
						return SearchClassLikeAt(b, Where);
				}

			return Parent;
		}

		#region Import path resolving
		/// <summary>
		/// Returns all imports of a module and those public ones of the imported modules
		/// </summary>
		/// <param name="cc"></param>
		/// <param name="ActualModule"></param>
		/// <returns></returns>
		public static IEnumerable<IAbstractSyntaxTree> ResolveImports(DModule ActualModule, IEnumerable<IAbstractSyntaxTree> CodeCache)
		{
			var ret = new List<IAbstractSyntaxTree>();
			if (CodeCache == null || ActualModule == null) return ret;

			// Try to add the 'object' module
			var objmod = SearchModuleInCache(CodeCache, "object");
			if (objmod != null && !ret.Contains(objmod))
				ret.Add(objmod);

			/* 
			 * dmd-feature: public imports only affect the directly superior module
			 *
			 * Module A:
			 * import B;
			 * 
			 * foo(); // Will fail, because foo wasn't found
			 * 
			 * Module B:
			 * import C;
			 * 
			 * Module C:
			 * public import D;
			 * 
			 * Module D:
			 * void foo() {}
			 * 
			 * 
			 * Whereas
			 * Module B:
			 * public import C;
			 * 
			 * will succeed because we have a closed import hierarchy in which all imports are public.
			 * 
			 */

			/*
			 * Procedure:
			 * 
			 * 1) Take the imports of the current module
			 * 2) Add the respective modules
			 * 3) If that imported module got public imports, also make that module to the current one and repeat Step 1) recursively
			 * 
			 */

			foreach (var kv in ActualModule.Imports)
				if (kv.IsSimpleBinding && !kv.IsStatic)
				{
					if (kv.ModuleIdentifier == null)
						continue;

					var impMod = SearchModuleInCache(CodeCache, kv.ModuleIdentifier.ToString()) as DModule;

					if (impMod != null && !ret.Contains(impMod))
					{
						ret.Add(impMod);

						ScanForPublicImports(ret, impMod, CodeCache);
					}

				}

			return ret;
		}

		static void ScanForPublicImports(List<IAbstractSyntaxTree> ret, DModule currentlyWatchedImport, IEnumerable<IAbstractSyntaxTree> CodeCache)
		{
			if (currentlyWatchedImport != null && currentlyWatchedImport.Imports != null)
				foreach (var kv2 in currentlyWatchedImport.Imports)
					if (kv2.IsSimpleBinding && !kv2.IsStatic && kv2.IsPublic)
					{
						if (kv2.ModuleIdentifier == null)
							continue;

						var impMod2 = SearchModuleInCache(CodeCache, kv2.ModuleIdentifier.ToString()) as DModule;

						if (impMod2 != null && !ret.Contains(impMod2))
						{
							ret.Add(impMod2);

							ScanForPublicImports(ret, impMod2, CodeCache);
						}
					}
		}
		#endregion

		static IAbstractSyntaxTree SearchModuleInCache(IEnumerable<IAbstractSyntaxTree> HayStack, string ModuleName)
		{
			foreach (var m in HayStack)
			{
				if (m.Name == ModuleName)
					return m;
			}
			return null;
		}

		#region ResolveType
		public static ResolveResult[] ResolveType(IEditorData editor,
			ResolverContext ctxt,
			bool alsoParseBeyondCaret = false,
			bool onlyAssumeIdentifierList = false)
		{
			var code = editor.ModuleCode;

			int start = 0;
			CodeLocation startLocation=CodeLocation.Empty;
			bool IsExpression = false;
			
			if (ctxt.ScopedStatement is IExpressionContainingStatement)
			{
				var exprs=(ctxt.ScopedStatement as IExpressionContainingStatement).SubExpressions;
				IExpression targetExpr = null;

				if(exprs!=null)
					foreach (var ex in exprs)
						if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, editor.CaretLocation))
							!=ex)
							break;

				if (targetExpr != null && editor.CaretLocation >= targetExpr.Location && editor.CaretLocation <= targetExpr.EndLocation)
				{
					startLocation = targetExpr.Location;
					start = DocumentHelper.LocationToOffset(editor.ModuleCode, startLocation);
					IsExpression = true;
				}
			}
			
			if(!IsExpression)
			{
				// First check if caret is inside a comment/string etc.
				int lastNonNormalStart = 0;
				int lastNonNormalEnd = 0;
				var caretContext = CaretContextAnalyzer.GetTokenContext(code, editor.CaretOffset, out lastNonNormalStart, out lastNonNormalEnd);

				// Return if comment etc. found
				if (caretContext != TokenContext.None)
					return null;

				start = CaretContextAnalyzer.SearchExpressionStart(code, editor.CaretOffset - 1,
					(lastNonNormalEnd > 0 && lastNonNormalEnd < editor.CaretOffset) ? lastNonNormalEnd : 0);
				startLocation = DocumentHelper.OffsetToLocation(editor.ModuleCode, start);
			}

			if (start < 0 || editor.CaretOffset<=start)
				return null;

			var expressionCode = code.Substring(start, alsoParseBeyondCaret ? code.Length - start : editor.CaretOffset - start);

			var parser = DParser.Create(new StringReader(expressionCode));
			parser.Lexer.SetInitialLocation(startLocation);
			parser.Step();

			if (!IsExpression && onlyAssumeIdentifierList && parser.Lexer.LookAhead.Kind == DTokens.Identifier)
				return ResolveType(parser.IdentifierList(), ctxt);
			else if (IsExpression || parser.IsAssignExpression())
			{
				var expr = parser.AssignExpression();

				if (expr != null)
				{
					// Do not accept number literals but (100.0) etc.
					if (expr is IdentifierExpression && (expr as IdentifierExpression).Format.HasFlag(LiteralFormat.Scalar))
						return null;

					expr = ExpressionHelper.SearchExpressionDeeply(expr, editor.CaretLocation);

					ResolveResult[] ret = null;

					if (expr is IdentifierExpression && !(expr as IdentifierExpression).IsIdentifier)
						ret = new[] { new ExpressionResult() { Expression = expr, TypeDeclarationBase = expr.ExpressionTypeRepresentation } };
					else
						ret = ResolveType(expr.ExpressionTypeRepresentation, ctxt);

					if (ret == null && expr != null && !(expr is TokenExpression))
						ret = new[] { new ExpressionResult() { Expression = expr, TypeDeclarationBase=expr.ExpressionTypeRepresentation } };

					return ret;
				}
			}
			else
				return ResolveType(parser.Type(), ctxt);

			return null;
		}

		public static ResolveResult[] ResolveType(ITypeDeclaration declaration,
		                                          ResolverContext ctxt,
												  IBlockNode currentScopeOverride = null)
		{
			if (ctxt == null)
				return null;

			var ctxtOverride=ctxt;
			
			if(currentScopeOverride!=null && currentScopeOverride!=ctxt.ScopedBlock){
				ctxtOverride=new ResolverContext();
				ctxtOverride.ApplyFrom(ctxt);
				ctxtOverride.ScopedBlock = currentScopeOverride;
				ctxtOverride.ScopedStatement = null;
			}			
			
			if(ctxtOverride.ScopedBlock!=null &&( ctxtOverride.ImportCache==null || ctxtOverride.ScopedBlock.NodeRoot!=ctxt.ScopedBlock.NodeRoot))
			{
				ctxtOverride.ImportCache=ResolveImports(ctxtOverride.ScopedBlock.NodeRoot as DModule,ctxt.ParseCache);
			}
			
			if (currentScopeOverride == null)
				currentScopeOverride = ctxt.ScopedBlock;

			if (ctxt == null || declaration == null)
				return null;

			ResolveResult[] preRes = null;
			object scopeObj = null;

			if (ctxtOverride.ScopedStatement != null)
			{
				var curStmtLevel=ctxtOverride.ScopedStatement;

				while (curStmtLevel != null && !(curStmtLevel is BlockStatement))
					curStmtLevel = curStmtLevel.Parent;

				if(curStmtLevel is BlockStatement)
					scopeObj = curStmtLevel;
			}

			if (scopeObj == null)
				scopeObj = ctxtOverride.ScopedBlock;

			// Check if already resolved once
			if (ctxtOverride.TryGetAlreadyResolvedType(declaration.ToString(), out preRes, scopeObj))
				return preRes;

			var returnedResults = new List<ResolveResult>();

			// Walk down recursively to resolve everything from the very first to declaration's base type declaration.
			ResolveResult[] rbases = null;
			if (declaration.InnerDeclaration != null)
			{
				rbases = ResolveType(declaration.InnerDeclaration, ctxtOverride);

				if (rbases != null)
					rbases = FilterOutByResultPriority(ctxt, rbases);
			}

            // If it's a template, resolve the template id first
            if (declaration is TemplateInstanceExpression)
                declaration = (declaration as TemplateInstanceExpression).TemplateIdentifier;

			/* 
			 * If there is no parent resolve context (what usually means we are searching the type named like the first identifier in the entire declaration),
			 * search the very first type declaration by walking along the current block scope hierarchy.
			 * If there wasn't any item found in that procedure, search in the global parse cache
			 */
			#region Search initial member/type/module/whatever
			if (rbases == null)
			{
				#region IdentifierDeclaration
				if (declaration is IdentifierDeclaration)
				{
					string searchIdentifier = (declaration as IdentifierDeclaration).Value as string;

					if (string.IsNullOrEmpty(searchIdentifier))
						return null;

					// Try to convert the identifier into a token
					int searchToken = DTokens.GetTokenID(searchIdentifier);

					// References current class scope
					if (searchToken == DTokens.This)
					{
						var classDef = ctxt.ScopedBlock;

						while (!(classDef is DClassLike) && classDef != null)
							classDef = classDef.Parent as IBlockNode;

						if (classDef is DClassLike)
						{
							var res = HandleNodeMatch(classDef, ctxtOverride, typeBase: declaration);

							if (res != null)
								returnedResults.Add(res);
						}
					}
					// References super type of currently scoped class declaration
					else if (searchToken == DTokens.Super)
					{
						var classDef = currentScopeOverride;

						while (!(classDef is DClassLike) && classDef != null)
							classDef = classDef.Parent as IBlockNode;

						if (classDef != null)
						{
							var baseClassDefs = ResolveBaseClass(classDef as DClassLike, ctxtOverride);

							if (baseClassDefs != null)
							{
								// Important: Overwrite type decl base with 'super' token
								foreach (var bc in baseClassDefs)
									bc.TypeDeclarationBase = declaration;

								returnedResults.AddRange(baseClassDefs);
							}
						}
					}
					// If we found a base type, return a static-type-result
					else if (searchToken > 0)
					{
						if (DTokens.BasicTypes[searchToken])
							returnedResults.Add(new StaticTypeResult()
							{
								BaseTypeToken = searchToken,
								TypeDeclarationBase = declaration
							});
						// anything else is just a keyword, not a type
					}
					// (As usual) Go on searching in the local&global scope(s)
					else
					{
						var matches = NameScan.SearchMatchesAlongNodeHierarchy(ctxtOverride, declaration.Location, searchIdentifier);

						var results = HandleNodeMatches(matches, ctxtOverride, TypeDeclaration: declaration);
						if (results != null)
							returnedResults.AddRange(results);
					}
				}
				#endregion

				#region TypeOfDeclaration
				else if(declaration is TypeOfDeclaration)
				{
					var typeOf=declaration as TypeOfDeclaration;
					
					// typeof(return)
					if(typeOf.InstanceId is TokenExpression && (typeOf.InstanceId as TokenExpression).Token==DTokens.Return)
					{
						var m= HandleNodeMatch(currentScopeOverride,ctxt,currentScopeOverride,null,declaration);
						if(m!=null)
							returnedResults.Add(m);
					}
					// typeOf(myInt) === int
					else if(typeOf.InstanceId!=null)
					{
						var wantedTypes=ResolveType(typeOf.InstanceId.ExpressionTypeRepresentation,ctxt,currentScopeOverride);

						if (wantedTypes == null)
							return null;

						// Scan down for variable's base types
						var c1=new List<ResolveResult>(wantedTypes);
						var c2=new List<ResolveResult>();
						
						while(c1.Count>0)
						{
							foreach(var t in c1)
							{
								if (t is MemberResult)
								{
									if((t as MemberResult).MemberBaseTypes!=null)
										c2.AddRange((t as MemberResult).MemberBaseTypes);
								}
								else
									returnedResults.Add(t);
							}
							
							c1.Clear();
							c1.AddRange(c2);
							c2.Clear();
						}
					}
				}
				#endregion

				else
					returnedResults.Add(new StaticTypeResult() { TypeDeclarationBase = declaration });
			}
			#endregion

			#region Search in further, deeper levels
			else foreach (var rbase in rbases)
				{
					#region Identifier
					if (declaration is IdentifierDeclaration)
					{
						string searchIdentifier = (declaration as IdentifierDeclaration).Value as string;

						// Scan for static properties
						var staticProp = StaticPropertyResolver.TryResolveStaticProperties(
							rbase,
							declaration as IdentifierDeclaration,
							ctxtOverride);
						if (staticProp != null)
						{
							returnedResults.Add(staticProp);
							continue;
						}

						var scanResults = new List<ResolveResult>();
						scanResults.Add(rbase);
						var nextResults = new List<ResolveResult>();

						while (scanResults.Count > 0)
						{
							foreach (var scanResult in scanResults)
							{
								// First filter out all alias and member results..so that there will be only (Static-)Type or Module results left..
								if (scanResult is MemberResult)
								{
									var _m = (scanResult as MemberResult).MemberBaseTypes;
									if (_m != null) 
										nextResults.AddRange(FilterOutByResultPriority(ctxt, _m));
								}

								else if (scanResult is TypeResult)
								{
									var tr=scanResult as TypeResult;
									var nodeMatches=NameScan.ScanNodeForIdentifier(tr.ResolvedTypeDefinition, searchIdentifier, ctxtOverride);

									var results = HandleNodeMatches(
										nodeMatches,
										ctxtOverride, 
										tr.ResolvedTypeDefinition, 
										resultBase: rbase, 
										TypeDeclaration: declaration);

									if (results != null)
										returnedResults.AddRange(FilterOutByResultPriority(ctxt, results));
								}
								else if (scanResult is ModuleResult)
								{
									var modRes = scanResult as ModuleResult;

									if (modRes.IsOnlyModuleNamePartTyped())
									{
										var modNameParts = modRes.ResolvedModule.ModuleName.Split('.');

										if (modNameParts[modRes.AlreadyTypedModuleNameParts] == searchIdentifier)
										{
											returnedResults.Add(new ModuleResult()
											{
												ResolvedModule = modRes.ResolvedModule,
												AlreadyTypedModuleNameParts = modRes.AlreadyTypedModuleNameParts + 1,
												ResultBase = modRes,
												TypeDeclarationBase = declaration
											});
										}
									}
									else
									{
										var results = HandleNodeMatches(
										NameScan.ScanNodeForIdentifier((scanResult as ModuleResult).ResolvedModule, searchIdentifier, ctxtOverride),
										ctxtOverride, currentScopeOverride, rbase, TypeDeclaration: declaration);
										if (results != null)
											returnedResults.AddRange(results);
									}
								}
								else if (scanResult is StaticTypeResult)
								{

								}
							}

							scanResults = nextResults;
							nextResults = new List<ResolveResult>();
						}
					}
					#endregion

					else if (declaration is ArrayDecl || declaration is PointerDecl)
					{
						returnedResults.Add(new StaticTypeResult() { TypeDeclarationBase = declaration, ResultBase = rbase });
					}

					else if (declaration is DExpressionDecl)
					{
						var expr = (declaration as DExpressionDecl).Expression;

						/* 
						 * Note: Assume e.g. foo.bar.myArray in foo.bar.myArray[0] has been resolved!
						 * So, we just have to take the last postfix expression
						 */

						/*
						 * After we've done this, we reduce the stack..
						 * Target of this action is to retrieve the value type:
						 * 
						 * int[string][] myArray; // Is an array that holds an associative array, whereas the value type is 'int', and key type is 'string'
						 * 
						 * auto mySubArray=myArray[0]; // returns a reference to an int[string] array
						 * 
						 * auto myElement=mySubArray["abcd"]; // returns the most basic value type: 'int'
						 */
						if (rbase is StaticTypeResult)
						{
							var str = rbase as StaticTypeResult;

							if (str.TypeDeclarationBase is ArrayDecl && expr is PostfixExpression_Index)
							{
								returnedResults.Add(new StaticTypeResult() { TypeDeclarationBase = (str.TypeDeclarationBase as ArrayDecl).ValueType });
							}
						}
						else if (rbase is MemberResult)
						{
							var mr = rbase as MemberResult;
							if (mr.MemberBaseTypes != null && mr.MemberBaseTypes.Length > 0)
								foreach (var memberType in TryRemoveAliasesFromResult(mr.MemberBaseTypes))
								{
									if (expr is PostfixExpression_Index)
									{
										if (memberType is StaticTypeResult)
										{
											var str = memberType as StaticTypeResult;
											/*
											 * If the member's type is an array, and if our expression contains an index-expression (e.g. myArray[0]),
											 * take the value type of the 
											 */
											// For array and pointer declarations, the StaticTypeResult object contains the array's value type / pointer base type.
											if (str != null && (str.TypeDeclarationBase is ArrayDecl || str.TypeDeclarationBase is PointerDecl))
											{
												returnedResults.AddRange(TryRemoveAliasesFromResult(str.ResultBase));
												continue;
											}
										}
									}
									
									returnedResults.Add(memberType);
								}
						}
					}
				}
			#endregion

			if (returnedResults.Count > 0)
			{
				ctxt.TryAddResults(declaration.ToString(), returnedResults.ToArray(), ctxtOverride.ScopedBlock);

				return FilterOutByResultPriority(ctxt, returnedResults.ToArray());
			}

			return null;
		}
		#endregion

		public static ResolveResult[] FilterOutByResultPriority(
			ResolverContext ctxt,
			ResolveResult[] results)
		{
			if (results != null && results.Length > 1)
			{
				var newRes = new List<ResolveResult>();
				foreach (var rb in results)
				{
					var n = GetResultMember(rb);
					if (n != null)
					{
						// Put priority on locals
						if (n is DVariable && 
							(n as DVariable).IsLocal)
							return new[] { rb };

						// If member/type etc. is part of the actual module, omit external symbols
						if (n.NodeRoot == ctxt.ScopedBlock.NodeRoot)
							newRes.Add(rb);
					}
				}

				if (newRes.Count > 0)
					return newRes.ToArray();
			}

			return results;
		}

		public static INode GetResultMember(ResolveResult res)
		{
			if (res is MemberResult)
				return (res as MemberResult).ResolvedMember;
			else if (res is TypeResult)
				return (res as TypeResult).ResolvedTypeDefinition;
			else if (res is ModuleResult)
				return (res as ModuleResult).ResolvedModule;

			return null;
		}

		/// <summary>
		/// If an aliased type result has been passed to this method, it'll return the resolved type.
		/// If aliases were done multiple times, it also tries to skip through these.
		/// 
		/// alias char[] A;
		/// alias A B;
		/// 
		/// var resolvedType=TryRemoveAliasesFromResult(% the member result from B %);
		/// --> resolvedType will be StaticTypeResult from char[]
		/// 
		/// </summary>
		/// <param name="rr"></param>
		/// <returns></returns>
		public static ResolveResult[] TryRemoveAliasesFromResult(params ResolveResult[] initialResults)
		{
			var ret=new List<ResolveResult> (initialResults);
			var l2 = new List<ResolveResult>();

			while (ret.Count > 0)
			{
				foreach (var res in ret)
				{
					var mr = res as MemberResult;
					if (mr!=null &&

						// Alias check
						mr.ResolvedMember is DVariable &&
						(mr.ResolvedMember as DVariable).IsAlias &&

						// Check if it has resolved base types
						mr.MemberBaseTypes != null && 
						mr.MemberBaseTypes.Length > 0)
							l2.AddRange(mr.MemberBaseTypes);
				}

				if (l2.Count < 1)
					break;

				ret.Clear();
				ret.AddRange(l2);
				l2.Clear();
			}

			return ret.ToArray();
		}

		static int bcStack = 0;
		public static TypeResult[] ResolveBaseClass(DClassLike ActualClass, ResolverContext ctxt)
		{
			if (bcStack > 8)
			{
				bcStack--;
				return null;
			}

			if (ActualClass == null || ((ActualClass.BaseClasses == null || ActualClass.BaseClasses.Count < 1) && ActualClass.Name != null && ActualClass.Name.ToLower() == "object"))
				return null;

			var ret = new List<TypeResult>();
			// Implicitly set the object class to the inherited class if no explicit one was done
			var type = (ActualClass.BaseClasses == null || ActualClass.BaseClasses.Count < 1) ? new IdentifierDeclaration("Object") : ActualClass.BaseClasses[0];

			// A class cannot inherit itself
			if (type == null || type.ToString(false) == ActualClass.Name || ActualClass.NodeRoot == ActualClass)
				return null;

			bcStack++;

			/*
			 * If the ActualClass is defined in an other module (so not in where the type resolution has been started),
			 * we have to enable access to the ActualClass's module's imports!
			 * 
			 * module modA:
			 * import modB;
			 * 
			 * class A:B{
			 * 
			 *		void bar()
			 *		{
			 *			fooC(); // Note that modC wasn't imported publically! Anyway, we're still able to access this method!
			 *			// So, the resolver must know that there is a class C.
			 *		}
			 * }
			 * 
			 * -----------------
			 * module modB:
			 * import modC;
			 * 
			 * // --> When being about to resolve B's base class C, we have to use the imports of modB(!), not modA
			 * class B:C{}
			 * -----------------
			 * module modC:
			 * 
			 * class C{
			 * 
			 * void fooC();
			 * 
			 * }
			 */
			ResolveResult[] results = null;

			if (ctxt != null)
			{
				var ctxtOverride = new ResolverContext();

				// Take ctxt's parse cache etc.
				ctxtOverride.ApplyFrom(ctxt);

				// First override the scoped block
				ctxtOverride.ScopedBlock = ActualClass.Parent as IBlockNode;

				// Then override the import cache with imports of the ActualClass's module
				if (ctxt.ScopedBlock != null &&
					ctxt.ScopedBlock.NodeRoot != ActualClass.NodeRoot)
					ctxtOverride.ImportCache = ResolveImports(ActualClass.NodeRoot as DModule, ctxt.ParseCache);

				results = ResolveType(type, ctxtOverride);
			}
			else
			{
				results = ResolveType(type, null, ActualClass.Parent as IBlockNode);
			}

			if (results != null)
				foreach (var i in results)
					if (i is TypeResult)
						ret.Add(i as TypeResult);
			bcStack--;

			return ret.Count > 0 ? ret.ToArray() : null;
		}

		/// <summary>
		/// The variable's or method's base type will be resolved (if auto type, the intializer's type will be taken).
		/// A class' base class will be searched.
		/// etc..
		/// </summary>
		/// <returns></returns>
		public static ResolveResult HandleNodeMatch(
			INode m,
			ResolverContext ctxt,
			IBlockNode currentlyScopedNode = null,
			ResolveResult resultBase = null, ITypeDeclaration typeBase = null)
		{
			if (currentlyScopedNode == null)
				currentlyScopedNode = ctxt.ScopedBlock;

			stackNum_HandleNodeMatch++;

			//HACK: Really dirty stack overflow prevention via manually counting call depth
			var DoResolveBaseType =
				stackNum_HandleNodeMatch > 5 ?
				false : ctxt.ResolveBaseTypes;
			// Prevent infinite recursion if the type accidently equals the node's name
			if (m.Type != null && m.Type.ToString(false) == m.Name)
				DoResolveBaseType = false;

			if (m is DVariable)
			{
				var v = m as DVariable;

				var memberbaseTypes = DoResolveBaseType ? ResolveType(v.Type, ctxt, currentlyScopedNode) : null;

				// For auto variables, use the initializer to get its type
				if (memberbaseTypes == null && DoResolveBaseType && v.ContainsAttribute(DTokens.Auto) && v.Initializer != null)
				{
					memberbaseTypes = ResolveType(v.Initializer.ExpressionTypeRepresentation, ctxt, currentlyScopedNode);
				}

				// Resolve aliases if wished
				if (ctxt.ResolveAliases && memberbaseTypes != null)
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
							if (mr != null && mr.ResolvedMember is DVariable)
							{
								var dv = mr.ResolvedMember as DVariable;
								// Note: Normally, a variable's base type mustn't be an other variable but an alias defintion...
								if (dv.IsAlias)
								{
									var newRes = ResolveType(dv.Type, ctxt, currentlyScopedNode);
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

				// Note: Also works for aliases! In this case, we simply try to resolve the aliased type, otherwise the variable's base type
				stackNum_HandleNodeMatch--;
				return new MemberResult()
				{
					ResolvedMember = m,
					MemberBaseTypes = memberbaseTypes,
					ResultBase = resultBase,
					TypeDeclarationBase = typeBase
				};
			}
			else if (m is DMethod)
			{
				var method = m as DMethod;

				var methodType = method.Type;

				/*
				 * If a method's type equals null, assume that it's an 'auto' function..
				 * 1) Search for a return statement
				 * 2) Resolve the returned expression
				 * 3) Use that one as the method's type
				 */
				if (methodType == null && method.Body != null)
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
						currentlyScopedNode = method;
						methodType = returnStmt.ReturnExpression.ExpressionTypeRepresentation;
					}
				}

				var ret = new MemberResult()
				{
					ResolvedMember = m,
					MemberBaseTypes = DoResolveBaseType ? ResolveType(methodType, ctxt, currentlyScopedNode) : null,
					ResultBase = resultBase,
					TypeDeclarationBase = typeBase
				};
				stackNum_HandleNodeMatch--;
				return ret;
			}
			else if (m is DClassLike)
			{
				var Class = m as DClassLike;

				var bc = DoResolveBaseType ? ResolveBaseClass(Class, ctxt) : null;

				stackNum_HandleNodeMatch--;
				return new TypeResult()
				{
					ResolvedTypeDefinition = Class,
					BaseClass = bc,
					ResultBase = resultBase,
					TypeDeclarationBase = typeBase
				};
			}
			else if (m is IAbstractSyntaxTree)
			{
				stackNum_HandleNodeMatch--;
				return new ModuleResult()
				{
					ResolvedModule = m as IAbstractSyntaxTree,
					AlreadyTypedModuleNameParts = 1,
					ResultBase = resultBase,
					TypeDeclarationBase = typeBase
				};
			}
			else if (m is DEnum)
			{
				stackNum_HandleNodeMatch--;
				return new TypeResult()
				{
					ResolvedTypeDefinition = m as IBlockNode,
					ResultBase = resultBase,
					TypeDeclarationBase = typeBase
				};
			}
			else if (m is TemplateParameterNode)
			{
				stackNum_HandleNodeMatch--;
				return new MemberResult()
				{
					ResolvedMember = m,
					TypeDeclarationBase = typeBase,
					ResultBase = resultBase
				};
			}

			stackNum_HandleNodeMatch--;
			// This never should happen..
			return null;
		}

		static int stackNum_HandleNodeMatch = 0;
		public static ResolveResult[] HandleNodeMatches(IEnumerable<INode> matches,
			ResolverContext ctxt,
			IBlockNode currentlyScopedNode = null,
			ResolveResult resultBase = null,
			ITypeDeclaration TypeDeclaration = null)
		{
			var rl = new List<ResolveResult>();

			if (matches != null)
				foreach (var m in matches)
				{
					if (m == null)
						continue;

					var res = HandleNodeMatch(m, ctxt, currentlyScopedNode, resultBase, typeBase: TypeDeclaration);
					if (res != null)
						rl.Add(res);
				}
			return rl.ToArray();
		}
	}
}
