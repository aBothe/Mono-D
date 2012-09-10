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
			VisitAbstractStmt(s);
		}

		public virtual void Visit(BreakStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(ReturnStatement s)
		{
			VisitAbstractStmt(s);
			if (s.ReturnExpression != null)
				s.ReturnExpression.Accept(this);
		}

		public virtual void Visit(GotoStatement s)
		{
			VisitAbstractStmt(s);
			if (s.CaseExpression != null)
				s.CaseExpression.Accept(this);
		}

		public virtual void Visit(WithStatement s)
		{
			VisitChildren(s);

			if (s.WithExpression != null)
				s.WithExpression.Accept(this);
			if (s.WithSymbol != null)
				s.WithSymbol.Accept(this);
		}

		public virtual void Visit(SynchronizedStatement s)
		{
			VisitChildren(s);

			if (s.SyncExpression != null)
				s.SyncExpression.Accept(this);
		}

		public virtual void Visit(TryStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(TryStatement.CatchStatement s)
		{
			VisitChildren(s);

			if (s.CatchParameter != null)
				s.CatchParameter.Accept(this);
		}

		public virtual void Visit(Statements.TryStatement.FinallyStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.ThrowStatement s)
		{
			VisitAbstractStmt(s);

			if (s.ThrowExpression != null)
				s.ThrowExpression.Accept(this);
		}

		public virtual void Visit(Statements.ScopeGuardStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.AsmStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(Statements.PragmaStatement s)
		{
			VisitChildren(s);

			s.Pragma.Accept(this);
		}

		public virtual void Visit(Statements.AssertStatement s)
		{
			VisitAbstractStmt(s);

			if (s.AssertedExpression != null)
				s.AssertedExpression.Accept(this);
		}

		public virtual void Visit(Statements.ConditionStatement.DebugStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.ConditionStatement.VersionStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.VolatileStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.ExpressionStatement s)
		{
			VisitAbstractStmt(s);

			s.Expression.Accept(this);
		}

		public virtual void Visit(Statements.DeclarationStatement s)
		{
			VisitAbstractStmt(s);

			if (s.Declarations != null)
				foreach (var decl in s.Declarations)
					decl.Accept(this);
		}

		public virtual void Visit(Statements.TemplateMixin s)
		{
			VisitAbstractStmt(s);

			if (s.Qualifier != null)
				s.Qualifier.Accept(this);
		}

		public virtual void Visit(Statements.VersionDebugSpecification s)
		{
			VisitAbstractStmt(s);

			if (s.SpecifiedValue != null)
				s.SpecifiedValue.Accept(this);
		}

		public virtual void VisitAbstractStmt(AbstractStatement stmt)
		{
			if (stmt.Attributes != null && stmt.Attributes.Length != 0)
				foreach (var attr in stmt.Attributes)
					attr.Accept(this);
		}
		#endregion

		#region Expressions
		public virtual void VisitChildren(ContainerExpression x)
		{
			foreach (var sx in x.SubExpressions)
				sx.Accept(this);
		}

		public virtual void VisitOpBasedExpression(OperatorBasedExpression ox)
		{
			VisitChildren(ox);
		}

		public virtual void Visit(Expression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.AssignExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.ConditionalExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.OrOrExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AndAndExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.XorExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.OrExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AndExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.EqualExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.IdendityExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.RelExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.InExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.ShiftExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AddExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.MulExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.CatExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.PowExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_And x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Increment x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Decrement x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Mul x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Add x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Sub x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Not x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Cat x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Type x)
		{
			if (x.Type != null)
				x.Type.Accept(this);
		}

		public virtual void Visit(Expressions.NewExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.AnonymousClassExpression x)
		{
			VisitChildren(x);

			if (x.AnonymousClass != null)
				x.AnonymousClass.Accept(this);
		}

		public virtual void Visit(Expressions.DeleteExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.CastExpression x)
		{
			if (x.UnaryExpression != null)
				x.UnaryExpression.Accept(this);

			if (x.Type != null)
				x.Type.Accept(this);
		}

		public virtual void VisitPostfixExpression(PostfixExpression x)
		{
			if (x.PostfixForeExpression != null)
				x.PostfixForeExpression.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Access x)
		{
			VisitPostfixExpression(x);

			if (x.AccessExpression != null)
				x.AccessExpression.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Increment x)
		{
			VisitPostfixExpression(x);
		}

		public virtual void Visit(Expressions.PostfixExpression_Decrement x)
		{
			VisitPostfixExpression(x);
		}

		public virtual void Visit(Expressions.PostfixExpression_MethodCall x)
		{
			VisitPostfixExpression(x);

			if (x.ArgumentCount != 0)
				foreach (var arg in x.Arguments)
					arg.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Index x)
		{
			VisitPostfixExpression(x);

			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
					arg.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Slice x)
		{
			VisitPostfixExpression(x);

			if (x.FromExpression != null)
				x.FromExpression.Accept(this);
			if (x.ToExpression != null)
				x.ToExpression.Accept(this);
		}

		public virtual void Visit(TemplateInstanceExpression x)
		{
			if (x.TemplateIdentifier != null)
				x.TemplateIdentifier.Accept(this);

			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
					arg.Accept(this);
		}

		public virtual void Visit(Expressions.IdentifierExpression x)
		{
			
		}

		public virtual void Visit(Expressions.TokenExpression x)
		{
			
		}

		public virtual void Visit(Expressions.TypeDeclarationExpression x)
		{
			x.Declaration.Accept(this);
		}

		public virtual void Visit(Expressions.ArrayLiteralExpression x)
		{
			foreach (var e in x.Elements)
				if(e!=null)
					e.Accept(this);
		}

		public virtual void Visit(Expressions.AssocArrayExpression x)
		{
			foreach (var kv in x.Elements)
			{
				kv.Key.Accept(this);
				kv.Value.Accept(this);
			}
		}

		public virtual void Visit(Expressions.FunctionLiteral x)
		{
			x.AnonymousMethod.Accept(this);
		}

		public virtual void Visit(Expressions.AssertExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.MixinExpression x)
		{
			if (x.AssignExpression != null)
				x.AssignExpression.Accept(this);
		}

		public virtual void Visit(Expressions.ImportExpression x)
		{
			if (x.AssignExpression != null)
				x.AssignExpression.Accept(this);
		}

		public virtual void Visit(Expressions.TypeidExpression x)
		{
			if (x.Type != null)
				x.Type.Accept(this);
			else if (x.Expression != null)
				x.Expression.Accept(this);
		}

		public virtual void Visit(Expressions.IsExpression x)
		{
			if (x.TestedType != null)
				x.TestedType.Accept(this);

			// Do not visit the artificial param..it's not existing

			if (x.TypeSpecialization != null)
				x.TypeSpecialization.Accept(this);

			if (x.TemplateParameterList != null)
				foreach (var p in x.TemplateParameterList)
					p.Accept(this);
		}

		public virtual void Visit(Expressions.TraitsExpression x)
		{
			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
					arg.Accept(this);
		}

		public virtual void Visit(TraitsArgument arg)
		{
			if (arg.Type != null)
				arg.Type.Accept(this);
			if (arg.AssignExpression != null)
				arg.AssignExpression.Accept(this);
		}

		public virtual void Visit(Expressions.SurroundingParenthesesExpression x)
		{
			if (x.Expression != null)
				x.Expression.Accept(this);
		}

		public virtual void Visit(Expressions.VoidInitializer x)
		{
			
		}

		public virtual void Visit(Expressions.ArrayInitializer x)
		{
			Visit((AssocArrayExpression)x);
		}

		public virtual void Visit(Expressions.StructInitializer x)
		{
			if (x.MemberInitializers != null)
				foreach (var i in x.MemberInitializers)
					i.Accept(this);
		}

		public virtual void Visit(StructMemberInitializer init)
		{
			if (init.Value != null)
				init.Value.Accept(this);
		}
		#endregion

		#region Decls
		public virtual void VisitInner(ITypeDeclaration td)
		{
			if (td.InnerDeclaration != null)
				td.InnerDeclaration.Accept(this);
		}

		public virtual void Visit(IdentifierDeclaration td)
		{
			VisitInner(td);
		}

		public virtual void Visit(DTokenDeclaration td)
		{
			VisitInner(td);
		}

		public virtual void Visit(ArrayDecl td)
		{
			VisitInner(td);

			if (td.KeyType != null)
				td.KeyType.Accept(this);

			if (td.KeyExpression != null)
				td.KeyExpression.Accept(this);

			// ValueType == InnerDeclaration
		}

		public virtual void Visit(DelegateDeclaration td)
		{
			VisitInner(td);
			// ReturnType == InnerDeclaration

			if (td.Modifiers != null && td.Modifiers.Length != 0)
				foreach (var attr in td.Modifiers)
					attr.Accept(this);

			foreach (var p in td.Parameters)
				p.Accept(this);
		}

		public virtual void Visit(PointerDecl td)
		{
			VisitInner(td);
		}

		public virtual void Visit(MemberFunctionAttributeDecl td)
		{
			VisitInner(td);

			if (td.InnerType != null)
				td.InnerType.Accept(this);
		}

		public virtual void Visit(TypeOfDeclaration td)
		{
			VisitInner(td);

			if (td.InstanceId != null)
				td.InstanceId.Accept(this);
		}

		public virtual void Visit(VectorDeclaration td)
		{
			VisitInner(td);

			if (td.Id != null)
				td.Id.Accept(this);
		}

		public virtual void Visit(VarArgDecl td)
		{
			VisitInner(td);
		}

		public virtual void Visit(ITemplateParameterDeclaration td)
		{
			td.TemplateParameter.Accept(this);
		}
		#endregion

		#region Meta decl blocks
		public virtual void VisitMetaBlock(IMetaDeclarationBlock block)
		{

		}

		public virtual void Visit(MetaDeclarationBlock metaDeclarationBlock)
		{
			VisitMetaBlock(metaDeclarationBlock);
		}

		public virtual void Visit(AttributeMetaDeclarationBlock attributeMetaDeclarationBlock)
		{
			Visit((AttributeMetaDeclaration)attributeMetaDeclarationBlock);
			VisitMetaBlock(attributeMetaDeclarationBlock);
		}

		public virtual void Visit(AttributeMetaDeclarationSection attributeMetaDeclarationSection)
		{
			Visit((AttributeMetaDeclaration)attributeMetaDeclarationSection);
		}

		public virtual void Visit(ElseMetaDeclarationBlock elseMetaDeclarationBlock)
		{
			VisitMetaBlock(elseMetaDeclarationBlock);
		}

		public virtual void Visit(ElseMetaDeclaration elseMetaDeclaration)
		{
			
		}

		public virtual void Visit(AttributeMetaDeclaration md)
		{
			if (md.AttributeOrCondition != null)
				foreach (var attr in md.AttributeOrCondition)
					attr.Accept(this);

			if (md.OptionalElseBlock != null)
				md.OptionalElseBlock.Accept(this);
		}
		#endregion

		#region Template parameters
		public virtual void Visit(TemplateTypeParameter p)
		{
			if (p.Specialization != null)
				p.Specialization.Accept(this);

			if (p.Default != null)
				p.Default.Accept(this);
		}

		public virtual void Visit(TemplateThisParameter p)
		{
			if (p.FollowParameter != null)
				p.FollowParameter.Accept(this);
		}

		public virtual void Visit(TemplateValueParameter p)
		{
			if (p.Type != null)
				p.Type.Accept(this);

			if (p.SpecializationExpression != null)
				p.SpecializationExpression.Accept(this);
			if (p.DefaultExpression != null)
				p.DefaultExpression.Accept(this);
		}

		public virtual void Visit(TemplateAliasParameter p)
		{
			Visit((TemplateValueParameter)p);

			if (p.SpecializationType != null)
				p.SpecializationType.Accept(this);
			if (p.DefaultType != null)
				p.DefaultType.Accept(this);
		}

		public virtual void Visit(TemplateTupleParameter p)
		{
			
		}
		#endregion
	}
}
