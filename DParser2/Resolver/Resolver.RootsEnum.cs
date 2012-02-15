using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver
{
	public abstract class RootsEnum
	{
		public static DVariable __ctfe;
		ResolverContext ctxt;
		public ResolverContext Context { 
			get{ return ctxt;}
			set{ ctxt=value;}
		}

		public RootsEnum(ResolverContext context)
		{
			ctxt=context;
		}

		static RootsEnum()
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

		protected abstract void HandleItem(INode n);

		protected virtual void HandleItems(IEnumerable<INode> nodes)
		{
			foreach (var n in nodes)
				HandleItem(n);
		}

		public void IterateThroughScopeLayers(CodeLocation Caret, MemberTypes VisibleMembers= MemberTypes.All)
		{
			#region Current module/scope related members

			// 1)
			if (ctxt.ScopedStatement != null)
				IterateThroughItemHierarchy(ctxt.ScopedStatement, Caret);

			var curScope = ctxt.ScopedBlock;

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
							HandleItems(curWatchedClass.TemplateParameterNodes as IEnumerable<INode>);

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
								HandleItem(m);
						}

						// Stop adding if Object class level got reached
						if (!string.IsNullOrEmpty(curWatchedClass.Name) && curWatchedClass.Name.ToLower() == "object")
							break;

						// 3)
						var baseclassDefs = DResolver.ResolveBaseClass(curWatchedClass, ctxt);

						if (baseclassDefs == null || baseclassDefs.Length < 0)
							break;
						if (curWatchedClass == baseclassDefs[0].ResolvedTypeDefinition)
							break;

						curWatchedClass = baseclassDefs[0].ResolvedTypeDefinition as DClassLike;
					}
				}
				else if (curScope is DMethod)
				{
					var dm = curScope as DMethod;

					// Add 'out' variable if typing in the out test block currently
					if (dm.OutResultVariable != null && dm.Out != null && dm.GetSubBlockAt(Caret) == dm.Out)
						HandleItem(new DVariable // Create pseudo-variable
							{
								Name = dm.OutResultVariable.Value as string,
								NameLocation = dm.OutResultVariable.Location,
								Type = dm.Type, // TODO: What to do on auto functions?
								Parent = dm,
								StartLocation = dm.OutResultVariable.Location,
								EndLocation = dm.OutResultVariable.EndLocation,
							});

					if (VisibleMembers.HasFlag(MemberTypes.Variables))
						HandleItems(dm.Parameters);

					if (dm.TemplateParameters != null)
						HandleItems(dm.TemplateParameterNodes as IEnumerable<INode>);

					// The method's declaration children are handled above already via BlockStatement.GetItemHierarchy().
					// except AdditionalChildren:
					foreach (var ch in dm.AdditionalChildren)
						if (CanAddMemberOfType(VisibleMembers, ch))
							HandleItem(ch);

					// If the method is a nested method,
					// this method won't be 'linked' to the parent statement tree directly - 
					// so, we've to gather the parent method and add its locals to the return list
					if (dm.Parent is DMethod)
					{
						var nestedBlock = (dm.Parent as DMethod).GetSubBlockAt(Caret);

						// Search for the deepest statement scope and add all declarations done in the entire hierarchy
						if(nestedBlock!=null)
							IterateThroughItemHierarchy(nestedBlock.SearchStatementDeeply(Caret), Caret);
					}
				}
				else foreach (var n in curScope)
					{
						// Add anonymous enums' items
						if (n is DEnum && string.IsNullOrEmpty(n.Name) && CanAddMemberOfType(VisibleMembers, n))
						{
							HandleItems((n as DEnum).Children);
							continue;
						}

						var dm3 = n as DMethod; // Only show normal & delegate methods
						if (
							!CanAddMemberOfType(VisibleMembers, n) ||
							(dm3 != null && !(dm3.SpecialType == DMethod.MethodType.Normal || dm3.SpecialType == DMethod.MethodType.Delegate)))
							continue;

						HandleItem(n);
					}

				curScope = curScope.Parent as IBlockNode;
			}

			// Add __ctfe variable
			if(CanAddMemberOfType(VisibleMembers, __ctfe))
				HandleItem(__ctfe);

			#endregion

			#region Global members
			// Add all non-private and non-package-only nodes
			foreach (var mod in ctxt.ImportCache)
			{
				if (mod.FileName == (ctxt.ScopedBlock.NodeRoot as IAbstractSyntaxTree).FileName)
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
							HandleItems((i as DEnum).Children);
							continue;
						}

						if (dn.IsPublic && !dn.ContainsAttribute(DTokens.Package) &&
							CanAddMemberOfType(VisibleMembers, dn))
							HandleItem(dn);
					}
					else
						HandleItem(i);
				}
			}
			#endregion
		}

		static bool CanAddMemberOfType(MemberTypes VisibleMembers, INode n)
		{
			if (n is DMethod)
				return (n as DMethod).Name != "" && VisibleMembers.HasFlag(MemberTypes.Methods);

			if (n is DVariable)
			{
				var d = n as DVariable;

				// Only add aliases if at least types,methods or variables shall be shown.
				if (d.IsAlias)
					return
						VisibleMembers.HasFlag(MemberTypes.Methods) ||
						VisibleMembers.HasFlag(MemberTypes.Types) ||
						VisibleMembers.HasFlag(MemberTypes.Variables);

				return VisibleMembers.HasFlag(MemberTypes.Variables);
			}

			if (n is DClassLike)
				return VisibleMembers.HasFlag(MemberTypes.Types);

			if (n is DEnum)
			{
				var d = n as DEnum;

				// Only show enums if a) they're named and types are allowed or b) variables are allowed
				return (d.IsAnonymous ? false : VisibleMembers.HasFlag(MemberTypes.Types)) ||
					VisibleMembers.HasFlag(MemberTypes.Variables);
			}

			return false;
		}

		/// <summary>
		/// Walks up the statement scope hierarchy and enlists all declarations that have been made BEFORE the caret position. 
		/// (If CodeLocation.Empty given, this parameter will be ignored)
		/// </summary>
		public void IterateThroughItemHierarchy(IStatement Statement, CodeLocation Caret)
		{
			// To a prevent double entry of the same declaration, skip a most scoped declaration first
			if (Statement is DeclarationStatement)
				Statement = Statement.Parent;

			while (Statement != null)
			{
				if (Statement is IDeclarationContainingStatement)
				{
					var decls = (Statement as IDeclarationContainingStatement).Declarations;

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

							HandleItem(decl);
						}
				}

				if (Statement is StatementContainingStatement)
					foreach (var s in (Statement as StatementContainingStatement).SubStatements)
					{
						if (s is MixinStatement)
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
		}
	}
}
