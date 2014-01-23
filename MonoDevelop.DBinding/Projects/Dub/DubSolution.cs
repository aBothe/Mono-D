using MonoDevelop.Projects;
using System;
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

		protected override BuildResult OnBuild (MonoDevelop.Core.IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			var s = StartupItem as Project;

			if (s == null)
				return new BuildResult{ FailedBuildCount = 1, CompilerOutput = "No default package specified!", BuildCount = 0 };

			return StartupItem.Build(monitor, configuration);
		}
	}
}
