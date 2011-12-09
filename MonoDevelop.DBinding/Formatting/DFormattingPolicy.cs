using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects.Policies;

namespace MonoDevelop.D.Formatting
{
	[PolicyType("D formatting")]
	public class DFormattingPolicy:IEquatable<DFormattingPolicy>
	{
		static DFormattingPolicy ()
		{
			PolicyService.InvariantPolicies.Set<DFormattingPolicy> (new DFormattingPolicy (), "text/x-d");
		}

		public bool Equals(DFormattingPolicy other)
		{
			throw new NotImplementedException();
		}
	}
}
