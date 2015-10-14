using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL
{
	public class SDLDeclaration
	{
		public readonly string Name;
		public readonly Tuple<string, string>[] Attributes;

		public SDLDeclaration(string name, IEnumerable<Tuple<string, string>> attributes)
		{
			this.Name = name;
			this.Attributes = attributes.ToArray();
		}
	}
}
