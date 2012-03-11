using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ASTScanner
{
	public abstract class AbstractVisitor
	{
		#region Properties
		public static DVariable __ctfe;
		Dictionary<string, List<string>> scannedModules = new Dictionary<string, List<string>>();

		static ImportStatement.Import _objectImport = new ImportStatement.Import
		{
			ModuleIdentifier = new IdentifierDeclaration("object")
		};

		ResolverContextStack ctxt;
		public ResolverContextStack Context { 
			get{ return ctxt;}
			set{ ctxt=value;}
		}
		#endregion

		#region Constructor
		public AbstractVisitor(ResolverContextStack context)
		{
			ctxt=context;
		}

		static AbstractVisitor()
		{
			__ctfe = new DVariable
			{
				Name = "__ctfe",
				Type = new DTokenDeclaration(DTokens.Bool),
				Initializer = new TokenExpression(DTokens.True),
				Description = @"The __ctfe boolean pseudo-vari­able, 
which eval­u­ates to true at com­pile time, but false at run time, 
can be used to pro­vide an al­ter­na­tive ex­e­cu­tion path 
to avoid op­er­a­tions which are for­bid­den at com­pile time.",
			};

			__ctfe.Attributes.Add(new DAttribute(DTokens.Static));
			__ctfe.Attributes.Add(new DAttribute(DTokens.Const));
		}
		#endregion

        /// <summary>
        /// Return true if search shall stop(!), false if search shall go on
        /// </summary>
		protected abstract bool HandleItem(INode n);

		protected virtual bool HandleItems(IEnumerable<INode> nodes)
		{
            foreach (var n in nodes)
                if (HandleItem(n))
                    return true;
            return false;
		}

		public void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers= MemberFilter.All)
		{
			// 1)
			if (ctxt.ScopedStatement != null && 
				IterateThroughItemHierarchy(ctxt.ScopedStatement, Caret, VisibleMembers) &&
					(ctxt.CurrentContext.Options.HasFlag(ResolutionOptions.StopAfterFirstOverloads) || 
					ctxt.CurrentContext.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch)))
					return;

			var curScope = ctxt.ScopedBlock;

			bool breakOnNextScope = false;
			bool breakImmediately = ctxt.CurrentContext.Options == ResolutionOptions.StopAfterFirstMatch;

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
                        if (curWatchedClass.TemplateParameters != null &&
                            (breakOnNextScope=HandleItems(curWatchedClass.TemplateParameterNodes as IEnumerable<INode>)) && breakImmediately)
                                return;

						foreach (var m in curWatchedClass)
						{
							var dm2 = m as DNode;
							var dm3 = m as DMethod; // Only show normal & delegate methods
							if (!CanAddMemberOfType(VisibleMembers, m) || dm2 == null ||
								(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate)))
								continue;

							// Add static and non-private members of all base classes; 
							// Add everything if we're still handling the currently scoped class
                            if ((curWatchedClass == curScope || dm2.IsStatic || !dm2.ContainsAttribute(DTokens.Private)) && 
                                (breakOnNextScope= HandleItem(m)) && 
								breakImmediately)
                                    return;
						}

						// Stop adding if Object class level got reached
						if (!string.IsNullOrEmpty(curWatchedClass.Name) && curWatchedClass.Name.ToLower() == "object")
							break;

						// 3)
						var baseclassDefs = DResolver.ResolveBaseClass(curWatchedClass, ctxt);

						if (baseclassDefs == null || baseclassDefs.Length < 0)
							break;
						if (curWatchedClass == baseclassDefs[0].Node)
							break;

						curWatchedClass = baseclassDefs[0].Node as DClassLike;
					}
				}
				else if (curScope is DMethod)
				{
					var dm = curScope as DMethod;

					// Add 'out' variable if typing in the out test block currently
                    if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out && 
                        (breakOnNextScope=HandleItem(new DVariable // Create pseudo-variable
                            {
                                Name = dm.OutResultVariable.Id as string,
                                NameLocation = dm.OutResultVariable.Location,
                                Type = dm.Type, // TODO: What to do on auto functions?
                                Parent = dm,
                                StartLocation = dm.OutResultVariable.Location,
                                EndLocation = dm.OutResultVariable.EndLocation,
                            })) && 
							breakImmediately)
                            return;

                    if (VisibleMembers.HasFlag(MemberFilter.Variables) && 
						(breakOnNextScope=HandleItems(dm.Parameters)) && 
						breakImmediately)
                            return;

                    if (dm.TemplateParameters != null && 
						(breakOnNextScope= HandleItems(dm.TemplateParameterNodes as IEnumerable<INode>)) && 
						breakImmediately)
                            return;

					// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
					// except AdditionalChildren:
                    foreach (var ch in dm.AdditionalChildren)
                        if (CanAddMemberOfType(VisibleMembers, ch) && 
							(breakOnNextScope=HandleItem(ch) && breakImmediately))
                            return;

					// If the method is a nested method,
					// this method won't be 'linked' to the parent statement tree directly - 
					// so, we've to gather the parent method and add its locals to the return list
					if (dm.Parent is DMethod)
					{
						var nestedBlock = (dm.Parent as DMethod).GetSubBlockAt(Caret);

						// Search for the deepest statement scope and add all declarations done in the entire hierarchy
                        if (nestedBlock != null && 
                            (breakOnNextScope=IterateThroughItemHierarchy(nestedBlock.SearchStatementDeeply(Caret), Caret, VisibleMembers)) &&
							breakImmediately)
                            return;
					}
				}
				else foreach (var n in curScope)
					{
						// Add anonymous enums' items
						if (n is DEnum && string.IsNullOrEmpty(n.Name) && CanAddMemberOfType(VisibleMembers, n))
						{
                            if ((breakOnNextScope = HandleItems((n as DEnum).Children)) && breakImmediately)
                                return;
							continue;
						}

						var dm3 = n as DMethod; // Only show normal & delegate methods
						if (
							!CanAddMemberOfType(VisibleMembers, n) ||
							(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate)))
							continue;

                        if ((breakOnNextScope= HandleItem(n)) && breakImmediately)
                            return;
					}

				// Handle imports
                if (curScope is DBlockNode)
                    if ((breakOnNextScope = HandleDBlockNode((DBlockNode)curScope, VisibleMembers)) && breakImmediately)
                        return;

				if (breakOnNextScope && ctxt.CurrentContext.Options.HasFlag(ResolutionOptions.StopAfterFirstOverloads))
					return;

				curScope = curScope.Parent as IBlockNode;
			}

			// Add __ctfe variable
            if (!breakOnNextScope && CanAddMemberOfType(VisibleMembers, __ctfe))
                if (HandleItem(__ctfe))
                    return;
		}

		static bool CanAddMemberOfType(MemberFilter VisibleMembers, INode n)
		{
			if (n is DMethod)
				return !string.IsNullOrEmpty(n.Name) && VisibleMembers.HasFlag(MemberFilter.Methods);

			if (n is DVariable)
			{
				var d = n as DVariable;

				// Only add aliases if at least types,methods or variables shall be shown.
				if (d.IsAlias)
					return
						VisibleMembers.HasFlag(MemberFilter.Methods) ||
						VisibleMembers.HasFlag(MemberFilter.Types) ||
						VisibleMembers.HasFlag(MemberFilter.Variables);

				return VisibleMembers.HasFlag(MemberFilter.Variables);
			}

			if (n is DClassLike)
				return VisibleMembers.HasFlag(MemberFilter.Types);

			if (n is DEnum)
			{
				var d = n as DEnum;

				// Only show enums if a) they're named and types are allowed or b) variables are allowed
				return (d.IsAnonymous ? false : VisibleMembers.HasFlag(MemberFilter.Types)) ||
					VisibleMembers.HasFlag(MemberFilter.Variables);
			}

			return false;
		}

		/// <summary>
		/// Walks up the statement scope hierarchy and enlists all declarations that have been made BEFORE the caret position. 
		/// (If CodeLocation.Empty given, this parameter will be ignored)
		/// </summary>
        /// <returns>True if scan shall stop, false if not</returns>
		bool IterateThroughItemHierarchy(IStatement Statement, CodeLocation Caret, MemberFilter VisibleMembers)
		{
			// To a prevent double entry of the same declaration, skip a most scoped declaration first
			if (Statement is DeclarationStatement)
				Statement = Statement.Parent;

			while (Statement != null)
			{
				if (Statement is IDeclarationContainingStatement)
				{
					var decls = ((IDeclarationContainingStatement)Statement).Declarations;

					if (decls != null)
						foreach (var decl in decls)
						{
							if (Caret != CodeLocation.Empty)
							{
								if (Caret < decl.StartLocation)
									continue;

								var dv = decl as DVariable;
								if (dv != null &&
									dv.Initializer != null &&
									!(Caret < dv.Initializer.Location ||
									Caret > dv.Initializer.EndLocation))
									continue;
							}

                            if (HandleItem(decl))
                                return true;
						}
				}

				if (Statement is StatementContainingStatement)
					foreach (var s in (Statement as StatementContainingStatement).SubStatements)
					{
						if (s is ImportStatement)
						{
							/*
							 * void foo()
							 * {
							 * 
							 *	writeln(); -- error, writeln undefined
							 *	
							 *  import std.stdio;
							 *  
							 *  writeln(); -- ok
							 * 
							 * }
							 */
							if (Caret < s.StartLocation && Caret != CodeLocation.Empty)
								continue;

							// Selective imports were handled in the upper section already!

							var impStmt = (ImportStatement)s;

                            foreach (var imp in impStmt.Imports)
                                if (string.IsNullOrEmpty(imp.ModuleAlias))
                                    if (HandleNonAliasedImport(imp, VisibleMembers))
                                        return true;
						}
						else if (s is ExpressionStatement)
						{
							// TODO: Parse MixinStatements à la mixin("int x" ~ "="~ to!string(5) ~";");
						}
						else if (s is TemplateMixin)
						{
							var tm = s as TemplateMixin;

							if (string.IsNullOrEmpty(tm.MixinId))
							{

							}
							else
							{

							}
						}
					}

				Statement = Statement.Parent;
			}

            return false;
		}

		#region Imports

		/* 
		 * public imports only affect the directly superior module:
		 *
		 * module A:
		 * import B;
		 * 
		 * foo(); // Will fail, because foo wasn't found
		 * 
		 * ---------------------------
		 * module B:
		 * import C;
		 * 
		 * ---------------------------
		 * module C:
		 * public import D;
		 * 
		 * ---------------------------
		 * module D:
		 * void foo() {}
		 * 
		 * ---------------------------
		 * Whereas
		 * module B:
		 * public import C;
		 * 
		 * -- will compile because we have a closed import hierarchy in which all imports are public.
		 * 
		 */

		/// <summary>
		/// Handle the node's static statements (but not the node itself)
		/// </summary>
		bool HandleDBlockNode(DBlockNode dbn, MemberFilter VisibleMembers, bool takePublicImportsOnly=false)
		{
			if (dbn != null && dbn.StaticStatements != null)
			{
				foreach (var stmt in dbn.StaticStatements)
				{
					var dstmt = stmt as IDeclarationContainingStatement;

					if (takePublicImportsOnly &&
						dstmt is ImportStatement &&
						!DAttribute.ContainsAttribute(dstmt.Attributes, DTokens.Public))
						continue;

					/*
					 * Mainly used for selective imports/import module aliases
					 */
                    if (dstmt != null && dstmt.Declarations != null)
                        foreach (var d in dstmt.Declarations)
                            if (HandleItem(d)) //TODO: Handle visibility?
                                return true;

					if (dstmt is ImportStatement)
					{
						var impStmt = (ImportStatement)dstmt;

                        foreach (var imp in impStmt.Imports)
                            if (string.IsNullOrEmpty(imp.ModuleAlias))
                                if (HandleNonAliasedImport(imp, VisibleMembers))
                                    return true;
					}
				}
			}

			// Every module imports 'object' implicitly
            if (!takePublicImportsOnly)
                if (HandleNonAliasedImport(_objectImport, VisibleMembers))
                    return true;

            return false;
		}

		bool HandleNonAliasedImport(ImportStatement.Import imp, MemberFilter VisibleMembers)
		{
			if (imp == null || imp.ModuleIdentifier == null)
				return false;

			var thisModuleName = (ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree).ModuleName;
			var moduleName = imp.ModuleIdentifier.ToString();

			List<string> seenModules = null;

			if(!scannedModules.TryGetValue(thisModuleName,out seenModules))
				seenModules = scannedModules[thisModuleName] = new List<string>();
			else if (seenModules.Contains(moduleName))
				return false;
			seenModules.Add(moduleName);

			foreach (var module in ctxt.ParseCache.LookupModuleName(moduleName))
			{
				if (module == null || module.FileName == (ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName)
					continue;

                if (HandleItem(module))
                    return true;

				foreach (var i in module)
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
                            if (HandleItems((i as DEnum).Children))
                                return true;
                            continue;
                        }

                        if (dn.IsPublic && !dn.ContainsAttribute(DTokens.Package) &&
                            CanAddMemberOfType(VisibleMembers, dn))
                            if (HandleItem(dn))
                                return true;
                    }
                    else
                        if (HandleItem(i))
                            return true;
				}

                if (HandleDBlockNode(module as DBlockNode, VisibleMembers, true))
                    return true;
			}
            return false;
		}

		#endregion
	}
}
