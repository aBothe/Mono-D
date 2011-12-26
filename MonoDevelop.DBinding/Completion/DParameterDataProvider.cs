using System;

//using Mono.TextEditor;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;

using D_Parser.Resolver;
using D_Parser.Dom;
using D_Parser.Dom.Statements;

namespace MonoDevelop.D.Completion
{
	public class DParameterDataProvider : IParameterDataProvider
	{
		Document doc;
		DResolver.ArgumentsResolutionResult args;
		int selIndex = 0;
		public ResolveResult CurrentResult { get { return args.ResolvedTypesOrMethods[selIndex]; } }		
		
		
		public static DParameterDataProvider Create(Document doc, IAbstractSyntaxTree SyntaxTree, CodeCompletionContext ctx)
		{		
			var caretOffset = ctx.TriggerOffset;
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset, ctx.TriggerLine);

			IStatement stmt = null;
			var curBlock = DResolver.SearchBlockAt(SyntaxTree, caretLocation, out stmt);

			if (curBlock == null)
				return null;
			
			
			//doc.Edito
			if (!(curBlock is D_Parser.Dom.DMethod))
				return null;

			try
	
			{
				var argsResult = DResolver.ResolveArgumentContext(
					doc.Editor.Text, 
					caretOffset, 
					caretLocation, 
					curBlock as D_Parser.Dom.DMethod, 
					DCodeCompletionSupport.EnumAvailableModules(doc),null);
				
				if (argsResult == null || argsResult.ResolvedTypesOrMethods == null || argsResult.ResolvedTypesOrMethods.Length < 1)
					return null;

				return new DParameterDataProvider(doc, argsResult);
			}
			catch { return null; }
		}
		
		private DParameterDataProvider(Document doc, DResolver.ArgumentsResolutionResult argsResult)
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
			return args.CurrentlyTypedArgumentIndex+1;

			int cursor = widget.CurrentCodeCompletionContext.TriggerOffset;
			int i =  ctx.TriggerOffset;
			
			if (i > cursor)
				return -1;
			if (i == cursor) 
				return 1; // parameters are 1 based
			//IEnumerable<string> types = MonoDevelop.Ide.DesktopService.GetMimeTypeInheritanceChain (CSharpFormatter.MimeType);
			//CSharpIndentEngine engine = new CSharpIndentEngine (MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<CSharpFormattingPolicy> (types));
			int index = 0 + 1;
			int parentheses = 0;
			int bracket = 0;
			do {
				char c = widget.GetChar (i - 1);			
				switch (c) {
				case '{':
					if (!DResolver.CommentSearching.IsInCommentAreaOrString(doc.Editor.Text, i - 1))
						bracket++;
					break;
				case '}':
					if (!DResolver.CommentSearching.IsInCommentAreaOrString(doc.Editor.Text, i - 1))
						bracket--;
					break;
				case '(':
					if (!DResolver.CommentSearching.IsInCommentAreaOrString(doc.Editor.Text, i - 1))
						parentheses++;
					break;
				case ')':
					if (!DResolver.CommentSearching.IsInCommentAreaOrString(doc.Editor.Text, i - 1))
						parentheses--;
					break;
				case ',':
					if (!DResolver.CommentSearching.IsInCommentAreaOrString(doc.Editor.Text, i - 1) && parentheses == 1 && bracket == 0)
						index++;
					break;
				}
				i++;
			} while (i <= cursor && parentheses >= 0);
			
			return parentheses != 1 || bracket > 0 ? -1 : index;
					
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

