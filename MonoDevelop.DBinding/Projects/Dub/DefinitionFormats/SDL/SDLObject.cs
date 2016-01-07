using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL
{
	public class SDLObject : SDLDeclaration
	{
		public readonly SDLDeclaration[] Children;

		public SDLObject(string name, IEnumerable<Tuple<string, string>> attributes, SDLDeclaration[] children) : base(name, attributes) {
			Children = children;
		}
	}
}
