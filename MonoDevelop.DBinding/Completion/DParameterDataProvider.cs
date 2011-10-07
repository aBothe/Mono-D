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
					DCodeCompletionSupport.EnumAvailableModules(doc));

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
		
		public ResolveResult CurrentResult { get { return args.ResolvedTypesOrMethods[selIndex]; } }		
		
		#region IParameterDataProvider implementation
		
		public int GetCurrentParameterIndex (ICompletionWidget widget, CodeCompletionContext ctx)
		{			
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
			string result = "";
			if (CurrentResult is MemberResult)
			{
				result += " " + (CurrentResult as MemberResult).ResolvedMember.Type.ToString();
				result += " " + (CurrentResult as MemberResult).ResolvedMember.Name;
				result += " (" + string.Join(",", parameterMarkup) + ")";
				result += "\r\n " + (CurrentResult as MemberResult).ResolvedMember.Description;
			}
			
			if (CurrentResult is TypeResult)
			{
				result += " " + (CurrentResult as TypeResult).ResolvedTypeDefinition.Type.ToString();
				result += " " + (CurrentResult as TypeResult).ResolvedTypeDefinition.Name;
				result += " (" + string.Join(",", parameterMarkup) + ")";
				result += "\r\n " + (CurrentResult as TypeResult).ResolvedTypeDefinition.Description;	
			}					
			
			return result;	
		}

		public string GetParameterMarkup (int overload, int paramIndex)			
		{
			selIndex = overload;
			string result = "";
			if (CurrentResult is MemberResult)
				if ((((CurrentResult as MemberResult).ResolvedMember is DMethod))
				    && (((CurrentResult as MemberResult).ResolvedMember as DMethod).Parameters.Count > paramIndex))
				{
					DMethod dmethod = (CurrentResult as MemberResult).ResolvedMember as DMethod;
					result += " " + dmethod.Parameters[paramIndex].Type.ToString() + " " +  dmethod.Parameters[paramIndex].Name;
				}
			if (CurrentResult is TypeResult)
				result += " " + (CurrentResult as TypeResult).ResolvedTypeDefinition.Description;
			return result;
		}

		public int GetParameterCount (int overload)
		{			
			selIndex = overload;			
			if (CurrentResult is MemberResult)
				if (((CurrentResult as MemberResult).ResolvedMember is DMethod))
				    return  ((CurrentResult as MemberResult).ResolvedMember as DMethod).Parameters.Count;
			return 0;
		}

		public int OverloadCount 
		{
				get { return args.ResolvedTypesOrMethods.Length; }
		}
		#endregion
	}
}

