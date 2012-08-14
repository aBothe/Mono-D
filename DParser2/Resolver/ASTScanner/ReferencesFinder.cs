using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.ASTScanner
{
	public class ReferencesFinder
	{
		#region Properties
		readonly List<ISyntaxRegion> l = new List<ISyntaxRegion>();
		readonly INode symbol;
		readonly string searchId;
		/// <summary>
		/// Used when searching references of a variable.
		/// </summary>
		readonly bool handleSingleIdentifiersOnly;
		#endregion

		#region Ctor/IO
		public static IEnumerable<ISyntaxRegion> Scan(INode symbol)
		{
			return Scan(symbol.NodeRoot as IAbstractSyntaxTree, symbol);
		}

		public static IEnumerable<ISyntaxRegion> Scan(IAbstractSyntaxTree ast, INode symbol)
		{
			var f = new ReferencesFinder(symbol);



			return f.l;
		}

		private ReferencesFinder(INode symbol)
		{
			this.symbol = symbol;
			searchId = symbol.Name;
			handleSingleIdentifiersOnly = symbol is DVariable /* && ((DVariable)symbol).IsAlias */;
		}
		#endregion

		void S(INode n)
		{
			if (n.Type != null)
				S(n.Type);

			if (n is DModule)
			{
				var dm = (DModule)n;

				if (dm.OptionalModuleStatement != null)
					S(dm.OptionalModuleStatement);
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

				S(dm.TemplateConstraint);

				S(dm.In);
				S(dm.Out);
				S(dm.Body);
			}

			if (n is DBlockNode)
			{
				var dbn = (DBlockNode)n;

				foreach (var s in dbn.StaticStatements)
					S(s);

			}
			if (n is DNode)
			{
				var dn = (DNode)n;

				//TODO: Template params still missing
				if (dn.TemplateParameters != null)
					foreach (var tp in dn.TemplateParameters)
					{
						if (tp is TemplateValueParameter)
						{
							var tvp = (TemplateValueParameter)tp;

							S(tvp.Type);
							if(tvp.DefaultExpression!=null)
								S(tvp.DefaultExpression);
							if(tvp.SpecializationExpression!=null)
								S(tvp.SpecializationExpression);
						}
					}
			}
			
		}

		void S(IStatement s)
		{

		}

		void S(IExpression x)
		{

		}

		void S(ITypeDeclaration td)
		{

		}
	}
}
