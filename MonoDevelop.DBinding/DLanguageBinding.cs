using System;
using MonoDevelop.Projects;
using System.CodeDom.Compiler;
using MonoDevelop.Core;
using System.Xml;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Projects.CodeGeneration;
using MonoDevelop.D.Parser;
using System.Reflection;
using MonoDevelop.D.Completion;
using D_Parser.Completion;

namespace MonoDevelop.D
{
	public class DLanguageBinding: ILanguageBinding
	{
		#region Properties

		public static DLanguageBinding Instance { get; private set; }
		public static ASTStorage GlobalParseCache { get; private set; }
		
		private static DIncludesParser dIncludesParser=null;
		public static DIncludesParser DIncludesParser { get{ return (dIncludesParser == null)? dIncludesParser = new DIncludesParser(): dIncludesParser;}}
		#endregion

		public DLanguageBinding()
		{
			Instance = this;
			GlobalParseCache = new ASTStorage();
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
	}
}

