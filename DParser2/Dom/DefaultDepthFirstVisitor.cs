using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;

namespace D_Parser.Dom
{
	public abstract class DefaultDepthFirstVisitor : DVisitor
	{
		public virtual void VisitChildren(ContainerExpression x)
		{
			
		}

		public virtual void VisitChildren(ITypeDeclaration td)
		{

		}

		#region Nodes
		public virtual void VisitChildren(IBlockNode block)
		{
			foreach (var n in block)
				n.Accept(this);
			
		}

		/// <summary>
		/// Calls VisitDNode already.
		/// </summary>
		public virtual void VisitBlock(DBlockNode block)
		{
			VisitChildren(block);
			VisitDNode(block);

			if (block.StaticStatements.Count != 0)
				foreach (var s in block.StaticStatements)
					s.Accept(this);

			if (block.MetaBlocks.Count != 0)
				foreach (var mb in block.MetaBlocks)
					mb.Accept(this);
		}

		public virtual void Visit(DEnumValue n)
		{
			Visit((DVariable)n);
		}

		public virtual void Visit(DVariable n)
		{
			VisitDNode(n);
			if (n.Initializer != null)
				n.Initializer.Accept(this);
		}

		public virtual void Visit(DMethod n)
		{
			VisitChildren(n);
			VisitDNode(n);

			if(n.Parameters!=null)
				foreach (var par in n.Parameters)
					par.Accept(this);

			if (n.In != null)
				n.In.Accept(this);
			if (n.Body != null)
				n.Body.Accept(this);
			if (n.Out != null)
				n.Out.Accept(this);

			if (n.OutResultVariable != null)
				n.OutResultVariable.Accept(this);
		}

		public virtual void Visit(DClassLike n)
		{
			VisitBlock(n);

			foreach (var bc in n.BaseClasses)
				bc.Accept(this);
		}

		public virtual void Visit(DEnum n)
		{
			VisitBlock(n);
		}

		public virtual void Visit(DModule n)
		{
			VisitBlock(n);

			if (n.OptionalModuleStatement != null)
				n.OptionalModuleStatement.Accept(this);
		}

		public virtual void Visit(TemplateParameterNode n)
		{
			VisitDNode(n);

			n.TemplateParameter.Accept(this);
		}

		public virtual void VisitDNode(DNode n)
		{
			if (n.TemplateParameters != null)
				foreach (var tp in n.TemplateParameters)
					tp.Accept(this);

			if (n.TemplateConstraint != null)
				n.TemplateConstraint.Accept(this);

			if (n.Attributes != null && n.Attributes.Count != 0)
				foreach (var attr in n.Attributes)
					attr.Accept(this);

			if (n.Type != null)
				n.Type.Accept(this);
		}

		public virtual void VisitAttribute(DAttribute attribute) {}

		public void VisitAttribute(DeclarationCondition declCond)
		{
			if (declCond.Condition != null)
				declCond.Condition.Accept(this);
		}

		public void VisitAttribute(PragmaAttribute pragma)
		{
			if (pragma.Arguments != null && pragma.Arguments.Length != 0)
				foreach (var arg in pragma.Arguments)
					arg.Accept(this);
		}
		#endregion

		#region Statements
		/// <summary>
		/// Visit abstract stmt
		/// </summary>
		public virtual void VisitChildren(StatementContainingStatement stmtContainer)
		{
			if (stmtContainer.SubStatements != null)
				foreach (var s in stmtContainer.SubStatements)
					s.Accept(this);

			VisitAbstractStmt(stmtContainer);
		}

		public virtual void Visit(ModuleStatement s)
		{
			VisitAbstractStmt(s);
			s.ModuleName.Accept(this);
		}

