using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using D_Parser.Dom.Expressions;

namespace MonoDevelop.D.Completion
{
	public class DParameterDataProvider : IParameterDataProvider
	{
		Document doc;
		ArgumentsResolutionResult args;
		int selIndex = 0;

		public ResolveResult CurrentResult { get {
			return args.ResolvedTypesOrMethods[selIndex];
		} }

		
		public static DParameterDataProvider Create (Document doc, IAbstractSyntaxTree SyntaxTree, CodeCompletionContext ctx)
		{
			var caretLocation = new CodeLocation (ctx.TriggerLineOffset, ctx.TriggerLine);

			try {
				var edData = DResolverWrapper.GetEditorData(doc);

				edData.CaretLocation=caretLocation;
				edData.CaretOffset=ctx.TriggerOffset;

				var argsResult = ParameterInsightResolution.ResolveArgumentContext (edData);
				
				if (argsResult == null || argsResult.ResolvedTypesOrMethods == null || argsResult.ResolvedTypesOrMethods.Length < 1)
					return null;

				return new DParameterDataProvider (doc, argsResult);
			} catch {
				return null;
			}
		}
		
		private DParameterDataProvider (Document doc, ArgumentsResolutionResult argsResult)
		{
			this.doc = doc;
			args = argsResult;
			selIndex = args.CurrentlyCalledMethod;
		}
		
		public static string GetNodeParamString (D_Parser.Dom.INode node)
		{	
			string result = "";
			string sep = "";
			if (node is DMethod) {
				
				foreach (D_Parser.Dom.INode param in (node as DMethod).Parameters) {
					if (param.Type != null)
						result = result + sep + param.Type.ToString ();	
					sep = ", ";
				}
				if (result.Length != 0)
					result = "(" + result + ")"; 
			}
			return result;
		}			
	
		#region IParameterDataProvider implementation
		
		public int GetCurrentParameterIndex (ICompletionWidget widget, CodeCompletionContext ctx)
		{
			/*
			int cursor = widget.CurrentCodeCompletionContext.TriggerOffset;
			var loc=new CodeLocation(ctx.TriggerLineOffset,ctx.TriggerLine);

			if (args.IsTemplateInstanceArguments)
			{

			}
			else
			{
				var firstArgLocation = CodeLocation.Empty;

				if (args.ParsedExpression is PostfixExpression_MethodCall)
					firstArgLocation = (args.ParsedExpression as PostfixExpression_MethodCall).Arguments[0].Location;
				else if (args.ParsedExpression is NewExpression)
					firstArgLocation = (args.ParsedExpression as NewExpression).Arguments[0].Location;
				else
					return -1;

				if (loc < firstArgLocation)
					loc = firstArgLocation;

				var code = doc.Editor.Document.GetTextBetween(firstArgLocation.Line,firstArgLocation.Column, scopeMethod.EndLocation.Line, scopeMethod.EndLocation.Column);

				var tr = new StringReader(code);
				var parser = new DParser(new Lexer(tr));
				parser.Lexer.SetInitialLocation(firstArgLocation);
				parser.Step();

				var updatedArguments = parser.ArgumentList();
				tr.Close();

				var lastArg = updatedArguments[updatedArguments.Count - 1];

				for (int i = 0; i < updatedArguments.Count; i++)
					if ((loc >= updatedArguments[i].Location && loc <= updatedArguments[i].EndLocation) ||
						(i==updatedArguments.Count-1 && loc <= updatedArguments[i].EndLocation))
						return i + 1;
			}
			*/
			return 0;
		}

