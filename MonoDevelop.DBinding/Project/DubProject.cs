using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MonoDevelop.D.Dub
{
	public class DubProject : WorkspaceItem
	{
		string name;
		string description;
		string homepage;
		string copyright;
		List<string> authors = new List<string>();
		Dictionary<string, DubProjectDependency> dependencies = new Dictionary<string, DubProjectDependency>();
		/*
		{
			set {
				var deps = new Dictionary<string, DubProjectDependency>();

				if (value != null)
					foreach (var kv in value)
						deps[kv.Key] = new DubProjectDependency(kv.Key,kv.Value);

				Dependencies = deps;
			}
		}*/

		public override string Name { get { return name; } set { name = value; } }
		public string Description { get { return description; } set { description = value; } }
		public string Homepage { get { return homepage; } set { homepage = value; } }
		public string Copyright { get { return copyright; } set { copyright = value; } }
		public List<string> Authors { get { return authors; } }
		public Dictionary<string, DubProjectDependency> Dependencies
		{
			get { return dependencies; }
		}
	}

	public class DubProjectDependency
	{
		string name;
		string version;
		string path;

		public string Name { get { return name; } set { name = value; } }
		public string Version { get { return version; } set { version = value; } }
		public string Path { get { return path; } set { path = value; } }
	}
}
