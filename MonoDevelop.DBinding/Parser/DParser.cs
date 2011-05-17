using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Dom.Parser;
using System.IO;
using MonoDevelop.Projects.Dom;
using MonoDevelop.D.Parser.Lexer;

/*
 * Parser "Front-End" - contains all methods & properties that can be used externally 
 */

namespace MonoDevelop.D.Parser
{
	public partial class DParser:DTokens,IParser
	{
		public bool CanParse(string fileName)
		{
			throw new NotImplementedException();
		}

		public IExpressionFinder CreateExpressionFinder(ProjectDom dom)
		{
			throw new NotImplementedException();
		}

		public IResolver CreateResolver(ProjectDom dom, object editor, string fileName)
		{
			throw new NotImplementedException();
		}

		public ParsedDocument Parse(ProjectDom dom, string fileName, TextReader content)
		{
			var doc = new ParsedDocument(fileName)
			{
				CompilationUnit = new CompilationUnit(fileName)
			};

			var lexer = new DLexer(content);

			this.lexer = lexer;

			Root(doc.CompilationUnit);

			return doc;
		}

		public ParsedDocument Parse(ProjectDom dom, string fileName, string content)
		{
			return Parse(dom, fileName, new StringReader(content));
		}
	}
}
