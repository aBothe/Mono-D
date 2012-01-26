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
using MonoDevelop.D.Building;
using System.Collections.Generic;
using System.Threading;

namespace MonoDevelop.D
{
	public class DLanguageBinding: ILanguageBinding
	{
		public static DLanguageBinding Instance { get; private set; }

		public DLanguageBinding()
		{
			Instance = this;

			// Init compiler configurations if not done yet
			if (!DCompilerService.IsInitialized)
			{
				DCompilerService.Load();

				// Init global parse cache
				DCompilerService.Instance.UpdateParseCachesAsync();
			}
		}

		~DLanguageBinding()
		{
			DCompilerService.Instance.Save();
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

