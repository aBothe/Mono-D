using System;
using MonoDevelop.D.Profiler.Gui;
using System.IO;
using System.Text.RegularExpressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver;
using MonoDevelop.D.Refactoring;
using MonoDevelop.D.Profiler.Commands;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Profiler
{
	public class TraceLogParser
	{
		private ProfilerPadWidget profilerPadWidget;
		private DProject lastProfiledProject;
		
		public TraceLogParser (ProfilerPadWidget widget)
		{
			profilerPadWidget = widget;
		}
		
		public static string TraceLogFile(DProject project)
		{
			if(project == null)
				return null;
		
			var config = project.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;
			
			if (config == null || 
			    config.CompileTarget != DCompileTarget.Executable || 
			    project.Compiler.HasProfilerSupport == false)
			{
				return null;
			}
			
			
			string file = Path.Combine(config.OutputDirectory, "trace.log");
			if(File.Exists(file) == false)
				return null;
			return file;
		}
		
		public void Parse(DProject project)
		{
			string file = TraceLogFile(project);
			if(file == null)
			{
				profilerPadWidget.AddTracedFunction(0,0,0,0,"trace.log not found..");
				return;
			}
		
			lastProfiledProject = project;
			profilerPadWidget.ClearTracedFunctions();
				
			StreamReader reader = File.OpenText(file);
			string line;
			while ((line = reader.ReadLine()) != null) {
				Match m = Regex.Match(line, "^\\s+(\\d+)\\s+(\\d+)\\s+(\\d+)\\s+(\\d+)\\s+(\\S\\P{C}[\\P{C}.]*)$");
				
				if (m.Success) {
					profilerPadWidget.AddTracedFunction(long.Parse(m.Groups[1].Value), long.Parse(m.Groups[2].Value), 
					                                    long.Parse(m.Groups[3].Value), long.Parse(m.Groups[4].Value), m.Groups[5].Value);
				}
			}
		}
		public void Clear()
		{
			profilerPadWidget.ClearTracedFunctions();
		}
		
		public void GoToFunction(string functionSymbol)
		{
			if(lastProfiledProject == null)
				return;
				
			ITypeDeclaration typeDeclaration;
			DMethod method = DParser.ParseMethodDeclarationHeader(functionSymbol, out typeDeclaration);
				
			INode node = SearchFunctionNode(method, typeDeclaration, new DModule{ModuleName="___dummy___"});
			if(node != null)
				RefactoringCommandsExtension.GotoDeclaration(node);
		}
		
		private INode SearchFunctionNode(DMethod method, ITypeDeclaration typeDeclaration, IBlockNode module)
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
		}
	}
}

