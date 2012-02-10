using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Ide.Templates;

namespace MonoDevelop.D.templates
{
	public class DFileDescriptionTemplate : TextFileDescriptionTemplate
	{
		public override void ModifyTags(
			Projects.SolutionItem policyParent, 
			Projects.Project project, 
			string language, 
			string identifier, 
			string fileName, 
			ref Dictionary<string, string> tags)
		{
			base.ModifyTags(policyParent, project, language, identifier, fileName, ref tags);

			tags["ModuleName"] = 
				Path.ChangeExtension(new FilePath(fileName)
				.ToRelative(project.BaseDirectory),null)
				.Replace(Path.DirectorySeparatorChar,'.')
				.Replace(' ','_');
		}
	}
}
