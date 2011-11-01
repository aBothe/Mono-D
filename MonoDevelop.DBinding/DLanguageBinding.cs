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

namespace MonoDevelop.D
{
	public class DLanguageBinding: ILanguageBinding
	{
		#region Properties

		public static DLanguageBinding Instance { get; private set; }
		
		private static DIncludesParser dIncludesParser=null;
		public static DIncludesParser DIncludesParser { get{ return (dIncludesParser == null)? dIncludesParser = new DIncludesParser(): dIncludesParser;}}

		/// <summary>
		/// Workaround for handling serializer issue:
		/// Although GlobalParseCache isn't marked as serializable, there's an error thrown that ASTStorage isn't implementing Add(System.Object) ..
		/// </summary>
		static Dictionary<DCompilerVendor, ASTStorage> GlobalParseCaches = new Dictionary<DCompilerVendor, ASTStorage>(); // Note: This property has to be (de-)serialized manually!

		/// <summary>
		/// Stores code libraries paths. 
		/// These libraries will be scanned by the DParser and used for providing code completion later on.
		/// </summary>
		public static ASTStorage GetGlobalParseCache(DCompilerVendor Vendor)
		{
			if (!GlobalParseCaches.ContainsKey(Vendor))
				GlobalParseCaches.Add(Vendor, new ASTStorage());

			return GlobalParseCaches[Vendor];
		}
		#endregion

		public DLanguageBinding()
		{
			Instance = this;

			// Init compiler configurations
			DCompiler.Init();
		}

		~DLanguageBinding()
		{
			DCompiler.Instance.Save();
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

