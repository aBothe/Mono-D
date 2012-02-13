using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Ide.Templates;
using MonoDevelop.Ide.StandardHeader;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.templates
{
	public class DFileDescriptionTemplate : TextFileDescriptionTemplate
	{
		bool addStdHeader = true;

		public override string CreateContent(Projects.Project project, Dictionary<string, string> tags, string language)
		{
			var cc = base.CreateContent(project, tags, language);

			if (addStdHeader)
			{
				var textPolicy = project != null ? 
					project.Policies.Get<TextStylePolicy> ("text/plain") : 
					MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy> ("text/plain");

				var eol= TextStylePolicy.GetEolMarker(textPolicy.EolMarker);

				var hdr=StandardHeaderService.GetHeader(project, tags["FileName"], true).Trim();

				if (string.IsNullOrWhiteSpace(hdr))
					return cc;

				var headerLines=hdr.Split('\n');

				if (headerLines.Length == 1)
					return "///" + headerLines[0].TrimStart('/') + eol + cc;
				else
				{
					var ret="/**"+eol;
					for (int i = 0; i < headerLines.Length; i++)
					{
						ret += " * " + headerLines[i].TrimStart('/').Trim() + eol;
					}
					return ret + " */" + eol + cc;
				}
			}

			return cc;
		}

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
				Path.ChangeExtension(new FilePath(fileName ?? identifier)
				.ToRelative(project.BaseDirectory),null)
				.Replace(Path.DirectorySeparatorChar,'.')
				.Replace(' ','_');
		}
	}
}
