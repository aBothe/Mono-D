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
	public class ResolverContext
	{
		public IBlockNode ScopedBlock;
		public IStatement ScopedStatement;

		public IEnumerable<IAbstractSyntaxTree> ParseCache;
		public IEnumerable<IAbstractSyntaxTree> ImportCache;
		public bool ResolveBaseTypes = true;
		public bool ResolveAliases = true;

		Dictionary<object, Dictionary<string, ResolveResult[]>> resolvedTypes = new Dictionary<object, Dictionary<string, ResolveResult[]>>();

		public void ApplyFrom(ResolverContext other)
		{
			if (other == null)
				return;

			ScopedBlock = other.ScopedBlock;
			ScopedStatement = other.ScopedStatement;
			ParseCache = other.ParseCache;
			ImportCache = other.ImportCache;

			ResolveBaseTypes = other.ResolveBaseTypes;
			ResolveAliases = other.ResolveAliases;

			resolvedTypes = other.resolvedTypes;
		}

		/// <summary>
		/// Stores scoped-block dependent type dictionaries, which store all types that were already resolved once
		/// </summary>
		public Dictionary<object, Dictionary<string, ResolveResult[]>> ResolvedTypes
		{
			get{ return resolvedTypes; }
		}

		public void TryAddResults(string TypeDeclarationString, ResolveResult[] NodeMatches, IBlockNode ScopedType = null)
		{
			if (ScopedType == null)
				ScopedType = ScopedBlock;

			Dictionary<string, ResolveResult[]> subDict = null;

			if (!ResolvedTypes.TryGetValue(ScopedType, out subDict))
				ResolvedTypes.Add(ScopedType, subDict = new Dictionary<string, ResolveResult[]>());

			if (!subDict.ContainsKey(TypeDeclarationString))
				subDict.Add(TypeDeclarationString, NodeMatches);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="TypeDeclarationString"></param>
		/// <param name="NodeMatches"></param>
		/// <param name="ScopedType">If null, ScopedBlock variable will be assumed</param>
		/// <returns></returns>
		public bool TryGetAlreadyResolvedType(string TypeDeclarationString, out ResolveResult[] NodeMatches, object ScopedType = null)
		{
			if (ScopedType == null)
				ScopedType = ScopedBlock;

			Dictionary<string, ResolveResult[]> subDict = null;

			if (ScopedType!=null && !ResolvedTypes.TryGetValue(ScopedType, out subDict))
			{
				NodeMatches = null;
				return false;
			}

			if(subDict!=null)
				return subDict.TryGetValue(TypeDeclarationString, out NodeMatches);

			NodeMatches = null;
			return false;
		}
	}

	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public class DResolver
	{
		[Flags]
		public enum MemberTypes
		{
			Imports = 1,
			Variables = 1 << 1,
			Methods = 1 << 2,
			Types = 1 << 3,
			Keywords = 1 << 4,

			All = Imports | Variables | Methods | Types | Keywords
		}

		public static bool CanAddMemberOfType(MemberTypes VisibleMembers, INode n)
		{
			if(n is DMethod)
				return (n as DMethod).Name!="" && VisibleMembers.HasFlag(MemberTypes.Methods);

			if(n is DVariable)
			{
				var d=n as DVariable;

				// Only add aliases if at least types,methods or variables shall be shown.
				if(d.IsAlias)
					return 
						VisibleMembers.HasFlag(MemberTypes.Methods) || 
						VisibleMembers.HasFlag(MemberTypes.Types) || 
						VisibleMembers.HasFlag(MemberTypes.Variables);

				return VisibleMembers.HasFlag(MemberTypes.Variables);
			}

			if (n is DClassLike)
				return VisibleMembers.HasFlag(MemberTypes.Types);

			if(n is DEnum)
			{
				var d=n as DEnum;

				// Only show enums if a) they're named and types are allowed or b) variables are allowed
				return (d.IsAnonymous ? false : VisibleMembers.HasFlag(MemberTypes.Types)) ||
					VisibleMembers.HasFlag(MemberTypes.Variables);
			}

			return false;
		}

		/// <summary>
		/// Returns a list of all items that can be accessed in the current scope.
		/// </summary>
		/// <param name="ScopedBlock"></param>
		/// <param name="ImportCache"></param>
		/// <returns></returns>
		public static IEnumerable<INode> EnumAllAvailableMembers(
			IBlockNode ScopedBlock
			, IStatement ScopedStatement,
			CodeLocation Caret,
			IEnumerable<IAbstractSyntaxTree> CodeCache,
			MemberTypes VisibleMembers)
		{
			/* 
			 * Shown items:
			 * 1) First walk through the current scope.
			 * 2) Walk up the node hierarchy and add all their items (private as well as public members).
			 * 3) Resolve base classes and add their non-private|static members.
			 * 
			 * 4) Then add public members of the imported modules 
			 */
			var ret = new List<INode>();
			var ImportCache = ResolveImports(ScopedBlock.NodeRoot as DModule, CodeCache);

			#region Current module/scope related members
			
			// 1)
			if (ScopedStatement != null)
			{
				ret.AddRange(BlockStatement.GetItemHierarchy(ScopedStatement, Caret));
			}

			var curScope = ScopedBlock;

			// 2)
			while (curScope != null)
			{
				// Walk up inheritance hierarchy
				if (curScope is DClassLike)
				{
					var curWatchedClass = curScope as DClassLike;
					// MyClass > BaseA > BaseB > Object
					while (curWatchedClass != null)
					{
						if (curWatchedClass.TemplateParameters != null)
							ret.AddRange(curWatchedClass.TemplateParameterNodes as IEnumerable<INode>);

						foreach (var m in curWatchedClass)
						{
							var dm2 = m as DNode;
							var dm3 = m as DMethod; // Only show normal & delegate methods
							if (!CanAddMemberOfType(VisibleMembers, m) || dm2 == null ||
								(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate))
								)
								continue;

							// Add static and non-private members of all base classes; 
							// Add everything if we're still handling the currently scoped class
							if (curWatchedClass == curScope || dm2.IsStatic || !dm2.ContainsAttribute(DTokens.Private))
								ret.Add(m);
						}

						// Stop adding if Object class level got reached
						if (!string.IsNullOrEmpty(curWatchedClass.Name) && curWatchedClass.Name.ToLower() == "object")
							break;

						// 3)
						var baseclassDefs = DResolver.ResolveBaseClass(curWatchedClass, new ResolverContext { 
							ParseCache = CodeCache,
							ScopedBlock=ScopedBlock,
							ImportCache = ImportCache });

						if (baseclassDefs == null || baseclassDefs.Length<0)
							break;
						if (curWatchedClass == baseclassDefs[0].ResolvedTypeDefinition)
							break;
						curWatchedClass = baseclassDefs[0].ResolvedTypeDefinition as DClassLike;
					}
				}
				else if (curScope is DMethod)
				{
					var dm = curScope as DMethod;

					if (VisibleMembers.HasFlag(MemberTypes.Variables))
						ret.AddRange(dm.Parameters);

					if (dm.TemplateParameters != null)
						ret.AddRange(dm.TemplateParameterNodes as IEnumerable<INode>);

					// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
					// except AdditionalChildren:
					foreach (var ch in dm.AdditionalChildren)
						if (CanAddMemberOfType(VisibleMembers, ch))
							ret.Add(ch);

					// If the method is a nested method,
					// this method won't be 'linked' to the parent statement tree directly - 
					// so, we've to gather the parent method and add its locals to the return list
					if (dm.Parent is DMethod)
					{
						var parDM = dm.Parent as DMethod;
						var nestedBlock = parDM.GetSubBlockAt(Caret);

						// Search for the deepest statement scope and add all declarations done in the entire hierarchy
						ret.AddRange(BlockStatement.GetItemHierarchy(nestedBlock.SearchStatementDeeply(Caret), Caret));
					}
				}
				else foreach (var n in curScope)
					{
						// Add anonymous enums' items
						if (n is DEnum && string.IsNullOrEmpty(n.Name) && CanAddMemberOfType(VisibleMembers, n))
						{
							ret.AddRange((n as DEnum).Children);
							continue;
						}

						var dm3 = n as DMethod; // Only show normal & delegate methods
						if (
							!CanAddMemberOfType(VisibleMembers, n) ||
							(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate)))
							continue;

						ret.Add(n);
					}

				curScope = curScope.Parent as IBlockNode;
			}
			#endregion

			#region Global members
			// Add all non-private and non-package-only nodes
			foreach (var mod in ImportCache)
			{
				if (mod.FileName == (ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName)
					continue;

				foreach (var i in mod)
				{
					var dn = i as DNode;
					if (dn != null)
					{
						// Add anonymous enums' items
						if (dn is DEnum && 
							string.IsNullOrEmpty(i.Name) && 
							dn.IsPublic && 
							!dn.ContainsAttribute(DTokens.Package) && 
							CanAddMemberOfType(VisibleMembers, i))
						{
							ret.AddRange((i as DEnum).Children);
							continue;
						}

						if (dn.IsPublic && !dn.ContainsAttribute(DTokens.Package) &&
							CanAddMemberOfType(VisibleMembers, dn))
							ret.Add(dn);
					}
					else 
						ret.Add(i);
				}
			}
			#endregion

			if (ret.Count < 1)
				return null;
			return ret;
		}

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

		public static IAbstractSyntaxTree SearchModuleInCache(IEnumerable<IAbstractSyntaxTree> HayStack, string ModuleName)
		{
			foreach (var m in HayStack)
			{
				if (m.Name == ModuleName)
					return m;
			}
			return null;
		}

		/// <summary>
		/// Trivial class which cares about locating Comments and other non-code blocks within a code file
		/// </summary>
		public class CommentSearching
		{
			public enum TokenContext
			{
				None=0,
				String=2<<0,
				VerbatimString=2<<1,
				LineComment=2<<5,
				BlockComment=2<<2,
				NestedComment=2<<3,
				CharLiteral=2<<4
			}

			public static TokenContext GetTokenContext(string Text, int Offset)
			{
				int _u;
				return GetTokenContext(Text, Offset, out _u, out _u);
			}

			public static TokenContext GetTokenContext(string Text, int Offset, out int lastBeginOffset, out int lastEndOffset)
			{
				char cur = '\0', peekChar = '\0';
				int off = 0;
				bool IsInString = false;
				bool IsInLineComment = false;
				bool IsInBlockComment = false;
				bool IsInNestedBlockComment = false;
				bool IsChar = false;
				bool IsVerbatimString = false;
				bool IsAlternateVerbatimString = false;

				lastBeginOffset = -1;
				lastEndOffset = -1;

				/*
				 * Continue searching if
				 *	1) Caret offset hasn't been reached yet
				 *	2) An end of a context block is still expected
				 */
				bool isBeyondCaret = false; // Only reset bool states if NOT beyond target offset
				while (off < Offset - 1 || 
					(isBeyondCaret=(lastBeginOffset != -1 && lastEndOffset == -1 && off < Text.Length)))
				{
					cur = Text[off];
					if (off < Text.Length - 1) 
						peekChar = Text[off + 1];

					// String check
					if (!IsInLineComment && !IsInBlockComment && !IsInNestedBlockComment)
					{
						if (!IsInString)
						{
							// Char handling
							if (!IsChar && cur == '\'')
							{
								lastBeginOffset = off;
								lastEndOffset = -1;
								IsChar = true;
							}
							else
							{
								// Single quote char escape
								if (cur == '\\' && peekChar == '\'')
								{
									off += 2;
									continue;
								}
								else if (cur == '\'')
								{
									IsChar = isBeyondCaret;
									lastEndOffset = off;
								}
							}

							// Verbatim string recognition
							if (cur == 'r' && peekChar == '\"')
							{
								lastBeginOffset = off;
								lastEndOffset = -1;
								off++;
								IsInString = IsVerbatimString = true;
							}
							else if (cur == '`')
							{
								lastBeginOffset = off;
								lastEndOffset = -1;
								IsInString = IsAlternateVerbatimString = true;
							}
							// if not, test for normal string literals
							else if (cur == '\"')
							{
								IsInString = true;
							}
						}
						else
						{
							// Verbatim double quote char escape
							if ((IsVerbatimString && cur == '\"' && peekChar == '\"') ||
								// Normal backslash escape
								(cur == '\\' && peekChar == '\\'))
							{
								off += 2;
								continue;
							}
							else if (IsAlternateVerbatimString && cur=='`')
							{
								IsInString = IsAlternateVerbatimString = isBeyondCaret;
								lastEndOffset = off;
							}
							else if (cur == '\"')
							{
								IsInString = IsVerbatimString = isBeyondCaret;
								lastEndOffset = off;
							}
						}
					}

					if (!IsInString && !IsChar)
					{
						// Line comment check
						if (!IsInBlockComment && !IsInNestedBlockComment)
						{
							if (cur == '/' && peekChar == '/')
							{
								IsInLineComment = true;
								lastBeginOffset = off;
								lastEndOffset = -1;
							} 
							else if (IsInLineComment && cur == '\n')
							{
								IsInLineComment = isBeyondCaret;
								lastEndOffset = off;
							}
						}

						// Block comment check
						if (cur == '/' && peekChar == '*')
						{
							IsInBlockComment = true;
							lastBeginOffset = off;
							lastEndOffset = -1;
						}
						else if (IsInBlockComment && cur == '*' && peekChar == '/')
						{
							IsInBlockComment = isBeyondCaret;
							off++;
							lastEndOffset = off+1;
						}

						// Nested comment check
						if (!IsInString && cur == '/' && peekChar == '+')
						{
							IsInNestedBlockComment = true;
							lastBeginOffset = off;
							lastEndOffset = -1;
						} 
						else if (IsInNestedBlockComment && cur == '+' && peekChar == '/')
						{
							IsInNestedBlockComment = isBeyondCaret;
							off++;
							lastEndOffset = off+1;
						}
					}

					off++;
				}

				var ret=TokenContext.None;

				if (IsChar)
					ret |= TokenContext.CharLiteral;
				if (IsInLineComment)
					ret |= TokenContext.LineComment;
				if (IsInBlockComment)
					ret |= TokenContext.BlockComment;
				else if (IsInNestedBlockComment)
					ret |= TokenContext.NestedComment;
				if (IsInString)
					ret |= TokenContext.String;
				if (IsVerbatimString || IsAlternateVerbatimString)
					ret |= TokenContext.VerbatimString;

				return ret;
			}

			public static bool IsInCommentAreaOrString(string Text, int Offset)
			{
				int _u;
				return GetTokenContext(Text, Offset, out _u, out _u)!=TokenContext.None;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="code"></param>
		/// <param name="CurrentScope"></param>
		/// <param name="caretOffset"></param>
		/// <param name="caretLocation"></param>
		/// <param name="LastParsedObject"></param>
		/// <param name="PreviouslyParsedObject"></param>
		/// <param name="ExpectedIdentifier"></param>
		/// <returns>Either CurrentScope, a BlockStatement object that is associated with the parent method or a complete new DModule object</returns>
		public static object FindCurrentCaretContext(string code,
			IBlockNode CurrentScope,
			int caretOffset, CodeLocation caretLocation,
			out ParserTrackerVariables TrackerVariables)
		{
			bool ParseDecl = false;

			int blockStart = 0;
			var blockStartLocation = CurrentScope.BlockStartLocation;

			if (CurrentScope is DMethod)
			{
				var block = (CurrentScope as DMethod).GetSubBlockAt(caretLocation);

				if (block != null)
					blockStart = DocumentHelper.LocationToOffset(code, blockStartLocation = block.StartLocation);
				else
					return FindCurrentCaretContext(code, CurrentScope.Parent as IBlockNode, caretOffset, caretLocation, out TrackerVariables);
			}
			else if (CurrentScope != null)
			{
				if (CurrentScope.BlockStartLocation.IsEmpty || caretLocation < CurrentScope.BlockStartLocation && caretLocation > CurrentScope.StartLocation)
				{
					ParseDecl = true;
					blockStart = DocumentHelper.LocationToOffset(code, blockStartLocation= CurrentScope.StartLocation);
				}
				else
					blockStart = DocumentHelper.LocationToOffset(code, CurrentScope.BlockStartLocation);
			}

			if (blockStart >= 0 && caretOffset - blockStart > 0)
			{
				var codeToParse = code.Substring(blockStart, caretOffset - blockStart);

				var psr = DParser.Create(new StringReader(codeToParse));

				/* Deadly important! For correct resolution behaviour, 
				 * it is required to set the parser virtually to the blockStart position, 
				 * so that everything using the returned object is always related to 
				 * the original code file, not our code extraction!
				 */
				psr.Lexer.SetInitialLocation(blockStartLocation);

				object ret = null;

				if (CurrentScope == null || CurrentScope is IAbstractSyntaxTree)
					ret = psr.Parse();
				else if (CurrentScope is DMethod)
				{
					psr.Step();
					ret = psr.BlockStatement();
				}
				else if (CurrentScope is DModule)
					ret = psr.Root();
				else
				{
					psr.Step();
					if (ParseDecl)
					{
						var ret2 = psr.Declaration();

						if (ret2 != null && ret2.Length > 0)
							ret = ret2[0];
					}
					else
					{
						IBlockNode bn = null;
						if (CurrentScope is DClassLike)
						{
							var t = new DClassLike((CurrentScope as DClassLike).ClassType);
							t.AssignFrom(CurrentScope);
							bn = t;
						}
						else if (CurrentScope is DEnum)
						{
							var t = new DEnum();
							t.AssignFrom(CurrentScope);
							bn = t;
						}

						bn.Clear();

						psr.ClassBody(bn);
						ret = bn;
					}
				}

				TrackerVariables = psr.TrackerVariables;

				return ret;
			}

			TrackerVariables = null;

			return null;
		}

		/// <summary>
		/// Parses the code between the start of the parent method's block and the given caret location.
		/// </summary>
		/// <returns>Returns the deepest Statement that exists in the statement hierarchy.</returns>
		public static IStatement ParseBlockStatementUntilCaret(string code, DMethod MethodParent, int caretOffset, CodeLocation caretLocation)
		{
			//HACK: Clear anonymous decl array to ensure that no duplicates occur when calling DParser.ParseBlockStatement()
			MethodParent.AdditionalChildren.Clear();

			var oldBlock = MethodParent.GetSubBlockAt(caretLocation);
			if (oldBlock == null)
				return null;
			var blockOpenerLocation = oldBlock.StartLocation;
			var blockOpenerOffset = blockOpenerLocation.Line <= 0 ? blockOpenerLocation.Column :
				DocumentHelper.LocationToOffset(code, blockOpenerLocation);

			if (blockOpenerOffset >= 0 && caretOffset - blockOpenerOffset > 0)
			{
				var codeToParse = code.Substring(blockOpenerOffset, caretOffset - blockOpenerOffset);

				/*
				 * So, if we're inside of a method, we parse all its 'contents' (statements, expressions, declarations etc.)
				 * to achieve a fully updated insight.
				 */
				var newStmt = DParser.ParseBlockStatement(codeToParse, blockOpenerLocation, MethodParent);

				var ret = newStmt.SearchStatementDeeply(caretLocation);

				return ret == null ? newStmt : ret;
			}

			return null;
		}

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

				if (targetExpr == null)
					return null;

				startLocation = targetExpr.Location;
				start = DocumentHelper.LocationToOffset(editor.ModuleCode, startLocation);
				IsExpression = true;
			}
			else
			{
				// First check if caret is inside a comment/string etc.
				int lastNonNormalStart = 0;
				int lastNonNormalEnd = 0;
				var caretContext = CommentSearching.GetTokenContext(code, editor.CaretOffset, out lastNonNormalStart, out lastNonNormalEnd);

				// Return if comment etc. found
				if (caretContext != CommentSearching.TokenContext.None)
					return null;

				start = ReverseParsing.SearchExpressionStart(code, editor.CaretOffset - 1,
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
					expr = ExpressionHelper.SearchExpressionDeeply(expr, editor.CaretLocation);

					var ret = ResolveType(expr.ExpressionTypeRepresentation, ctxt);

					if (ret == null && expr != null && !(expr is TokenExpression))
						ret = new[] { new ExpressionResult() { Expression = expr } };

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
				rbases = ResolveType(declaration.InnerDeclaration, ctxtOverride);

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
					int searchToken = string.IsNullOrEmpty(searchIdentifier) ? 0 : DTokens.GetTokenID(searchIdentifier);

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
						// anything else is just a key word, not a type
					}
					// (As usual) Go on searching in the local&global scope(s)
					else
					{
						var matches = new List<INode>();

						// Search in current statement's declarations (if possible)
						var decls = BlockStatement.GetItemHierarchy(ctxt.ScopedStatement, declaration.Location);

						if(decls!=null)
							foreach (var decl in decls)
								if (decl != null && decl.Name == searchIdentifier)
									matches.Add(decl);

						// First search along the hierarchy in the current module
						var curScope = ctxtOverride.ScopedBlock;
						while (curScope != null)
						{
							/* 
							 * If anonymous enum, skip that one, because in the following ScanForNodeIdentifier call, 
							 * its children already become added to the match list
							 */
							if (curScope is DEnum && curScope.Name == "")
								curScope = curScope.Parent as IBlockNode;

							if (curScope is DMethod)
							{
								var dm = curScope as DMethod;

								// If the method is a nested method,
								// this method won't be 'linked' to the parent statement tree directly - 
								// so, we've to gather the parent method and add its locals to the return list
								if (dm.Parent is DMethod)
								{
									var parDM = dm.Parent as DMethod;
									var nestedBlock = parDM.GetSubBlockAt(declaration.Location);
									if (nestedBlock != null)
									{
										// Search for the deepest statement scope and test all declarations done in the entire scope hierarchy
										decls = BlockStatement.GetItemHierarchy(nestedBlock.SearchStatementDeeply(declaration.Location), declaration.Location);

										foreach (var decl in decls)
											// ... Add them if match was found
											if (decl != null && decl.Name == searchIdentifier)
												matches.Add(decl);
									}
								}

								// Do not check further method's children but its (template) parameters
								foreach (var p in dm.Parameters)
									if (p.Name == searchIdentifier)
										matches.Add(p);

								if (dm.TemplateParameters != null)
									foreach (var tp in dm.TemplateParameterNodes)
										if (tp.Name == searchIdentifier)
											matches.Add(tp);
							}
							else
							{
								var m = ScanNodeForIdentifier(curScope, searchIdentifier, ctxtOverride);

								if (m != null)
									matches.AddRange(m);

								var mod = curScope as IAbstractSyntaxTree;
								if (mod != null)
								{
									var modNameParts = mod.ModuleName.Split('.');
									if (!string.IsNullOrEmpty(mod.ModuleName) && modNameParts[0] == searchIdentifier)
										matches.Add(curScope);
								}
							}
							curScope = curScope.Parent as IBlockNode;
						}

						// Then go on searching in the global scope
						var ThisModule =
							currentScopeOverride is IAbstractSyntaxTree ?
								currentScopeOverride as IAbstractSyntaxTree :
								currentScopeOverride.NodeRoot as IAbstractSyntaxTree;
						if (ctxt.ParseCache != null)
							foreach (var mod in ctxt.ParseCache)
							{
								if (mod == ThisModule)
									continue;

								var modNameParts = mod.ModuleName.Split('.');

								if (modNameParts[0] == searchIdentifier)
									matches.Add(mod);
							}

						if (ctxtOverride.ImportCache != null)
							foreach (var mod in ctxtOverride.ImportCache)
							{
								var m = ScanNodeForIdentifier(mod, searchIdentifier, null);
								if (m != null)
									matches.AddRange(m);
							}

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
									if (_m != null) nextResults.AddRange(_m);
								}

								else if (scanResult is TypeResult)
								{
									var tr=scanResult as TypeResult;
									var nodeMatches=ScanNodeForIdentifier(tr.ResolvedTypeDefinition, searchIdentifier, ctxtOverride);

									var results = HandleNodeMatches(
										nodeMatches,
										ctxtOverride, 
										tr.ResolvedTypeDefinition, 
										resultBase: rbase, 
										TypeDeclaration: declaration);

									if (results != null)
										returnedResults.AddRange(results);
								}
								else if (scanResult is ModuleResult)
								{
									var modRes = (scanResult as ModuleResult);

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
										ScanNodeForIdentifier((scanResult as ModuleResult).ResolvedModule, searchIdentifier, ctxtOverride),
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
										var str = (memberType as StaticTypeResult);
										/*
										 * If the member's type is an array, and if our expression contains an index-expression (e.g. myArray[0]),
										 * take the value type of the 
										 */
										// For array and pointer declarations, the StaticTypeResult object contains the array's value type / pointer base type.
										if (str != null && (str.TypeDeclarationBase is ArrayDecl || str.TypeDeclarationBase is PointerDecl))
											returnedResults.AddRange(TryRemoveAliasesFromResult(str.ResultBase));
									}
									else
										returnedResults.Add(memberType);
								}
						}
					}
				}
			#endregion

			if (returnedResults.Count > 0)
			{
				ctxt.TryAddResults(declaration.ToString(), returnedResults.ToArray(), ctxtOverride.ScopedBlock);

				return returnedResults.ToArray();
			}

			return null;
		}

		public class StaticPropertyResolver
		{
			/// <summary>
			/// Tries to resolve a static property's name.
			/// Returns a result describing the theoretical member (".init"-%gt;MemberResult; ".typeof"-&gt;TypeResult etc).
			/// Returns null if nothing was found.
			/// </summary>
			/// <param name="InitialResult"></param>
			/// <returns></returns>
			public static ResolveResult TryResolveStaticProperties(ResolveResult InitialResult, IdentifierDeclaration Identifier, ResolverContext ctxt = null)
			{
				if (InitialResult == null || Identifier == null || InitialResult is ModuleResult)
				{
					return null;
				}

				var propertyName = Identifier.Value as string;
				if (propertyName == null)
					return null;

				INode relatedNode = null;

				if (InitialResult is MemberResult)
					relatedNode = (InitialResult as MemberResult).ResolvedMember;
				else if (InitialResult is TypeResult)
					relatedNode = (InitialResult as TypeResult).ResolvedTypeDefinition;

				#region init
				if (propertyName == "init")
				{
					var prop_Init = new DVariable
					{
						Name = "init",
						Description = "Initializer"
					};

					if (relatedNode != null)
					{
						if (!(relatedNode is DVariable))
						{
							prop_Init.Parent = relatedNode.Parent;
							prop_Init.Type = new IdentifierDeclaration(relatedNode.Name);
						}
						else
						{
							prop_Init.Parent = relatedNode;
							prop_Init.Initializer = (relatedNode as DVariable).Initializer;
							prop_Init.Type = relatedNode.Type;
						}
					}

					return new MemberResult
					{
						ResultBase = InitialResult,
						MemberBaseTypes = new[] { InitialResult },
						TypeDeclarationBase = Identifier,
						ResolvedMember = prop_Init
					};
				}
				#endregion

				#region sizeof
				if (propertyName == "sizeof")
					return new MemberResult
					{
						ResultBase = InitialResult,
						TypeDeclarationBase = Identifier,
						ResolvedMember = new DVariable
						{
							Name = "sizeof",
							Type = new DTokenDeclaration(DTokens.Int),
							Initializer = new IdentifierExpression(4),
							Description = "Size in bytes (equivalent to C's sizeof(type))"
						}
					};
				#endregion

				#region alignof
				if (propertyName == "alignof")
					return new MemberResult
					{
						ResultBase = InitialResult,
						TypeDeclarationBase = Identifier,
						ResolvedMember = new DVariable
						{
							Name = "alignof",
							Type = new DTokenDeclaration(DTokens.Int),
							Description = "Alignment size"
						}
					};
				#endregion

				#region mangleof
				if (propertyName == "mangleof")
					return new MemberResult
					{
						ResultBase = InitialResult,
						TypeDeclarationBase = Identifier,
						ResolvedMember = new DVariable
						{
							Name = "mangleof",
							Type = new IdentifierDeclaration("string"),
							Description = "String representing the ‘mangled’ representation of the type"
						},
						MemberBaseTypes = ResolveType(new IdentifierDeclaration("string"), ctxt)
					};
				#endregion

				#region stringof
				if (propertyName == "stringof")
					return new MemberResult
					{
						ResultBase = InitialResult,
						TypeDeclarationBase = Identifier,
						ResolvedMember = new DVariable
						{
							Name = "stringof",
							Type = new IdentifierDeclaration("string"),
							Description = "String representing the source representation of the type"
						},
						MemberBaseTypes = ResolveType(new IdentifierDeclaration("string"), ctxt)
					};
				#endregion

				bool
					isArray = false,
					isAssocArray = false,
					isInt = false,
					isFloat = false;

				#region See AbsractCompletionSupport.StaticPropertyAddition
				if (InitialResult is StaticTypeResult)
				{
					var srr = InitialResult as StaticTypeResult;

					var type = srr.TypeDeclarationBase;

					// on things like immutable(char), pass by the surrounding attribute..
					while (type is MemberFunctionAttributeDecl)
						type = (type as MemberFunctionAttributeDecl).InnerType;

					if (type is ArrayDecl)
					{
						var ad = type as ArrayDecl;

						// Normal array
						if (ad.KeyType is DTokenDeclaration && DTokens.BasicTypes_Integral[(ad.KeyType as DTokenDeclaration).Token])
							isArray = true;
						// Associative array
						else
							isAssocArray = true;
					}
					else if (!(type is PointerDecl))
					{
						int TypeToken = srr.BaseTypeToken;

						if (TypeToken <= 0 && type is DTokenDeclaration)
							TypeToken = (type as DTokenDeclaration).Token;

						if (TypeToken > 0)
						{
							// Determine whether float by the var's base type
							isInt = DTokens.BasicTypes_Integral[srr.BaseTypeToken];
							isFloat = DTokens.BasicTypes_FloatingPoint[srr.BaseTypeToken];
						}
					}
				}
				else if (InitialResult is ExpressionResult)
				{
					var err = InitialResult as ExpressionResult;
					var expr = err.Expression;

					// 'Skip' surrounding parentheses
					while (expr is SurroundingParenthesesExpression)
						expr = (expr as SurroundingParenthesesExpression).Expression;

					var idExpr = expr as IdentifierExpression;
					if (idExpr != null)
					{
						// Char literals, Integrals types & Floats
						if ((idExpr.Format & LiteralFormat.Scalar) == LiteralFormat.Scalar || idExpr.Format == LiteralFormat.CharLiteral)
						{
							// Floats also imply integral properties
							isInt = true;
							isFloat = (idExpr.Format & LiteralFormat.FloatingPoint) == LiteralFormat.FloatingPoint;
						}
						// String literals
						else if (idExpr.Format == LiteralFormat.StringLiteral || idExpr.Format == LiteralFormat.VerbatimStringLiteral)
						{
							isArray = true;
						}
					}
					// Pointer conversions (e.g. (myInt*).sizeof)
				}
				#endregion

				//TODO: Resolve static [assoc] array props
				if (isArray || isAssocArray)
				{

				}

				return null;
			}
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
		/// Scans through the node. Also checks if n is a DClassLike or an other kind of type node and checks their specific child and/or base class nodes.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="name"></param>
		/// <param name="parseCache">Needed when trying to search base classes</param>
		/// <returns></returns>
		public static INode[] ScanNodeForIdentifier(IBlockNode curScope, string name, ResolverContext ctxt)
		{
			var matches = new List<INode>();

			if (curScope.Count > 0)
				foreach (var n in curScope)
				{
					// Scan anonymous enums
					if (n is DEnum && n.Name == "")
					{
						foreach (var k in n as DEnum)
							if (k.Name == name)
								matches.Add(k);
					}

					if (n.Name == name)
						matches.Add(n);
				}

			// If our current Level node is a class-like, also attempt to search in its baseclass!
			if (curScope is DClassLike)
			{
				var baseClasses = ResolveBaseClass(curScope as DClassLike, ctxt);
				if (baseClasses != null)
					foreach (var i in baseClasses)
					{
						var baseClass = i as TypeResult;
						if (baseClass == null)
							continue;
						// Search for items called name in the base class(es)
						var r = ScanNodeForIdentifier(baseClass.ResolvedTypeDefinition, name, ctxt);

						if (r != null)
							matches.AddRange(r);
					}
			}

			// Check parameters
			if (curScope is DMethod)
			{
				var dm = curScope as DMethod;
				foreach (var ch in dm.Parameters)
				{
					if (name == ch.Name)
						matches.Add(ch);
				}
			}

			// and template parameters
			if (curScope is DNode && (curScope as DNode).TemplateParameters != null)
				foreach (var ch in (curScope as DNode).TemplateParameters)
				{
					if (name == ch.Name)
						matches.Add(new TemplateParameterNode(ch));
				}

			return matches.Count > 0 ? matches.ToArray() : null;
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
					var init = v.Initializer;
					memberbaseTypes = ResolveType(init.ExpressionTypeRepresentation, ctxt, currentlyScopedNode);
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
				//TODO: What about handling 'null'-returns?
				if (methodType == null && method.Body != null)
				{
					ReturnStatement returnStmt = null;
					var list = new List<IStatement> { method.Body };
					var list2 = new List<IStatement>();

					while (returnStmt == null && list.Count > 0)
					{
						foreach (var stmt in list)
						{
							if (stmt is ReturnStatement)
							{
								returnStmt = stmt as ReturnStatement;
								break;
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

			var propertyMethodsToIgnore = new List<string>();

			if (matches != null)
				foreach (var m in matches)
				{
					if (m == null)
						continue;

					var n = m;

					// Replace getter&setter methods inline
					if (m is DMethod && 
						(m as DNode).ContainsPropertyAttribute())
					{
						if (propertyMethodsToIgnore.Contains(m.Name))
							continue;

						var dm = m as DMethod;
						bool isGetter = dm.Parameters.Count < 1;

						var virtPropNode = new DVariable();

						virtPropNode.AssignFrom(dm);

						if (!isGetter)
							virtPropNode.Type = dm.Parameters[0].Type;

						propertyMethodsToIgnore.Add(m.Name);

						n = virtPropNode;
					}

					var res = HandleNodeMatch(n, ctxt, currentlyScopedNode, resultBase, typeBase: TypeDeclaration);
					if (res != null)
						rl.Add(res);
				}
			return rl.ToArray();
		}

		static readonly BitArray sigTokens = DTokens.NewSet(
			DTokens.If,
			DTokens.Foreach,
			DTokens.Foreach_Reverse,
			DTokens.With,
			DTokens.Try,
			DTokens.Catch,
			DTokens.Finally,

			DTokens.Cast // cast(...) myType << Show cc popup after a cast
			);

		/// <summary>
		/// Checks if an identifier is about to be typed. Therefore, we assume that this identifier hasn't been typed yet. 
		/// So, we also will assume that the caret location is the start of the identifier;
		/// </summary>
		public static bool IsTypeIdentifier(string code, int caret)
		{
			//try{
			if (caret < 1)
				return false;

			code = code.Insert(caret, " "); // To ensure correct behaviour, insert a phantom ws after the caret

			// Check for preceding letters
			if (char.IsLetter(code[caret]))
				return true;

			int precedingExpressionOrTypeStartOffset = ReverseParsing.SearchExpressionStart(code, caret);

			if (precedingExpressionOrTypeStartOffset >= caret)
				return false;

			var expressionCode = code.Substring(precedingExpressionOrTypeStartOffset, caret - precedingExpressionOrTypeStartOffset);

			if (string.IsNullOrEmpty(expressionCode) || expressionCode.Trim() == string.Empty)
				return false;

			var lx = new Lexer(new StringReader(expressionCode));

			var firstToken = lx.NextToken();

			if (DTokens.ClassLike[firstToken.Kind])
				return true;

			while (lx.LookAhead.Kind != DTokens.EOF)
				lx.NextToken();

			var lastToken = lx.CurrentToken;

			if (lastToken.Kind == DTokens.Times)
				return false; // TODO: Check if it's an expression or not

			if (lastToken.Kind == DTokens.CloseSquareBracket || lastToken.Kind == DTokens.Identifier)
				return true;

			if (lastToken.Kind == DTokens.CloseParenthesis)
			{
				lx.CurrentToken = firstToken;

				while (lx.LookAhead.Kind != DTokens.OpenParenthesis && lx.LookAhead.Kind != DTokens.EOF)
					lx.NextToken();

				if (sigTokens[lx.CurrentToken.Kind])
					return false;
				else
					return true;
			}

			//}catch(Exception ex) { }
			return false;
		}

		public class ArgumentsResolutionResult
		{
			public bool IsMethodArguments;
			public bool IsTemplateInstanceArguments;

			public IExpression ParsedExpression;

			public ResolveResult[] ResolvedTypesOrMethods;

			public readonly Dictionary<IExpression, ResolveResult[]> TemplateArguments = new Dictionary<IExpression, ResolveResult[]>();
			/// <summary>
			/// Stores the already typed arguments (Expressions) + their resolved types.
			/// The value part will be null if nothing could get returned.
			/// </summary>
			public readonly Dictionary<IExpression, ResolveResult[]> Arguments = new Dictionary<IExpression, ResolveResult[]>();

			/// <summary>
			///	Identifies the currently called method overload. Is an index related to <see cref="ResolvedTypesOrMethods"/>
			/// </summary>
			public int CurrentlyCalledMethod;
			public IExpression CurrentlyTypedArgument
			{
				get
				{
					if (Arguments != null && Arguments.Count > CurrentlyTypedArgumentIndex)
					{
						int i = 0;
						foreach (var kv in Arguments)
						{
							if (i == CurrentlyTypedArgumentIndex)
								return kv.Key;
							i++;
						}
					}
					return null;
				}
			}
			public int CurrentlyTypedArgumentIndex;
		}

		public static ArgumentsResolutionResult ResolveArgumentContext(
			string code,
			int caretOffset,
			CodeLocation caretLocation,
			DMethod MethodScope,
			IEnumerable<IAbstractSyntaxTree> parseCache, IEnumerable<IAbstractSyntaxTree> ImportCache)
		{
			var ctxt = new ResolverContext { ScopedBlock = MethodScope, ParseCache = parseCache, ImportCache=ImportCache };

			#region Parse the code between the last block opener and the caret

			var curMethodBody = MethodScope.GetSubBlockAt(caretLocation);

			if (curMethodBody == null && MethodScope.Parent is DMethod)
			{
				MethodScope = MethodScope.Parent as DMethod;
				curMethodBody = MethodScope.GetSubBlockAt(caretLocation);
			}

			if (curMethodBody == null)
				return null;

			var blockOpenerLocation = curMethodBody.StartLocation;
			var blockOpenerOffset = blockOpenerLocation.Line <= 0 ? blockOpenerLocation.Column :
				DocumentHelper.LocationToOffset(code, blockOpenerLocation);

			if (blockOpenerOffset >= 0 && caretOffset - blockOpenerOffset > 0)
			{
				var codeToParse = code.Substring(blockOpenerOffset, caretOffset - blockOpenerOffset);

				curMethodBody = DParser.ParseBlockStatement(codeToParse, blockOpenerLocation, MethodScope);

				if (curMethodBody != null)
					ctxt.ScopedStatement = curMethodBody.SearchStatementDeeply(caretLocation);
			}

			if (curMethodBody == null || ctxt.ScopedStatement == null)
				return null;
			#endregion

			// Scan statement for method calls or template instantiations
			var e = DResolver.SearchForMethodCallsOrTemplateInstances(ctxt.ScopedStatement, caretLocation);

			/*
			 * 1) foo(			-- normal arguments only
			 * 2) foo!(...)(	-- normal arguments + template args
			 * 3) foo!(		-- template args only
			 * 4) new myclass(  -- ctor call
			 * 5) new myclass!( -- ditto
			 * 6) new myclass!(...)(
			 * 7) mystruct(		-- opCall call
			 */
			var res = new ArgumentsResolutionResult() { ParsedExpression = e };

			ITypeDeclaration methodIdentifier = null;

			// 1), 2)
			if (e is PostfixExpression_MethodCall)
			{
				res.IsMethodArguments = true;
				var call = e as PostfixExpression_MethodCall;

				if (call.Arguments != null)
				{
					int i = 0;
					foreach (var arg in call.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

				methodIdentifier = call.PostfixForeExpression.ExpressionTypeRepresentation;

			}
			// 3)
			else if (e is TemplateInstanceExpression)
			{
				var templ = e as TemplateInstanceExpression;

				res.IsTemplateInstanceArguments = true;

				if (templ.Arguments != null)
				{
					int i = 0;
					foreach (var arg in templ.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

                methodIdentifier = new IdentifierDeclaration(templ.TemplateIdentifier.Value) { InnerDeclaration=templ.InnerDeclaration };
			}
			else if (e is NewExpression)
			{
				var ne = e as NewExpression;

				if (ne.Arguments != null)
				{
					int i = 0;
					foreach (var arg in ne.Arguments)
					{
						if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
						{
							res.CurrentlyTypedArgumentIndex = i;
							break;
						}
						i++;
					}
				}

				methodIdentifier = ne.ExpressionTypeRepresentation;
			}

			if (methodIdentifier == null)
				return null;

			// Resolve all types, methods etc. which belong to the methodIdentifier
			res.ResolvedTypesOrMethods = ResolveType(methodIdentifier, ctxt);

			if (res.ResolvedTypesOrMethods == null)
				return res;

			// 4),5),6)
			if (e is NewExpression)
			{
				var substitutionList = new List<ResolveResult>();
				foreach (var rr in res.ResolvedTypesOrMethods)
					if (rr is TypeResult)
					{
						var classDef = (rr as TypeResult).ResolvedTypeDefinition as DClassLike;

						if (classDef == null)
							continue;

						//TODO: Regard protection attributes for ctor members
						foreach (var i in classDef)
							if (i is DMethod && (i as DMethod).SpecialType == DMethod.MethodType.Constructor)
								substitutionList.Add(HandleNodeMatch(i, ctxt, resultBase: rr));
					}

				if (substitutionList.Count > 0)
					res.ResolvedTypesOrMethods = substitutionList.ToArray();
			}

			// 7)
			else if (e is PostfixExpression_MethodCall)
			{
				var substitutionList = new List<ResolveResult>();

				var nonAliases=TryRemoveAliasesFromResult(res.ResolvedTypesOrMethods);

				foreach (var rr in nonAliases)
					if (rr is TypeResult)
					{
						var classDef = (rr as TypeResult).ResolvedTypeDefinition as DClassLike;

						if (classDef == null)
							continue;

						//TODO: Regard protection attributes for opCall members
						foreach (var i in classDef)
							if (i is DMethod && i.Name == "opCall")
								substitutionList.Add(HandleNodeMatch(i, ctxt, resultBase: rr));
					}

				if (substitutionList.Count > 0)
					nonAliases = substitutionList.ToArray();

				res.ResolvedTypesOrMethods = nonAliases;
			}

			return res;
		}

		public static IExpression SearchForMethodCallsOrTemplateInstances(IStatement Statement, CodeLocation Caret)
		{
			IExpression curExpression = null;
			INode curDeclaration = null;

			/*
			 * Step 1: Step down the statement hierarchy to find the stmt that's most next to Caret
			 * Note: As long we haven't found any fitting elements, go on searching
			 */
			while (Statement != null && curExpression == null && curDeclaration == null)
			{
				if (Statement is IExpressionContainingStatement)
				{
					var exprs = (Statement as IExpressionContainingStatement).SubExpressions;

					if (exprs != null && exprs.Length > 0)
						foreach (var expr in exprs)
							if (expr != null && Caret >= expr.Location && Caret <= expr.EndLocation)
							{
								curExpression = expr;
								break;
							}
				}

				if (Statement is IDeclarationContainingStatement)
				{
					var decls = (Statement as IDeclarationContainingStatement).Declarations;

					if (decls != null && decls.Length > 0)
						foreach (var decl in decls)
							if (decl != null && Caret >= decl.StartLocation && Caret <= decl.EndLocation)
							{
								curDeclaration = decl;
								break;
							}
				}

				if (Statement is StatementContainingStatement)
				{
					var stmts = (Statement as StatementContainingStatement).SubStatements;

					bool foundDeeperStmt = false;

					if (stmts != null && stmts.Length > 0)
						foreach (var stmt in stmts)
							if (stmt != null && Caret >= stmt.StartLocation && Caret <= stmt.EndLocation)
							{
								foundDeeperStmt = true;
								Statement = stmt;
								break;
							}

					if (foundDeeperStmt)
						continue;
				}

				break;
			}

			if (curDeclaration == null && curExpression == null)
				return null;


			/*
			 * Step 2: If a declaration was found, check for its inner elements
			 */
			if (curDeclaration != null)
			{
				if (curDeclaration is DVariable)
				{
					var dv = curDeclaration as DVariable;

					if (dv.Initializer != null && Caret >= dv.Initializer.Location && Caret <= dv.Initializer.EndLocation)
						curExpression = dv.Initializer;
				}

				//TODO: Watch the node's type! Over there, there also can be template instances..
			}

			if (curExpression != null)
			{
				IExpression curMethodOrTemplateInstance = null;

				while (curExpression != null)
				{
					if (!(curExpression.Location <= Caret || curExpression.EndLocation >= Caret))
						break;

					if (curExpression is PostfixExpression_MethodCall)
						curMethodOrTemplateInstance = curExpression;

					else if (curExpression is TemplateInstanceExpression)
						curMethodOrTemplateInstance = curExpression;
                    else if (curExpression is PostfixExpression_Access && 
                        (curExpression as PostfixExpression_Access).TemplateOrIdentifier is TemplateInstanceExpression)
                        curMethodOrTemplateInstance = curExpression.ExpressionTypeRepresentation as TemplateInstanceExpression;

					else if (curExpression is NewExpression)
						curMethodOrTemplateInstance = curExpression;
                    
					if (curExpression is ContainerExpression)
					{
						var currentContainer = curExpression as ContainerExpression;

						var subExpressions = currentContainer.SubExpressions;
						bool foundMatch = false;
						if (subExpressions != null && subExpressions.Length > 0)
							foreach (var se in subExpressions)
								if (se != null && Caret >= se.Location && Caret <= se.EndLocation)
								{
									curExpression = se;
									foundMatch = true;
									break;
								}

						if (foundMatch)
							continue;
					}
					break;
				}

				return curMethodOrTemplateInstance;
			}


			return null;
		}
	}

	/// <summary>
	/// Helper class for e.g. finding the initial offset of a statement.
	/// </summary>
	public class ReverseParsing
	{
		static IList<string> preParenthesisBreakTokens = new List<string> { "if", "while", "for", "foreach", "foreach_reverse", "with", "try", "catch", "finally", "synchronized", "pragma" };

		public static int SearchExpressionStart(string Text, int CaretOffset, int MinimumSearchOffset=0)
		{
			if (CaretOffset > Text.Length)
				throw new ArgumentOutOfRangeException("CaretOffset", "Caret offset must be smaller than text length");
			else if (CaretOffset == Text.Length)
				Text += ' ';

			// At first we only want to find the beginning of our identifier list
			// later we will pass the text beyond the beginning to the parser - there we parse all needed expressions from it
			int IdentListStart = -1;

			/*
			T!(...)>.<
			 */

			int isComment = 0;
			bool isString = false, expectDot = false, hadDot = true;
			bool hadString = false;
			var bracketStack = new Stack<char>();

			var identBuffer = "";
			bool hadBraceOpener = false;
			int lastBraceOpenerOffset = 0;

			bool stopSeeking = false;

			// Step backward
			for (int i = CaretOffset; i >= MinimumSearchOffset && !stopSeeking; i--)
			{
				IdentListStart = i;
				var c = Text[i];
				var str = Text.Substring(i);
				char p = ' ';
				if (i > 0) p = Text[i - 1];

				// Primitive comment check
				if (!isString && c == '/' && (p == '*' || p == '+'))
					isComment++;
				if (!isString && isComment > 0 && (c == '+' || c == '*') && p == '/')
					isComment--;

				// Primitive string check
				//TODO: "blah">.<
				hadString = false;
				if (isComment < 1 && c == '"' && p != '\\')
				{
					isString = !isString;

					if (!isString)
						hadString = true;
				}

				// If string or comment, just continue
				if (isString || isComment > 0)
					continue;

				// If between brackets, skip
				if (bracketStack.Count > 0 && c != bracketStack.Peek())
					continue;

				// Bracket check
				if (hadDot)
					switch (c)
					{
						case ']':
							bracketStack.Push('[');
							continue;
						case ')':
							bracketStack.Push('(');
							continue;
						case '}':
							if (bracketStack.Count < 1)
							{
								IdentListStart++;
								stopSeeking = true;
								continue;
							}
							bracketStack.Push('{');
							continue;

						case '[':
						case '(':
						case '{':
							if (bracketStack.Count > 0 && bracketStack.Peek() == c)
							{
								bracketStack.Pop();
								if (c == '(' && p == '!') // Skip template stuff
									i--;
							}
							else if (c == '{')
							{
								stopSeeking = true;
								IdentListStart++;
							}
							else
							{
								if (c == '(' && p == '!') // Skip template stuff
									i--;

								lastBraceOpenerOffset = IdentListStart;
								// e.g. foo>(< bar| )
								hadBraceOpener = true;
								identBuffer = "";
							}
							continue;
					}

				// whitespace check
				if (Char.IsWhiteSpace(c)) { if (hadDot) expectDot = false; else expectDot = true; continue; }

				if (c == '.')
				{
					hadBraceOpener = false;
					identBuffer = "";
					expectDot = false;
					hadDot = true;
					continue;
				}

				/*
				 * abc
				 * abc . abc
				 * T!().abc[]
				 * def abc.T
				 */
				if (Char.IsLetterOrDigit(c) || c == '_')
				{
					hadDot = false;

					if (!expectDot)
					{
						identBuffer += c;

						if (!hadBraceOpener)
							continue;
						else if (!preParenthesisBreakTokens.Contains(identBuffer))
							continue;
						else
							IdentListStart = lastBraceOpenerOffset;
					}
				}

				// Only re-increase our caret offset if we did not break because of a string..
				// otherwise, we'd return the offset after the initial string quote
				if (!hadString)
					IdentListStart++;
				stopSeeking = true;
			}

			return IdentListStart;
		}
	}

	public class DocumentHelper
	{
		public static CodeLocation OffsetToLocation(string Text, int Offset)
		{
			int line = 1;
			int col = 1;

			char c = '\0';
			for (int i = 0; i < Offset; i++)
			{
				c = Text[i];

				col++;

				if (c == '\n')
				{
					line++;
					col = 1;
				}
			}

			return new CodeLocation(col, line);
		}

		public static int LocationToOffset(string Text, CodeLocation Location)
		{
			int line = 1;
			int col = 1;

			int i = 0;
			for (; i < Text.Length && !(line >= Location.Line && col >= Location.Column); i++)
			{
				col++;

				if (Text[i] == '\n')
				{
					line++;
					col = 1;
				}
			}

			return i;
		}
	}
}