		public virtual void Visit(ImportStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(BlockStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(LabeledStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(IfStatement s)
		{
			VisitChildren(s);

			if (s.IfCondition != null)
				s.IfCondition.Accept(this);

			//TODO: Are the declarations also in the statements?
			if (s.IfVariable != null)
				foreach (var d in s.IfVariable)
					d.Accept(this);
		}

		public virtual void Visit(WhileStatement s)
		{
			VisitChildren(s);

			if (s.Condition != null)
				s.Condition.Accept(this);
		}

		public virtual void Visit(ForStatement s)
		{
			// Also visits 'Initialize'
			VisitChildren(s);

			if (s.Test != null)
				s.Test.Accept(this);
			if (s.Increment != null)
				s.Increment.Accept(this);
		}

		public virtual void Visit(ForeachStatement s)
		{
			VisitChildren(s);

			if (s.ForeachTypeList != null)
				foreach (var t in s.ForeachTypeList)
					t.Accept(this);

			if (s.UpperAggregate != null)
				s.UpperAggregate.Accept(this);
			if (s.Aggregate != null)
				s.Aggregate.Accept(this);
		}

		public virtual void Visit(SwitchStatement s)
		{
			VisitChildren(s);

			if (s.SwitchExpression != null)
				s.SwitchExpression.Accept(this);
		}

		public virtual void Visit(SwitchStatement.CaseStatement s)
		{
			VisitChildren(s);

			if (s.ArgumentList != null)
				s.ArgumentList.Accept(this);
			if (s.LastExpression != null)
				s.LastExpression.Accept(this);
		}

		public virtual void Visit(SwitchStatement.DefaultStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(ContinueStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(BreakStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ReturnStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(GotoStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(WithStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(SynchronizedStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TryStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TryStatement.CatchStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TryStatement.FinallyStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ThrowStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ScopeGuardStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AsmStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.PragmaStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.AssertStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.DebugStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ConditionStatement.VersionStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VolatileStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.ExpressionStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.DeclarationStatement s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.TemplateMixin s)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Statements.VersionDebugSpecification s)
		{
			throw new NotImplementedException();
		}

		public virtual void VisitAbstractStmt(AbstractStatement stmt)
		{
			if (stmt.Attributes != null && stmt.Attributes.Length != 0)
				foreach (var attr in stmt.Attributes)
					attr.Accept(this);
		}
		#endregion

		#region Expressions
		public virtual void Visit(Expression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssignExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ConditionalExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.OrOrExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AndAndExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.XorExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.OrExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AndExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.EqualExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IdendityExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.RelExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.InExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ShiftExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AddExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.MulExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.CatExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PowExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_And x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Increment x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Decrement x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Mul x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Add x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Sub x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Not x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Cat x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.UnaryExpression_Type x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.NewExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AnonymousClassExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.DeleteExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.CastExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Access x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Increment x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Decrement x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_MethodCall x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Index x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.PostfixExpression_Slice x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TemplateInstanceExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IdentifierExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TokenExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TypeDeclarationExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ArrayLiteralExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssocArrayExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.FunctionLiteral x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.AssertExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.MixinExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ImportExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TypeidExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.IsExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.TraitsExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.SurroundingParenthesesExpression x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.VoidInitializer x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.ArrayInitializer x)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(Expressions.StructInitializer x)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Decls
		public virtual void Visit(IdentifierDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DTokenDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ArrayDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(DelegateDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(PointerDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(MemberFunctionAttributeDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(TypeOfDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(VectorDeclaration td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(VarArgDecl td)
		{
			throw new NotImplementedException();
		}

		public virtual void Visit(ITemplateParameterDeclaration td)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Meta decl blocks
		public void Visit(MetaDeclarationBlock metaDeclarationBlock)
		{
			throw new NotImplementedException();
		}

		public void Visit(AttributeMetaDeclarationBlock attributeMetaDeclarationBlock)
		{
			throw new NotImplementedException();
		}

		public void Visit(AttributeMetaDeclarationSection attributeMetaDeclarationSection)
		{
			throw new NotImplementedException();
		}

		public void Visit(ElseMetaDeclarationBlock elseMetaDeclarationBlock)
		{
			throw new NotImplementedException();
		}

		public void Visit(ElseMetaDeclaration elseMetaDeclaration)
		{
			throw new NotImplementedException();
		}

		public void Visit(AttributeMetaDeclaration attributeMetaDeclaration)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Template parameters
		public void Visit(TemplateTypeParameter templateTypeParameter)
		{
			throw new NotImplementedException();
		}

		public void Visit(TemplateThisParameter templateThisParameter)
		{
			throw new NotImplementedException();
		}

		public void Visit(TemplateValueParameter templateValueParameter)
		{
			throw new NotImplementedException();
		}

		public void Visit(TemplateAliasParameter templateAliasParameter)
		{
			throw new NotImplementedException();
		}

		public void Visit(TemplateTupleParameter templateTupleParameter)
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
