using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public abstract class DubFileReader
	{
		public abstract bool CanLoad(string file);

		public abstract DubProject Load(StreamReader s, string originalFileName);
		public DubProject Load(string file)
		{
			using (var fs = new FileStream(file, FileMode.Open))
			using (var sr = new StreamReader(fs))
				return Load(sr, file);
		}
	}
}
