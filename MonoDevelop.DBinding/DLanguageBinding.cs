using System;
using MonoDevelop.Projects;
using System.CodeDom.Compiler;
using MonoDevelop.Core;
using System.Xml;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Projects.CodeGeneration;
using MonoDevelop.D.Parser;
using System.Reflection;

namespace MonoDevelop.D
{
	public class DLanguageBinding: ILanguageBinding
	{
		public DLanguageBinding()
		{
			
		}

		public static bool IsDFile(string fileName)
		{
			return fileName.EndsWith(".d") || fileName.EndsWith(".di");
		}

		public bool IsSourceCodeFile (string fileName)
		{
			return IsDFile(fileName);
		}

		public string GetFileName (string fileNameWithoutExtension)
		{
			return fileNameWithoutExtension + ".d";
		}

		public string Language {
			get {
				return "D";
			}
		}

		public string SingleLineCommentTag {
			get {
				return "//";
			}
		}

		public string BlockCommentStartTag {
			get {
				return "/*";
			}
		}

		public string BlockCommentEndTag {
			get {
				return "*/";
			}
		}

		DParserWrapper parser = new DParserWrapper();
		public IParser Parser {
			get {
				return parser;
			}
		}

		public IRefactorer Refactorer {
			get {
				return null;
			}
		}

		public BuildResult Compile(ProjectItemCollection items, DotNetProjectConfiguration configuration, ConfigurationSelector configSelector, IProgressMonitor monitor)
		{
			throw new NotImplementedException();
		}

		public ConfigurationParameters CreateCompilationParameters(XmlElement projectOptions)
		{
			throw new NotImplementedException();
		}

		public ProjectParameters CreateProjectParameters(XmlElement projectOptions)
		{
			throw new NotImplementedException();
		}

		public CodeDomProvider GetCodeDomProvider()
		{
			throw new Exception();
		}

		public ClrVersion[] GetSupportedClrVersions()
		{
			return null;
		}

		public string ProjectStockIcon
		{
			get { throw new NotImplementedException(); }
		}
	}
}

