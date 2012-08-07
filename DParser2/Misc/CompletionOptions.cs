using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace D_Parser.Misc
{
	public struct CompletionOptions
	{
		public readonly static CompletionOptions Default = new CompletionOptions
		{
			ShowUFCSItems = true
		};


		public bool ShowUFCSItems;


		public void Load(XmlReader x)
		{
			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "EnableUFCSCompletion":
						ShowUFCSItems = x.ReadString().ToLower() == "true";
						break;
				}
			}
		}

		public void Save(XmlWriter x)
		{
			x.WriteElementString("EnableUFCSCompletion", ShowUFCSItems.ToString());
		}
	}
}
