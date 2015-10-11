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
		public override bool CanLoad(string file) => Path.GetFileName(file).ToLower() == "dub.sdl";

		protected override void Read(DubProject target, Object input)
		{
			var sr = (StreamReader)input;
			
			var tree = SDL.SdlParser.Parse(sr);
			//TODO: Display parse errors?

			foreach(var decl in tree.Children)
			{

			}
		}
	}
}
