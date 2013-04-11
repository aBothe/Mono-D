using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubSolution : Solution
	{
		public override string Name
		{
			get
			{
				return StartupItem != null ? StartupItem.Name : base.Name;
			}
			set
			{
				
			}
		}

		public override Core.FilePath FileName
		{
			get
			{
				return StartupItem != null ? StartupItem.Name : base.Name;
			}
			set
			{
			}
		}
	}
}