		public string GetMethodMarkup (int overload, string[] parameterMarkup, int currentParameter)
		{
			selIndex = overload;

			if (CurrentResult is TemplateInstanceResult)
			{
				var s = "";
				var tir = (TemplateInstanceResult)CurrentResult;

				var dm = tir.Node as DMethod;
				if (dm != null)
				{
					s = GetMethodMarkup(dm, parameterMarkup, currentParameter);
				}
				else if (tir.Node is DClassLike)
				{
					s = tir.Node.Name + "(" + string.Join(",", parameterMarkup) + ")";
				}

				// Optional: description
				if (!string.IsNullOrWhiteSpace(tir.Node.Description))
					s += "\n\n " + tir.Node.Description;

				return s;
			}
			else if (CurrentResult is DelegateResult)
			{
				var dr = (DelegateResult)CurrentResult;

				if (dr.IsDelegateDeclaration)
				{
					var dg = (DelegateDeclaration)dr.DeclarationOrExpressionBase;

					return dg.ReturnType.ToString() + " " + (dg.IsFunction?"function":"delegate") + "(" + string.Join(",", parameterMarkup) + ")";
				}
				else
					return GetMethodMarkup(((FunctionLiteral)dr.DeclarationOrExpressionBase).AnonymousMethod, parameterMarkup, currentParameter);
			}

			return "";
		}

		string GetMethodMarkup(DMethod dm, string[] parameterMarkup, int currentParameter)
		{
			var s = "";

			switch (dm.SpecialType)
			{
				case DMethod.MethodType.Constructor:
					s = "(Constructor) ";
					break;
				case DMethod.MethodType.Destructor:
					s = "(Destructor) ";
					break;
				case DMethod.MethodType.Allocator:
					s = "(Allocator) ";
					break;
			}

			if (dm.Attributes.Count > 0)
				s = dm.AttributeString + ' ';

			s += dm.Name;

			// Template parameters
			if (dm.TemplateParameters != null && dm.TemplateParameters.Length > 0)
			{
				s += "(";

				if (args.IsTemplateInstanceArguments)
					s += string.Join(",", parameterMarkup);
				else
					foreach (var p in dm.TemplateParameters)
						s += p.ToString() + ",";

				s = s.Trim(',') + ")";
			}

			// Parameters
			s += "(";

			if (!args.IsTemplateInstanceArguments)
				s += string.Join(",", parameterMarkup);
			else
				foreach (var p in dm.Parameters)
					s += p.ToString() + ",";

			return s.Trim(',') + ")";
		}

		public string GetParameterMarkup (int overload, int paramIndex)
		{
			selIndex = overload;

			if (CurrentResult is TemplateInstanceResult)
			{
				var tir = (TemplateInstanceResult)CurrentResult;

				if (tir.Node is DClassLike)
					return ((DClassLike)tir.Node).TemplateParameters[paramIndex].ToString();

				var dm = tir.Node as DMethod;

				if (dm != null)
				{
					if (args.IsTemplateInstanceArguments)
						return dm.TemplateParameters[paramIndex].ToString();
					return ((DNode)dm.Parameters[paramIndex]).ToString(false);
				}
			}
			else if (CurrentResult is DelegateResult)
			{
				var dr = (DelegateResult)CurrentResult;

				if (dr.IsDelegateDeclaration)
					return ((DNode)((DelegateDeclaration)dr.DeclarationOrExpressionBase).Parameters[paramIndex]).ToString(false);
				else
					return ((DNode)((FunctionLiteral)dr.DeclarationOrExpressionBase).AnonymousMethod.Parameters[paramIndex]).ToString(false);
			}
				
			return null;
		}

		public int GetParameterCount (int overload)
		{			
			selIndex = overload;

			if (CurrentResult is TemplateInstanceResult)
			{
				var tir = (TemplateInstanceResult)CurrentResult;

				if (tir.Node is DClassLike)
				{
					var dc=(DClassLike)tir.Node;

					if(dc.TemplateParameters!=null)
						return dc.TemplateParameters.Length;
					return 0;
				}
				
				var dm = tir.Node as DMethod;

				if (dm != null)
				{
					if (args.IsTemplateInstanceArguments)
						return dm.TemplateParameters != null ? dm.TemplateParameters.Length : 0;
					return dm.Parameters.Count;
				}
			}
			else if (CurrentResult is DelegateResult)
			{
				var dr = (DelegateResult)CurrentResult;

				if (dr.IsDelegateDeclaration)
					return ((DelegateDeclaration)dr.DeclarationOrExpressionBase).Parameters.Count;
				else
					return ((FunctionLiteral)dr.DeclarationOrExpressionBase).AnonymousMethod.Parameters.Count;
			}

			return 0;
		}

		public int OverloadCount {
			get { return args.ResolvedTypesOrMethods.Length; }
		}
		#endregion
	}
}

