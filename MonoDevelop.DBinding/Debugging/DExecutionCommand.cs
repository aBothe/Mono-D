using System;
using MonoDevelop.Core.Execution;

namespace MonoDevelop.D.Debugging
{
	public class DExecutionCommand : NativeExecutionCommand
	{
		public DExecutionCommand (string exe, string args = null) : base(exe, args)
		{
		}
	}
}

