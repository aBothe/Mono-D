using System;
using System.Collections.Generic;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public class JSONObject : JSONThing
	{
		public readonly Dictionary<String, JSONThing> Properties = new Dictionary<string, JSONThing>();
	}
}

