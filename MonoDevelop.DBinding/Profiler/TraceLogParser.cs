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
				profilerPadWidget.AddTracedFunction("trace.log not found..","","","","");
				return;
			}
				
			StreamReader reader = File.OpenText(file);
			string line;
			while ((line = reader.ReadLine()) != null) {
				Match m = Regex.Match(line, @"^\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\S.*)");
				
				if (m.Success) {
					profilerPadWidget.AddTracedFunction(m.Groups[1].Value, m.Groups[2].Value, 
					                                    m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value);
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
			foreach(var module in modules)
			{//DModule
				INode node = SearchFunctionNode(method,typeDeclaration, module);
				if(node != null)
				{
					RefactoringCommandsExtension.GotoDeclaration(node);
					return;
				}
			}
		}
		
		private INode SearchFunctionNode(DMethod method, ITypeDeclaration typeDeclaration, IBlockNode module)
		{
			ResolutionContext context = ResolutionContext.Create(lastProfiledProject.ParseCache, null,module);
			
			AbstractType[] foundedTypes = TypeDeclarationResolver.Resolve(typeDeclaration,context);
			foreach(AbstractType type in foundedTypes)
			{
				MemberSymbol symbol = type as MemberSymbol;
				if(symbol == null)
					continue;
				DMethod methodSymbol = symbol.Definition as DMethod;
				if(methodSymbol == null)
					continue;
				
				if(methodSymbol.Type.ToString() != typeDeclaration.ToString())
					continue;
				
				if(CompareParamethers(methodSymbol, method))
					return methodSymbol;
			}
			return null;
		}
		
		private bool CompareParamethers(DMethod methodA, DMethod methodB)
		{
			if(methodA.Parameters.Count != methodB.Parameters.Count)
				return false;
			
			for(int i = 0; i < methodA.Parameters.Count; i++)
				if(methodA.Parameters[i].ToString() != methodB.Parameters[i].ToString())
					return false;
			return true;
		}
		
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

