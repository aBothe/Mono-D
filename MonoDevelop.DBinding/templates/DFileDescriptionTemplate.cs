using System.Collections.Generic;
using D_Parser.Dom;
using MonoDevelop.Core;
using MonoDevelop.D.Formatting;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.StandardHeader;
using MonoDevelop.Ide.Templates;

namespace MonoDevelop.D.templates
{
	public class DFileDescriptionTemplate : TextFileDescriptionTemplate
	{
		bool addStdHeader = true;

		public override string CreateContent(MonoDevelop.Projects.Project project, Dictionary<string, string> tags, string language)
		{
			var cc = base.CreateContent(project, tags, language);

			if (addStdHeader)
			{
				StandardHeaderPolicy headerPolicy = project != null ? project.Policies.Get<StandardHeaderPolicy>() : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<StandardHeaderPolicy>();
				TextStylePolicy textPolicy = project != null ? project.Policies.Get<TextStylePolicy>("text/plain") : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<TextStylePolicy>("text/plain");
				DFormattingPolicy dPolicy = project != null ?	project.Policies.Get<DFormattingPolicy>("text/x-d") : MonoDevelop.Projects.Policies.PolicyService.GetDefaultPolicy<DFormattingPolicy>("text/x-d");

				if (string.IsNullOrWhiteSpace(headerPolicy.Text) || !headerPolicy.IncludeInNewFiles)
					return cc;

				var eol = TextStylePolicy.GetEolMarker(textPolicy.EolMarker);

				var hdr= StringParserService.Parse(headerPolicy.Text, tags);

				if (dPolicy.CommentOutStandardHeaders)
				{
					var headerLines = hdr.Split('\n');

					if (headerLines.Length == 1)
						return "/// " + headerLines[0].Trim() + eol + cc;
					else
					{
						var ret = "/**" + eol;
						for (int i = 0; i < headerLines.Length; i++)
							ret += " * " + headerLines[i].Trim() + eol;
						return ret + " */" + eol + cc;
					}
				}
				else
					return hdr + eol + cc;
			}

			return cc;
		}

		public override void ModifyTags(
			MonoDevelop.Projects.SolutionItem policyParent,
			MonoDevelop.Projects.Project project, 
			string language, 
			string identifier, 
			string fileName, 
			ref Dictionary<string, string> tags)
		{
			base.ModifyTags(policyParent, project, language, identifier, fileName, ref tags);

			if(tags!=null)
				tags["ModuleName"] = DModule.GetModuleName(project == null ? string.Empty : project.BaseDirectory.ToString(), fileName ?? identifier ?? string.Empty).Replace(' ','_');
		}
	}
}
