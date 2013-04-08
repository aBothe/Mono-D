using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Projects
{
	public abstract class AbstractDProject : Project
	{
		#region Properties
		public override string ProjectType
		{
			get { return "Native"; }
		}

		#endregion
	}
}
