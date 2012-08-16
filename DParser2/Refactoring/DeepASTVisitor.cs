using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Refactoring
{
	/// <summary>
	/// Visits an entire AST including all its expressions, statements and type declarations.
	/// Made to inspect all kinds of identifiable syntax regions, such as single identifiers or template instance expressions.
	/// </summary>
	public abstract class DeepASTVisitor
	{
		/// <summary></summary>
		/// <param name="o">Will always be of type
		/// PostfixExpression_Access, IdentifierExpression, TemplateInstanceExpression or IdentifierDeclaration</param>
		protected abstract void Handle(ISyntaxRegion o);

		protected virtual void OnScopeChanged(IBlockNode scopedBlock) { }
		protected virtual void OnScopeChanged(IStatement scopedStatement) { }

		protected void S(INode n)
		{
			if (n == null)
				return;

			// Set new scope
			var bn = n as IBlockNode;
			if (bn!=null)
				OnScopeChanged(bn);

			if (n.Type != null)
				S(n.Type);

			// Scan generic properties
			if (n is DNode)
			{
				var dn = (DNode)n;

				if (dn.TemplateConstraint != null)
					S(dn.TemplateConstraint);

				//TODO: Template params still missing
				if (dn.TemplateParameters != null)
					foreach (var tp in dn.TemplateParameters)
					{
						if (tp is TemplateValueParameter)
						{
							var tvp = (TemplateValueParameter)tp;

							S(tvp.Type);
							if (tvp.DefaultExpression != null)
								S(tvp.DefaultExpression);
							if (tvp.SpecializationExpression != null)
								S(tvp.SpecializationExpression);
						}
					}
			}

			if (bn is DBlockNode)
			{
				var dbn = (DBlockNode)bn;

				foreach (var s in dbn.StaticStatements)
					S(s);
			}

			// Scan specific properties
			if (n is DModule)
			{
				var dm = (DModule)n;

				if (dm.OptionalModuleStatement != null)
					S(dm.OptionalModuleStatement.ModuleName);
			}
			else if (n is DVariable)
			{
				var dv = (DVariable)n;

				if (dv.Initializer != null)
					S(dv.Initializer);
			}
			else if (n is DMethod)
			{
				var dm = (DMethod)n;

				if (dm.Parameters != null)
					foreach (var m in dm.Parameters)
						S(m);

				if (dm.AdditionalChildren.Count > 0)
					foreach (var m in dm.AdditionalChildren)
						S(m);

				S(dm.In);
				S(dm.Out);
				S(dm.Body);

				// Don't scan the method's declarations - it's done in the body analysis already
				return;
			}
			else if (bn is DClassLike)
			{
				foreach (var bc in ((DClassLike)bn).BaseClasses)
					S(bc);
			}

			// Scan all sub members
			if (bn!=null)
				foreach (var m in bn)
					S(m);
		}

		protected void S(IStatement s)
		{
			OnScopeChanged(s);

			if (s is StatementContainingStatement)
			{
				var sstmts = ((StatementContainingStatement)s).SubStatements;

				if (sstmts != null && sstmts.Length > 0)
					foreach (var stmt in sstmts)
						S(stmt);
			}

			else if (s is ImportStatement)
			{
				var impStmt = (ImportStatement)s;

				foreach (var imp in impStmt.Imports)
					S(imp.ModuleIdentifier);

				if (impStmt.ImportBinding != null)
					S(impStmt.ImportBinding.Module.ModuleIdentifier);
			}

			if (s is IDeclarationContainingStatement && !(s is BlockStatement))
			{
				var decls = ((IDeclarationContainingStatement)s).Declarations;

				if (decls != null && decls.Length > 0)
					foreach (var d in decls)
						S(d);

				// Do not implicitly scan variable initializers..
				// Variable types have to be parsed.
				if (s is DeclarationStatement)
					return;
			}

			if (s is IExpressionContainingStatement)
			{
				var exprs = ((IExpressionContainingStatement)s).SubExpressions;

				if (exprs != null && exprs.Length > 0)
					foreach (var e in exprs)
						if (e != null)
							S(e);
			}
		}

		protected void S(IExpression x)
		{
			if (x is UnaryExpression_Type)
				S(((UnaryExpression_Type)x).Type);
			else if (x is NewExpression)
				S(((NewExpression)x).Type);
			else if (x is PostfixExpression_Access)
				Handle(x);
			else if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
				Handle(x);
			else if (x is TemplateInstanceExpression)
				Handle(x);
			else if (x is ContainerExpression)
			{
				var ec = (ContainerExpression)x;
				var subex = ec.SubExpressions;

				if (subex != null && subex.Length != 0)
					foreach (var sx in subex)
						S(sx);
			}
		}

		protected void S(ITypeDeclaration type)
		{
			while (type != null)
			{
				if (type is DelegateDeclaration)
					foreach (var p in ((DelegateDeclaration)type).Parameters)
						S(p);
				else if (type is ArrayDecl)
				{
					var ad = (ArrayDecl)type;

					if (ad.KeyExpression != null)
						S(ad.KeyExpression);
					if (ad.KeyType != null)
						S(ad.KeyType);
				}
				else if (type is TemplateInstanceExpression)
				{
					var tix = (TemplateInstanceExpression)type;

					Handle(type);

					if (tix.Arguments != null && tix.Arguments.Length!=0)
						foreach (var x in tix.Arguments)
							S(x);
				}
				else if (type is IdentifierDeclaration)
				{
					Handle(type);
					break;
				}

				type = type.InnerDeclaration;
			}
		}

		/// <summary>
		/// Used to extract the adequate code location + the identifier length
		/// </summary>
		public static CodeLocation ExtractIdLocation(ISyntaxRegion sr, out int idLength)
		{
			if (sr is IdentifierDeclaration)
			{
				var id = (IdentifierDeclaration)sr;

				idLength = id.Id.Length;
				return id.Location;
			}
			else if (sr is IdentifierExpression)
			{
				var id = (IdentifierExpression)sr;
				idLength = ((string)id.Value).Length;
				return id.Location;
			}
			else if (sr is TemplateInstanceExpression)
			{
				var tix = (TemplateInstanceExpression)sr;
				idLength = tix.TemplateIdentifier.Id.Length;
				return tix.TemplateIdentifier.Location;
			}
			else if (sr is PostfixExpression_Access)
				return ExtractIdLocation(((PostfixExpression_Access)sr).AccessExpression, out idLength);
			else if (sr is NewExpression)
				return ExtractIdLocation(((NewExpression)sr).Type, out idLength);

			idLength = 0;
			return CodeLocation.Empty;
		}
	}
}
