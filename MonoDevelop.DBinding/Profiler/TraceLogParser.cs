using System;
using MonoDevelop.D.Profiler.Gui;
using System.IO;
using System.Text.RegularExpressions;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver;
using MonoDevelop.D.Refactoring;

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
		
		public void Parse(DProject project, string folder)
		{
			lastProfiledProject = project;
			profilerPadWidget.ClearTracedFunctions();
			
			string file = Path.Combine(folder, "trace.log");
			if(File.Exists(file) == false)
			{
				profilerPadWidget.AddTracedFunction(0,0,0,0,"trace.log not found..");
				return;
			}
				
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
		
		public void GoToFunction(string functionSymbol)
		{
			if(lastProfiledProject == null)
				return;
				
			ITypeDeclaration typeDeclaration;
			DMethod method = DParser.ParseMethodDeclarationHeader(functionSymbol, out typeDeclaration);
			string moduleName = ModuleName(typeDeclaration);
			
			var modules = lastProfiledProject.ParseCache.LookupModuleName(moduleName);
			
			
//			foreach(var module in modules)
//			{
			INode node = SearchFunctionNode(method,typeDeclaration, new DModule{ModuleName="___dummy___"});
				if(node != null)
				{
					RefactoringCommandsExtension.GotoDeclaration(node);
					return;
				}
//			}
		}
		
		private INode SearchFunctionNode(DMethod method, ITypeDeclaration typeDeclaration, IBlockNode module)
		{
			ResolutionContext context = ResolutionContext.Create(lastProfiledProject.ParseCache, null,module);
			context.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
			
			AbstractType[] foundedTypes = TypeDeclarationResolver.Resolve(typeDeclaration,context);
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
		
		
		// buggy..
		private string ModuleName(ITypeDeclaration type)
		{
			if(type.InnerDeclaration != null)
				return ModuleName(type.InnerDeclaration);
			if(type is IdentifierDeclaration)
				return ((IdentifierDeclaration)type).Id;
			return null;
		}
	}
}

