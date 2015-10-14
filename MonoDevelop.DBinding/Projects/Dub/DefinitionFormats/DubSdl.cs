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
		public override bool CanLoad(string file) => Path.GetFileName(file).ToLower() == "dub.sdl";

		protected override void Read(DubProject target, Object input)
		{
			if (input is StreamReader)
			{
				var tree = SDL.SdlParser.Parse(input as StreamReader);
				//TODO: Display parse errors?

				foreach (var decl in tree.Children)
					ApplyProperty(decl, target);
			}
			else if (input is SDLObject)
				foreach (var decl in (input as SDLObject).Children)
					ApplyProperty(decl, target);
			else
				throw new ArgumentException("input");
		}

		void ApplyProperty(SDLDeclaration decl, DubProject target)
		{
			switch (decl.Name)
			{
				case "dependency":
					break;
			}
		}
	}
}
