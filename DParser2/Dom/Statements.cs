using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom.Statements
{
	#region Generics
	public interface IStatement
	{
		CodeLocation StartLocation { get; set; }
		CodeLocation EndLocation { get; set; }
		IStatement Parent { get; set; }
		INode ParentNode { get; set; }

		/// <summary>
		/// Mostly used for storing declaration constraints.
		/// </summary>
		DAttribute[] Attributes { get; set; }
		string AttributeString { get; }

		string ToCode();
	}

	public interface IExpressionContainingStatement : IStatement
	{
		IExpression[] SubExpressions { get; }
	}

	public interface IDeclarationContainingStatement : IStatement
	{
		INode[] Declarations { get; }
	}

	public abstract class AbstractStatement:IStatement
	{
		public virtual CodeLocation StartLocation { get; set; }
		public virtual CodeLocation EndLocation { get; set; }
		public IStatement Parent { get; set; }
		public DAttribute[] Attributes { get; set; }

		INode parent;
		public INode ParentNode {
			get
			{
				if(Parent!=null)
					return Parent.ParentNode;
				return parent;
			}
			set
			{
				if (Parent != null)
					Parent.ParentNode = value;
				else
					parent = value;
			}
		}

		public string AttributeString
		{
			get
			{
				string s = "";
				foreach (var attr in Attributes)
					if (attr != null)
						s += attr.ToString() + " ";
				return s;
			}
		}

		public abstract string ToCode();

		public override string ToString()
		{
			return ToCode();
		}
	}

	/// <summary>
	/// Represents a statement that can contain other statements, which may become scoped.
	/// </summary>
	public abstract class StatementContainingStatement : AbstractStatement
	{
		public virtual IStatement ScopedStatement { get; set; }

		public virtual IStatement[] SubStatements { get { return new[] { ScopedStatement }; } }
	}
	#endregion

	public class BlockStatement : StatementContainingStatement, IEnumerable<IStatement>, IDeclarationContainingStatement
	{
		readonly List<IStatement> _Statements = new List<IStatement>();

		public IEnumerator<IStatement>  GetEnumerator()
		{
 			return _Statements.GetEnumerator();
		}

		System.Collections.IEnumerator  System.Collections.IEnumerable.GetEnumerator()
		{
 			return _Statements.GetEnumerator();
		}

		public override string ToCode()
		{
			var ret = "{"+Environment.NewLine;

			foreach (var s in _Statements)
				ret += s.ToCode()+Environment.NewLine;

			return ret + "}";
		}

		public void Add(IStatement s)
		{
			if (s == null)
				return;
			s.Parent = this;
			_Statements.Add(s);
		}

		public override IStatement[] SubStatements
		{
			get
			{
				return _Statements.ToArray(); ;
			}
		}

		public INode[] Declarations
		{
			get
			{
				var l = new List<INode>();

				foreach (var s in _Statements)
					if (s is BlockStatement || s is DeclarationStatement)
					{
						var decls = (s as IDeclarationContainingStatement).Declarations;
						if(decls!=null && decls.Length>0)
							l.AddRange(decls);
					}

				return l.ToArray();
			}
		}

		public virtual IStatement SearchStatement(CodeLocation Where)
		{
			return SearchBlockStatement(this, Where);
		}

		/// <summary>
		/// Scans the current scope. If a scoping statement was found, also these ones get searched then recursively.
		/// </summary>
		/// <param name="Where"></param>
		/// <returns></returns>
		public IStatement SearchStatementDeeply(CodeLocation Where)
		{
			var s = SearchStatement(Where);

			while (s!=null)
			{
				if (s is BlockStatement)
				{
					var s2 = (s as BlockStatement).SearchStatement(Where);

					if (s == s2)
						break;

					if (s2 != null)
						s = s2;
				}
				else if (s is StatementContainingStatement)
				{
					bool foundMatch = false;
					foreach(var s2 in (s as StatementContainingStatement).SubStatements)
						if (s2 != null && Where >= s2.StartLocation && Where <= s2.EndLocation)
						{
							s = s2;
							foundMatch = true;
							break;
						}

					if (!foundMatch)
						break;
				}
				else 
					break;
			}

			return s;
		}

		public static IStatement SearchBlockStatement(BlockStatement BlockStmt, CodeLocation Where)
		{
			// First check if one sub-statement is located at the code location
			foreach (var s in BlockStmt._Statements)
				if (Where >= s.StartLocation && Where <= s.EndLocation)
					return s;

			// If nothing was found, check if this statement fits to the coordinates
			if (Where >= BlockStmt.StartLocation && Where <= BlockStmt.EndLocation)
				return BlockStmt;

			// If not, return null
			return null;
		}
	}

	public class LabeledStatement : AbstractStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return Identifier + ":";
		}
	}

	public class IfStatement : StatementContainingStatement,IDeclarationContainingStatement,IExpressionContainingStatement
	{
		public bool IsStatic = false;
		public IExpression IfCondition;
		public DVariable[] IfVariable;

		public IStatement ThenStatement
		{
			get { return ScopedStatement; }
			set { ScopedStatement = value; }
		}
		public IStatement ElseStatement;

		public override IStatement[] SubStatements
		{
			get
			{
				if (ThenStatement != null && ElseStatement != null)
					return new[] { ThenStatement, ElseStatement };
				return new[] { ThenStatement };
			}
		}

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ElseStatement!=null?ElseStatement.EndLocation:ScopedStatement. EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var ret = (IsStatic?"static ":"")+ "if(";

			if (IfCondition != null)
				ret += IfCondition.ToString();

			ret += ")"+Environment.NewLine;

			if (ScopedStatement != null)
				ret += ScopedStatement. ToCode();

			if (ElseStatement != null)
				ret += Environment.NewLine + "else " + ElseStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get {
				return new[] { IfCondition };
			}
		}

		public INode[] Declarations
		{
			get { 
				return IfVariable;
			}
		}
	}

	public class WhileStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression Condition;

		public override CodeLocation EndLocation
		{
			get
			{
				if (ScopedStatement == null)
					return base.EndLocation;
				return ScopedStatement.EndLocation;
			}
			set
			{
				if (ScopedStatement == null)
					base.EndLocation = value;
			}
		}

		public override string ToCode()
		{
			var ret= "while(";

			if (Condition != null)
				ret += Condition.ToString();

			ret += ") "+Environment.NewLine;

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Condition}; }
		}
	}

	public class ForStatement : StatementContainingStatement, IDeclarationContainingStatement, IExpressionContainingStatement
	{
		public IStatement Initialize;
		public IExpression Test;
		public IExpression Increment;

		public IExpression[] SubExpressions
		{
			get { return new[] { Test,Increment }; }
		}

		public override IStatement[] SubStatements
		{
			get
			{
				return new[]{Initialize, ScopedStatement};
			}
		}

		public override string ToCode()
		{
			var ret = "for(";

			if (Initialize != null)
				ret += Initialize.ToCode();

			ret+=';';

			if (Test != null)
				ret += Test.ToString();

			ret += ';';

			if (Increment != null)
				ret += Increment.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' '+ScopedStatement.ToCode();

			return ret;
		}

		public INode[] Declarations
		{
			get {
				if (Initialize is DeclarationStatement)
					return (Initialize as DeclarationStatement).Declarations;

				return null;
			}
		}
	}

	public class ForeachStatement : StatementContainingStatement, 
		IExpressionContainingStatement,
		IDeclarationContainingStatement
	{
		public bool IsRangeStatement
		{
			get { return UpperAggregate != null; }
		}
		public bool IsReverse = false;
		public DVariable[] ForeachTypeList;

		public INode[] Declarations
		{
			get { return ForeachTypeList; }
		}

		public IExpression Aggregate;

		/// <summary>
		/// Used in ForeachRangeStatements. The Aggregate field will be the lower expression then.
		/// </summary>
		public IExpression UpperAggregate;

		public IExpression[] SubExpressions
		{
			get { return new[]{ Aggregate, UpperAggregate }; }
		}

		public override string ToCode()
		{
			var ret=(IsReverse?"foreach_reverse":"foreach")+'(';

			foreach (var v in ForeachTypeList)
				ret += v.ToString() + ',';

			ret=ret.TrimEnd(',')+';';

			if (Aggregate != null)
				ret += Aggregate.ToString();

			if (UpperAggregate!=null)
				ret += ".." + UpperAggregate.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}
	}

	public class SwitchStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public bool IsFinal;
		public IExpression SwitchExpression;

		public IExpression[] SubExpressions
		{
			get { return new[] { SwitchExpression }; }
		}

		public override string ToCode()
		{
			var ret = "switch(";

			if (SwitchExpression != null)
				ret += SwitchExpression.ToString();

			ret+=')';

			if (ScopedStatement != null)
				ret += ' '+ScopedStatement.ToCode();

			return ret;
		}

		public class CaseStatement : StatementContainingStatement, IExpressionContainingStatement
		{
			public bool IsCaseRange
			{
				get { return LastExpression != null; }
			}

			public IExpression ArgumentList;

			/// <summary>
			/// Used for CaseRangeStatements
			/// </summary>
			public IExpression LastExpression;

			public IStatement[] ScopeStatementList;

			public override string ToCode()
			{
				var ret= "case "+ArgumentList.ToString()+':' + (IsCaseRange?(" .. case "+LastExpression.ToString()+':'):"")+Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode()+Environment.NewLine;

				return ret;
			}

			public IExpression[] SubExpressions
			{
				get { return new[]{ArgumentList,LastExpression}; }
			}

			public override IStatement[] SubStatements
			{
				get
				{
					return ScopeStatementList;
				}
			}
		}

		public class DefaultStatement : StatementContainingStatement
		{
			public IStatement[] ScopeStatementList;

			public override IStatement[] SubStatements
			{
				get
				{
					return ScopeStatementList;
				}
			}

			public override string ToCode()
			{
				var ret = "default:"+Environment.NewLine;

				foreach (var s in ScopeStatementList)
					ret += s.ToCode() + Environment.NewLine;

				return ret;
			}
		}
	}

	public class ContinueStatement : AbstractStatement, IExpressionContainingStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return "continue"+(string.IsNullOrEmpty(Identifier)?"":(' '+Identifier))+';';
		}

		public IExpression[] SubExpressions
		{
			get { return string.IsNullOrEmpty(Identifier)?null:new[]{new IdentifierExpression(Identifier)}; }
		}
	}

	public class BreakStatement : AbstractStatement,IExpressionContainingStatement
	{
		public string Identifier;

		public override string ToCode()
		{
			return "break" + (string.IsNullOrEmpty(Identifier) ? "" : (' ' + Identifier)) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return string.IsNullOrEmpty(Identifier) ? null : new[] { new IdentifierExpression(Identifier) }; }
		}
	}

	public class ReturnStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ReturnExpression;

		public override string ToCode()
		{
			return "return" + (ReturnExpression==null ? "" : (' ' + ReturnExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ReturnExpression}; }
		}
	}

	public class GotoStatement : AbstractStatement, IExpressionContainingStatement
	{
		public enum GotoStmtType
		{
			Identifier=DTokens.Identifier,
			Case=DTokens.Case,
			Default=DTokens.Default
		}

		public string LabelIdentifier;
		public IExpression CaseExpression;
		public GotoStmtType StmtType = GotoStmtType.Identifier;

		public override string ToCode()
		{
			switch (StmtType)
			{
				case GotoStmtType.Identifier:
					return "goto " + LabelIdentifier+';';
				case GotoStmtType.Default:
					return "goto default;";
				case GotoStmtType.Case:
					return "goto"+(CaseExpression==null?"":(' '+CaseExpression.ToString()))+';';
			}

			return null;
		}

		public IExpression[] SubExpressions
		{
			get { return CaseExpression != null ? new[] { CaseExpression } : null; }
		}
	}

	public class WithStatement : StatementContainingStatement, IExpressionContainingStatement
	{
		public IExpression WithExpression;
		public ITypeDeclaration WithSymbol;

		public override string ToCode()
		{
			var ret = "with(";

			if (WithExpression != null)
				ret += WithExpression.ToString();
			else if (WithSymbol != null)
				ret += WithSymbol.ToString();

			ret += ')';

			if (ScopedStatement != null)
				ret += ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get {
				if (WithExpression != null)
					return new[] { WithExpression};
				if (WithSymbol != null)
					return new[] { new TypeDeclarationExpression(WithSymbol) };
				return null;
			}
		}
	}

	public class SynchronizedStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public IExpression SyncExpression;

		public override string ToCode()
		{
			var ret="synchronized";

			if (SyncExpression != null)
				ret += '(' + SyncExpression.ToString() + ')';

			if (ScopedStatement != null)
				ret += ' ' + ScopedStatement.ToCode();

			return ret;
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{SyncExpression}; }
		}
	}

	public class TryStatement : StatementContainingStatement
	{
		public CatchStatement[] Catches;
		public FinallyStatement FinallyStmt;

		public override IStatement[] SubStatements
		{
			get
			{
				var l = new List<IStatement>();

				if (ScopedStatement != null)
					l.Add(ScopedStatement);

				if (Catches != null && Catches.Length > 0)
					l.AddRange(Catches);

				if (FinallyStmt != null)
					l.Add(FinallyStmt);

				if (l.Count > 0)
					return l.ToArray();
				return null;
			}
		}

		public override string ToCode()
		{
			var ret= "try " + (ScopedStatement!=null? (' '+ScopedStatement.ToCode()):"");

			if (Catches != null && Catches.Length > 0)
				foreach (var c in Catches)
					ret += Environment.NewLine + c.ToCode();

			if (FinallyStmt != null)
				ret += Environment.NewLine + FinallyStmt.ToCode();

			return ret;
		}

		public class CatchStatement : StatementContainingStatement,IDeclarationContainingStatement
		{
			public DVariable CatchParameter;

			public override string ToCode()
			{
				return "catch" + (CatchParameter != null ? ('(' + CatchParameter.ToString() + ')') : "")
					+ (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
			}

			public INode[] Declarations
			{
				get {
					if (CatchParameter == null)
						return null;
					return new[]{CatchParameter}; 
				}
			}
		}

		public class FinallyStatement : StatementContainingStatement
		{
			public override string ToCode()
			{
				return "finally" + (ScopedStatement != null ? (' ' + ScopedStatement.ToCode()) : "");
			}
		}
	}

	public class ThrowStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression ThrowExpression;

		public override string ToCode()
		{
			return "throw" + (ThrowExpression==null ? "" : (' ' + ThrowExpression.ToString())) + ';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ThrowExpression}; }
		}
	}

	public class ScopeGuardStatement : StatementContainingStatement
	{
		public const string ExitScope = "exit";
		public const string SuccessScope = "success";
		public const string FailureScope = "failure";

		public string GuardedScope=ExitScope;

		public override string ToCode()
		{
			return "scope("+GuardedScope+')'+ (ScopedStatement==null?"":ScopedStatement.ToCode());
		}
	}

	public class AsmStatement : AbstractStatement
	{
		public string[] Instructions;

		public override string ToCode()
		{
			var ret = "asm {";

			if (Instructions != null && Instructions.Length > 0)
			{
				foreach (var i in Instructions)
					ret += Environment.NewLine + i + ';';
				ret += Environment.NewLine;
			}

			return ret+'}';
		}
	}

	public class PragmaStatement : StatementContainingStatement,IExpressionContainingStatement
	{
		public PragmaAttribute Pragma;

		public IExpression[] SubExpressions
		{
			get { return Pragma==null ? null: Pragma.Arguments; }
		}

		public override string ToCode()
		{
			var r = Pragma==null? "" : Pragma.ToString();

			r += ScopedStatement==null? "" : (" " + ScopedStatement.ToCode());

			return r;
		}
	}

	public class MixinStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression MixinExpression;

		public override string ToCode()
		{
			return "mixin("+(MixinExpression==null?"":MixinExpression.ToString())+");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{MixinExpression}; }
		}
	}

	public abstract class ConditionStatement : StatementContainingStatement
	{
		public IStatement ElseStatement;

		public override IStatement[] SubStatements
		{
			get
			{
				if (ScopedStatement != null && ElseStatement != null)
					return new[] { ScopedStatement, ElseStatement };
				return new[] { ScopedStatement };
			}
		}

		public class DebugStatement : ConditionStatement
		{
			public object DebugIdentifierOrLiteral;
			public override string ToCode()
			{
				var ret = "debug";

				if(DebugIdentifierOrLiteral!=null)
					ret+='('+DebugIdentifierOrLiteral.ToString()+')';

				if (ScopedStatement != null)
					ret += ' ' + ScopedStatement.ToCode();

				if (ElseStatement != null)
					ret += " else " + ElseStatement.ToCode();

				return ret;
			}
		}

		public class VersionStatement : ConditionStatement
		{
			public object VersionIdentifierOrLiteral;
			public override string ToCode()
			{
				var ret = "version";

				if (VersionIdentifierOrLiteral != null)
					ret += '(' + VersionIdentifierOrLiteral.ToString() + ')';

				if (ScopedStatement != null)
					ret += ' ' + ScopedStatement.ToCode();

				if (ElseStatement != null)
					ret += " else " + ElseStatement.ToCode();

				return ret;
			}
		}
	}

	public class AssertStatement : AbstractStatement,IExpressionContainingStatement
	{
		public bool IsStatic = false;
		public IExpression AssertedExpression;

		public override string ToCode()
		{
			return (IsStatic?"static ":"")+"assert("+(AssertedExpression!=null?AssertedExpression.ToString():"")+");";
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ AssertedExpression }; }
		}
	}

	public class VolatileStatement : StatementContainingStatement
	{
		public override string ToCode()
		{
			return "volatile "+ScopedStatement==null?"":ScopedStatement.ToCode();
		}
	}

	public class ExpressionStatement : AbstractStatement,IExpressionContainingStatement
	{
		public IExpression Expression;

		public override string ToCode()
		{
			return Expression.ToString()+';';
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{Expression}; }
		}
	}

	public class DeclarationStatement : AbstractStatement,IDeclarationContainingStatement, IExpressionContainingStatement
	{
		/// <summary>
		/// Declarations done by this statement. Contains more than one item e.g. on int a,b,c;
		/// </summary>
		//public INode[] Declaration;

		public override string ToCode()
		{
			if (Declarations == null || Declarations.Length < 0)
				return ";";

			var r = Declarations[0].ToString();

			for (int i = 1; i < Declarations.Length; i++)
			{
				var d = Declarations[i];
				r += ',' + d.Name;

				var dv=d as DVariable;
				if (dv != null && dv.Initializer != null)
					r += '=' + dv.Initializer.ToString();
			}

			return r+';';
		}

		public INode[] Declarations
		{
			get;
			set;
		}

		public IExpression[] SubExpressions
		{
			get 
			{
				var l = new List<IExpression>();

				if(Declarations!=null)
					foreach (var decl in Declarations)
						if (decl is DVariable && (decl as DVariable).Initializer!=null)
							l.Add((decl as DVariable).Initializer);

				return l.ToArray();
			}
		}
	}

	public class TemplateMixin : AbstractStatement,IExpressionContainingStatement
	{
		public ITypeDeclaration Qualifier;
		public string MixinId;

		public override string ToCode()
		{
			var r = "mixin";

			if (Qualifier != null)
				r += " " + Qualifier.ToString();

			if(!string.IsNullOrEmpty(MixinId))
				r+=' '+MixinId;

			return r+';';
		}

		public IExpression[] SubExpressions
		{
			get {
				var l=new List<IExpression>();
				var c = Qualifier;

				while (c != null)
				{
					if (c is TemplateInstanceExpression)
						l.Add(c as IExpression);

					c = c.InnerDeclaration;
				}

				return l.ToArray();
			}
		}
	}

	public class VersionDebugSpecification : AbstractStatement, IExpressionContainingStatement
	{
		public int Token;

		public IExpression SpecifiedValue;
	
		public override string  ToCode()
		{
 			return DTokens.GetTokenString(Token)+ "="+(SpecifiedValue!=null?SpecifiedValue.ToString():"");
		}

		public IExpression[] SubExpressions
		{
			get { return new[]{ SpecifiedValue }; }
		}
	}
}
