using System;
using MonoDevelop.Projects;
using System.CodeDom.Compiler;
using MonoDevelop.Core;

namespace MonoDevelop.D
{
	public class DLanguageBinding: IDotNetLanguageBinding
	{
		public DLanguageBinding ()
		{
		}
		
		// Methods
		public ConfigurationParameters CreateCompilationParameters (XmlElement projectOptions);

		public ProjectParameters CreateProjectParameters (XmlElement projectOptions);

		public BuildResult Compile (ProjectItemCollection items, DotNetProjectConfiguration configuration, ConfigurationSelector configSelector, IProgressMonitor monitor)
		{
			return new BuildResult ("lol",1);
		}

		public ClrVersion[] GetSupportedClrVersions ()
			{return null;}

		public CodeDomProvider GetCodeDomProvider ()
		{
			return null;
		}

		// Properties
		public string ProjectStockIcon {
			get {return "d-project";} }
	}
}

