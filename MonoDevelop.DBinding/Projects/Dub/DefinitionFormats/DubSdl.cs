using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	class DubSdl : DubFileReader
	{
		public override bool CanLoad(string file)
		{
			throw new NotImplementedException();
		}

		protected override void Read(DubProject target, StreamReader r)
		{
			throw new NotImplementedException();
		}
	}
}
