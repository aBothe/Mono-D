using System;
using System.Collections.Generic;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public class JSONArray : JSONThing
	{
		public readonly List<JSONThing> Items = new List<JSONThing>();
	}
}

