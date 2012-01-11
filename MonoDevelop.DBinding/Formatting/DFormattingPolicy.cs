using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Policies;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting
{
	[PolicyType("D formatting")]
	public class DFormattingPolicy: IEquatable<DFormattingPolicy>
	{
		public bool Equals (DFormattingPolicy other)
		{
			return base.Equals (other);
		}
		
		public DFormattingPolicy Clone ()
		{
			//TODO: Clone object with all its properties!
			return new DFormattingPolicy ();	
		}
	}
}
