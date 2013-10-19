//
// DubSettings.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using MonoDevelop.Core;
using System.Xml;

namespace MonoDevelop.D
{
	public class DubSettings : ICustomXmlSerializer
	{
		#region Properties
		const string DubSettingsPropId = "MonoDevelop.D.Dub";
		static DubSettings inst;
		public static DubSettings Instance
		{
			get{ 
				if (inst == null) {
					inst = PropertyService.Get<DubSettings> (DubSettingsPropId);
					if (inst == null)
						inst = new DubSettings ();
				}

				return inst;
			}
		}

		public static void Save()
		{
			PropertyService.Set (DubSettingsPropId, Instance);
		}

		public string DubCommand = "dub";
		#endregion

		#region Loading & Saving
		public void WriteTo (XmlWriter w)
		{
			w.WriteStartElement ("cmd");
			w.WriteCData (DubCommand);
			w.WriteEndElement ();
		}

		public ICustomXmlSerializer ReadFrom (XmlReader r)
		{
			while (r.Read ()) {
				switch (r.LocalName) {
					case "cmd":
						DubCommand = r.ReadString ();
						break;
				}
			}
			return this;
		}
		#endregion
	}
}

