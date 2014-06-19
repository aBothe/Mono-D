using MonoDevelop.Ide.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.templates
{
	class AnsiFile : TextFileDescriptionTemplate
	{
		public override System.IO.Stream CreateFileContent(MonoDevelop.Projects.SolutionItem policyParent, MonoDevelop.Projects.Project project, string language, string fileName, string identifier)
		{
			var s = base.CreateFileContent(policyParent, project, language, fileName, identifier);

			using (var r = new StreamReader(s))
				return new MemoryStream(Encoding.ASCII.GetBytes(r.ReadToEnd()), false);
		}
	}
}
