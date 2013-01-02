using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using System.Xml;
using System.IO;

namespace MonoDevelop.D
{
	public class DProjectBinding:IProjectBinding
	{
		public bool CanCreateSingleFileProject(string sourceFile)
		{
			return DLanguageBinding.IsDFile(sourceFile);
		}

		public Project CreateProject(ProjectCreateInformation info, XmlElement projectOptions)
		{
			return new DProject(info,projectOptions);
		}

		public Project CreateSingleFileProject(string sourceFile)
		{
			// Create project information using sourceFile's path
			var info = new ProjectCreateInformation()
			{
				ProjectName = Path.GetFileNameWithoutExtension(sourceFile),
				SolutionPath = Path.GetDirectoryName(sourceFile),
				ProjectBasePath = Path.GetDirectoryName(sourceFile),
			};

			var prj = CreateProject(info, null);
			prj.AddFile(sourceFile);
			return prj;
		}

		public string Name
		{
			get { return "D"; }
		}
	}
}
