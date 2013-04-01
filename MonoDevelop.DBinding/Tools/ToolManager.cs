using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

/*
 * A tool is a command override for various functions in Mono-D such
 * as formatting, building, running a project, getting code completion(?), refactoring options, getting further code info.
 */

namespace MonoDevelop.D.Tools
{
	[JsonObject]
	public class ProjectTools
	{
		#region Properties
		public readonly DProject Project;
		public const string ToolsJson = "tools.json";

		public string AbsoluteFilePath
		{
			get { return Project.BaseDirectory.Combine(ToolsJson).ToString(); }
		}

		public List<Tool> Tools = new List<Tool>();
		#endregion

		public ProjectTools(DProject prj)
		{
			Project = prj;
		}

		public void Reload()
		{
			
		}

		[JsonObject]
		public class Tool
		{
			public string Type;

			public bool Active;
			public string Command;
			public string Arguments;
		}
	}
}
