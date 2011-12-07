using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.Projects;
using D_Parser.Dom;

namespace MonoDevelop.D.Refactoring
{
	public class ReferenceFinder
	{
		public static IEnumerable<SearchResult> FindReferences(Solution sln, INode member, ISearchProgressMonitor monitor=null)
		{
			//TODO
			return new List<SearchResult>();
		}
	}
}
