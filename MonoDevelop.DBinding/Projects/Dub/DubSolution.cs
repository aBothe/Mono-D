using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Projects.Dub
{
	/// <summary>
	/// A dub package container.
	/// </summary>
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
				return StartupItem != null ? StartupItem.FileName : base.FileName;
			}
			set
			{
			}
		}

		public override void Dispose ()
		{
			StartupItem = null;
			base.Dispose ();
			GC.ReRegisterForFinalize (this);
		}
	}
}
