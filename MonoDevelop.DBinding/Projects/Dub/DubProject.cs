using MonoDevelop.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubProject : AbstractDProject
	{
		protected override List<FilePath> OnGetItemFiles(bool includeReferencedFiles)
		{
			var files = new List<FilePath>();

			foreach (var f in Directory.GetFiles(BaseDirectory.Combine("source"), "*", SearchOption.AllDirectories))
				files.Add(new FilePath(f));

			return files;
		}
	}
}
