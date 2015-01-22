using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using D_Parser.Dom;
using D_Parser.Misc.Mangling;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Building;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.D.Refactoring;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Profiler
{
	public class TraceLogParser
	{
		private ProfilerPadWidget profilerPadWidget;
		public const string trace_log = "trace.log";
		
		public TraceLogParser (ProfilerPadWidget widget)
		{
			profilerPadWidget = widget;
		}

		public static string FindTraceLogFileName(AbstractDProject project)
		{
			if(project == null)
				return null;

			var file = project.GetAbsPath(trace_log);
			if (File.Exists (file))
				return file;

			file = project.GetOutputFileName (Ide.IdeApp.Workspace.ActiveConfiguration).ParentDirectory.Combine(trace_log);
			if (File.Exists (file))
				return file;

			return null;
		}
		
		static readonly Regex traceFuncRegex = new Regex (@"^\s*(?<numcalls>\d+)\s+(?<treetime>\d+)\s+(?<functime>\d+)\s+(?<percall>\d+)\s+(?<name>(\S\P{C}[\P{C}.]*))$",RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		
		public void Parse()
		{
			Clear();

			var editor = MonoDevelop.Ide.IdeApp.Workbench.ActiveDocument;
			var project = ((editor != null && editor.HasProject) ? editor.Project : MonoDevelop.Ide.IdeApp.ProjectOperations.CurrentSelectedProject) as AbstractDProject;
			var file = FindTraceLogFileName(project);
			if(file == null)
			{
				profilerPadWidget.AddTracedFunction(0,0,0,0,new DVariable{Name = trace_log+" not found.."});
				return;
			}
			
			var ctxt = ResolutionContext.Create(project.ParseCache, null, null);
			
			StreamReader reader = File.OpenText(file);
			string line;
			while ((line = reader.ReadLine()) != null) {
				var m = traceFuncRegex.Match(line);
				
				if (m.Success)
				{
					var symName = m.Groups["name"].Value;
					
					if(symName.StartsWith("="))
						continue;
					
					bool mightBeLegalUnresolvableSymbol;
					var dn = ExamTraceSymbol(symName, ctxt, out mightBeLegalUnresolvableSymbol);

					long v;
					if(dn != null || mightBeLegalUnresolvableSymbol)
						profilerPadWidget.AddTracedFunction(
							long.TryParse(m.Groups["numcalls"].Value, out v) ? v : 0, 
							long.TryParse(m.Groups["treetime"].Value, out v) ? v : 0, 
							long.TryParse(m.Groups["functime"].Value, out v) ? v : 0, 
							long.TryParse(m.Groups["percall"].Value, out v) ? v : 0, 
							dn ?? new DVariable{Name = symName});
				}
			}
		}

		public void Clear()
		{
			profilerPadWidget.ClearTracedFunctions();
		}
		
		public void GoToFunction(INode functionSymbol)
		{
			/*
			if(lastProfiledProject == null)
				return;
				
			ITypeDeclaration typeDeclaration;
			DMethod method = DParser.ParseMethodDeclarationHeader(functionSymbol, out typeDeclaration);
				
			INode node = SearchFunctionNode(method, typeDeclaration, new DModule{ModuleName="___dummy___"});
			if(node != null)*/
			RefactoringCommandCapsule.GotoDeclaration(functionSymbol);
		}
		
		public static DNode ExamTraceSymbol(string symName, ResolutionContext ctxt, out bool mightBeLegalUnresolvableSymbol)
		{
			DSymbol ds=null;
			mightBeLegalUnresolvableSymbol = false;
			
			if(string.IsNullOrWhiteSpace(symName))
				return null;
			
			// Try to handle a probably mangled string or C function.			
			if(symName.StartsWith("_"))
			{
				try{
					ds = Demangler.DemangleAndResolve(symName, ctxt) as DSymbol;
				}catch{}
			}
			
			// Stuff like void std.stdio.File.LockingTextWriter.put!(immutable(char)[]).put(immutable(char)[])
			if(ds == null && Lexer.IsIdentifierPart((int)symName[0]))
			{
				mightBeLegalUnresolvableSymbol = true;
				ITypeDeclaration q;
				var method = DParser.ParseMethodDeclarationHeader(symName, out q);
				q = Demangler.RemoveNestedTemplateRefsFromQualifier(q);

				AbstractType[] overloads = null;
				D_Parser.Completion.CodeCompletion.DoTimeoutableCompletionTask (null, ctxt, () => {
					try {
						overloads = TypeDeclarationResolver.HandleNodeMatches(DResolver.LookupIdRawly(ctxt.ParseCache, q), ctxt, null, q);
					}
					catch(Exception ex){ MonoDevelop.Core.LoggingService.LogWarning("Error during trace.log symbol resolution of "+q.ToString(), ex); }
				});
				
				if(overloads == null || overloads.Length == 0)
					return null;
				else if(overloads.Length == 1)
					ds = overloads[0] as DSymbol;
				else
				{
					method.Type = Demangler.RemoveNestedTemplateRefsFromQualifier(method.Type);
					var methodType = TypeDeclarationResolver.GetMethodReturnType(method, ctxt);

					var methodParameters = new List<AbstractType>();
					D_Parser.Completion.CodeCompletion.DoTimeoutableCompletionTask (null, ctxt, () => {
						if(method.Parameters != null && method.Parameters.Count != 0)
						{
							foreach(var parm in method.Parameters)
								methodParameters.Add(TypeDeclarationResolver.ResolveSingle(Demangler.RemoveNestedTemplateRefsFromQualifier(parm.Type),ctxt));
						}
					});

					foreach(var o in overloads)
					{
						ds = o as DSymbol;
						if(ds == null || !(ds.Definition is DMethod))
							continue;
						
						var dm = ds.Definition as DMethod;
						// Compare return types
						if(dm.Type != null)
						{
							if(methodType == null || ds.Base == null || !ResultComparer.IsEqual(methodType, ds.Base))
								continue;
						}
						else if(dm.Type == null && methodType != null)
							return null;
						
						// Compare parameters
						if(methodParameters.Count != dm.Parameters.Count)
							continue;
						
						for(int i = 0; i< methodParameters.Count; i++)
							if(!ResultComparer.IsImplicitlyConvertible(methodParameters[i], TypeDeclarationResolver.ResolveSingle(Demangler.RemoveNestedTemplateRefsFromQualifier(dm.Parameters[i].Type),ctxt)))
								continue;
					}
				}
			}
			
			return ds != null ? ds.Definition : null;
		}
		
		/*private INode SearchFunctionNode(DMethod method, ITypeDeclaration typeDeclaration, IBlockNode module)
		{
			ResolutionContext context = ResolutionContext.Create(lastProfiledProject.ParseCache, null,module);
			context.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
			
			AbstractType[] foundedTypes = TypeDeclarationResolver.Resolve(typeDeclaration,context);
			if(foundedTypes == null)
				return null;
				
			foreach(AbstractType type in foundedTypes)
			{
				MemberSymbol symbol = type as MemberSymbol;
				if(symbol == null)
					continue;
				DMethod methodSymbol = symbol.Definition as DMethod;
				if(methodSymbol == null)
					continue;
				
				if(CompareMethod(methodSymbol, method, context))
					return methodSymbol;
			}
			return null;
		}
		
		private bool CompareMethod(DMethod methodA, DMethod methodB, ResolutionContext context)
		{
			if(methodA.Name != methodB.Name )
				return false;
			return CompareParamethers(methodA, methodB, context);
		}
		
		private bool CompareParamethers(DMethod methodA, DMethod traceMethod, ResolutionContext context)
		{
			if(methodA.Parameters.Count != traceMethod.Parameters.Count)
				return false;
			
			for(int i = 0; i < methodA.Parameters.Count; i++)
			{
				if(methodA.Parameters[i].Type.ToString() != traceMethod.Parameters[i].Type.ToString())
				{
					AbstractType typeA = TypeDeclarationResolver.ResolveSingle(methodA.Parameters[i].Type, context);
					AbstractType typeB = TypeDeclarationResolver.ResolveSingle(traceMethod.Parameters[i].Type, context);
					if(ResultComparer.IsEqual(typeA is AliasedType ? ((AliasedType)typeA).Base :typeA, typeB) == false)
						return false;
				}
			}
			return true;
		}*/
	}
}

