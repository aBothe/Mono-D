using System;

//using Mono.TextEditor;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;

using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.IO;
using D_Parser.Dom.Expressions;

namespace MonoDevelop.D.Completion
{
	public class DParameterDataProvider : IParameterDataProvider
	{
		Document doc;
		ArgumentsResolutionResult args;

		int selIndex = 0;
		public ResolveResult CurrentResult { get { return args.ResolvedTypesOrMethods[selIndex]; } }
		DMethod scopeMethod = null;
		
		public static DParameterDataProvider Create(Document doc, IAbstractSyntaxTree SyntaxTree, CodeCompletionContext ctx)
		{		
			var caretOffset = ctx.TriggerOffset;
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset, ctx.TriggerLine);

			IStatement stmt = null;
			var curBlock = DResolver.SearchBlockAt(SyntaxTree, caretLocation, out stmt);

			if (!(curBlock is D_Parser.Dom.DMethod))
				return null;

			try
			{
				var parseCache=DCodeCompletionSupport.EnumAvailableModules(doc);
				var importCache=DResolver.ResolveImports(SyntaxTree as DModule,parseCache);
				var argsResult = ParameterContextResolution.ResolveArgumentContext(
					doc.Editor.Text, 
					caretOffset, 
					caretLocation, 
					curBlock as D_Parser.Dom.DMethod, 
					parseCache,
					importCache);
				
				if (argsResult == null || argsResult.ResolvedTypesOrMethods == null || argsResult.ResolvedTypesOrMethods.Length < 1)
					return null;

				return new DParameterDataProvider(doc, argsResult) { scopeMethod=curBlock as DMethod };
			}
			catch { return null; }
		}
		
		private DParameterDataProvider(Document doc, ArgumentsResolutionResult argsResult)
		{
			this.doc = doc;
			args = argsResult;
			selIndex = args.CurrentlyCalledMethod;
		}
		
		public static string GetNodeParamString(D_Parser.Dom.INode node)
		{	
			string result = "";
			string sep = "";
			if (node is DMethod)
			{
				
				foreach(D_Parser.Dom.INode param in (node as DMethod).Parameters)
				{
					if (param.Type != null)
						result =  result + sep + param.Type.ToString();	
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
			//string result1 = (overload+1).ToString()+"/"+args.ResolvedTypesOrMethods.Length.ToString();
			string s = "";

			if (CurrentResult is MemberResult)
			{
				MemberResult mr = (CurrentResult as MemberResult);
				var dv = mr.ResolvedMember as DMethod;

				if (dv == null)
					return (mr.ResolvedMember as DNode).ToString(false);

				switch(dv.SpecialType)
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

				if (dv.Attributes.Count>0)
					s = dv.AttributeString + ' ';

				s += dv.Name;

				// Template parameters
				if (dv.TemplateParameters != null && dv.TemplateParameters.Length > 0)
				{
					s += "(";

					if(args.IsTemplateInstanceArguments)
						s += string.Join(",", parameterMarkup);
					else foreach(var p in dv.TemplateParameters)
						s += p.ToString() + ",";

					s = s.Trim(',') + ")";
				}

				// Parameters
				s += "(";

				if(!args.IsTemplateInstanceArguments)
					s += string.Join(",", parameterMarkup);
				else foreach (var p in dv.Parameters)
						s += p.ToString() + ",";

				s = s.Trim(',') + ")";


				// Optional: description
				if(!string.IsNullOrWhiteSpace( mr.ResolvedMember.Description))
					s += "\n\n " + mr.ResolvedMember.Description;
				return s;
			}
			
			if (CurrentResult is TypeResult && args.IsTemplateInstanceArguments)
			{				
				var tr = (CurrentResult as TypeResult);
				
				s = tr.ResolvedTypeDefinition.Name;
				
				s += "(" + string.Join(",", parameterMarkup) + ")";
				s += "\r\n " + tr.ResolvedTypeDefinition.Description;

				return s;
			}

			return "";
		}

		public string GetParameterMarkup (int overload, int paramIndex)			
		{
			selIndex = overload;

			if (CurrentResult is MemberResult)
			{
				var dm = (CurrentResult as MemberResult).ResolvedMember as DMethod;

				if (dm != null)
				{
					if (args.IsTemplateInstanceArguments && dm.TemplateParameters != null)
						return dm.TemplateParameters[paramIndex].ToString();
					else
						return (dm.Parameters[paramIndex] as DNode).ToString(false);
				}
			}

			if (args.IsTemplateInstanceArguments && 
				CurrentResult is TypeResult && 
				(CurrentResult as TypeResult).ResolvedTypeDefinition is DClassLike)
			{
				var dc=(CurrentResult as TypeResult).ResolvedTypeDefinition as DClassLike;

				if(dc.TemplateParameters!=null && dc.TemplateParameters.Length>paramIndex)
					return dc.TemplateParameters[paramIndex].ToString();
			}
				
			return null;
		}

		public int GetParameterCount (int overload)
		{			
			selIndex = overload;

			if (CurrentResult is MemberResult)
				if (((CurrentResult as MemberResult).ResolvedMember is DMethod))
				{
					var dm=(CurrentResult as MemberResult).ResolvedMember as DMethod;
					if (args.IsTemplateInstanceArguments)
						return dm.TemplateParameters!=null? dm.TemplateParameters.Length:0;
					return dm.Parameters.Count;
				}
			if (CurrentResult is TypeResult && (CurrentResult as TypeResult).ResolvedTypeDefinition is DClassLike)
			{
				var dc=((CurrentResult as TypeResult).ResolvedTypeDefinition as DClassLike);

				if (dc.TemplateParameters != null)
					return dc.TemplateParameters.Length;
			}

			return 0;
		}

		public int OverloadCount 
		{
				get { return args.ResolvedTypesOrMethods.Length; }
		}
		#endregion
	}
}

