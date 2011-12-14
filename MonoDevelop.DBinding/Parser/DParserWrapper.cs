using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects.Dom.Parser;
using D_Parser;
using MonoDevelop.Projects.Dom;
using System.IO;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using D_Parser.Dom;
using D_Parser.Parser;

namespace MonoDevelop.D.Parser
{
	/// <summary>
	/// Parses D code.
	/// 
	/// Note: For natively parsing the code, the D_Parser engine will be used. 
	/// To make it compatible to the MonoDevelop.Dom, its output will be wrapped afterwards!
	/// </summary>
	public class DParserWrapper : IParser
	{
		public bool CanParse(string fileName)
		{
			return DLanguageBinding.IsDFile(fileName);
		}

		public IExpressionFinder CreateExpressionFinder(ProjectDom dom)
		{
			return null;
		}

		public IResolver CreateResolver(ProjectDom dom, object editor, string fileName)
		{
			return null;
		}

		public ParsedDocument Parse(ProjectDom dom, string fileName, TextReader content)
		{
			return ParsedDModule.CreateFromDFile(dom,fileName, content);
		}

		public Projects.Dom.ParsedDocument Parse(ProjectDom dom, string fileName, string content)
		{
			return Parse(dom, fileName, new StringReader(content));
		}
	}
}
