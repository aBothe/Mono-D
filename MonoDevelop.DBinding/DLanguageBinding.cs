using System;
using MonoDevelop.Projects;
using System.CodeDom.Compiler;
using MonoDevelop.Core;
using System.Xml;


namespace MonoDevelop.D
{
	public class DLanguageBinding: ILanguageBinding
	{
		public DLanguageBinding ()
		{
		}

		public bool IsSourceCodeFile (string fileName)
		{
			return fileName.EndsWith (".d") || fileName.EndsWith (".di");
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

		public MonoDevelop.Projects.Dom.Parser.IParser Parser {
			get {
				return null;
			}
		}

		public MonoDevelop.Projects.CodeGeneration.IRefactorer Refactorer {
			get {
				return null;
			}
		}
	}
}

