using MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL;
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
			return Path.GetFileName(file).ToLower() == DubSdlFile;
		}

		protected override void Read(DubProject target, Object input)
		{
			if (input is StreamReader)
			{
				var tree = SDL.SdlParser.Parse(input as StreamReader);
				//TODO: Display parse errors?

				foreach (var decl in tree.Children)
					InterpretGlobalProperty(decl, target);
			}
			else if (input is SDLObject)
				foreach (var decl in (input as SDLObject).Children)
					InterpretGlobalProperty(decl, target);
			else
				throw new ArgumentException("input");
		}

		void InterpretGlobalProperty(SDLDeclaration decl, DubProject target)
		{
			switch (decl.Name.ToLower())
			{
				case "name":
					target.Name = ExtractFirstAttribute(decl);
					break;
				case "description":
					target.Description = ExtractFirstAttribute(decl);
					break;
				case "homepage":
					target.Homepage = ExtractFirstAttribute(decl);
					break;
				case "authors":
					target.Authors.Clear();
					target.Authors.AddRange(from kv in decl.Attributes where kv.Item1 == null select kv.Item2);
					break;
				case "copyright":
					target.Copyright = ExtractFirstAttribute(decl);
					break;
				case "subpackage":
					if(decl is SDLObject)
						base.Load(target, target.ParentSolution, decl, target.FileName);
					else
						DubFileManager.Instance.LoadProject(GetDubFilePath(target, ExtractFirstAttribute(decl)), target.ParentSolution, null, DubFileManager.LoadFlags.None, target);
					break;
				case "configuration":
					var o = decl as SDLObject;
					if(o != null)
					{
						var c = new DubProjectConfiguration { Name = ExtractFirstAttribute(o) };
						if (string.IsNullOrEmpty(c.Name))
							c.Name = "<Undefined>";

						foreach(var childDecl in o.Children)
							InterpretBuildSetting(childDecl, c.BuildSettings);

						target.AddProjectAndSolutionConfiguration(c);
					}
					break;
				case "buildtype":
					var name = ExtractFirstAttribute(decl);
					if (!string.IsNullOrEmpty(name))
					{
						target.buildTypes.Add(name);
					}
					// Ignore remaining contents as they're not needed by mono-d

					target.buildTypes.Sort();
					break;
				default:
					InterpretBuildSetting(decl, target.CommonBuildSettings);
					break;
			}
		}

		void InterpretBuildSetting(SDLDeclaration decl, DubBuildSettings settings)
		{
			switch (decl.Name.ToLower())
			{
				case "dependency":
					var depName = ExtractFirstAttribute(decl);
					var depVersion = ExtractFirstAttribute(decl, "version");
					var depPath = ExtractFirstAttribute(decl, "path");


					break;
			}
		}

		/// <returns>string.Empty if nothing found</returns>
		string ExtractFirstAttribute(SDLDeclaration d,string key = null)
		{
			var i = d.Attributes.SingleOrDefault((kv) => kv.Item1 == key);
			return i != null ? i.Item2 : string.Empty;
		}
	}
}
