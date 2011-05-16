using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core;

namespace MonoDevelop.D
{
	public enum DMDVersion:int
	{
		V1=1,
		V2=2
	}

	public abstract class DMDCompiler
	{
		public abstract DMDVersion Version { get; }



		public BuildResult Compile(DProject dProject, ProjectFileCollection Files, ConfigurationSelector configuration, IProgressMonitor monitor)
		{
			throw new NotImplementedException();
		}
	}
}
