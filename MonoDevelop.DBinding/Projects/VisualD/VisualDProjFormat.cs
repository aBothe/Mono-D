//
// VisualDProjReader.cs
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
using MonoDevelop.Projects.Extensions;
using MonoDevelop.Core;
using System.Collections.Generic;
using MonoDevelop.Projects;
using System.Xml;
using System.IO;

namespace MonoDevelop.D.Projects.VisualD
{
	public class VisualDProjFormat : IFileFormat
	{
		public const string visualDExt = ".visualdproj";

		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.Extension.Equals (visualDExt, StringComparison.InvariantCultureIgnoreCase);
		}

		public VisualDProjFormat ()
		{
		}

		public FilePath GetValidFormatName (object obj, FilePath fileName)
		{
			return fileName.ChangeExtension (visualDExt);
		}

		public bool CanWriteFile (object obj)
		{
			return false;
		}

		public IEnumerable<string> GetCompatibilityWarnings(object obj)
		{
			yield return string.Empty;
		}

		public List<FilePath> GetItemFiles(object obj)
		{
			return new List<FilePath>();
		}

		public void WriteFile (FilePath file, object obj, IProgressMonitor monitor)
		{
			throw new NotImplementedException ();
		}

		public object ReadFile (FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			if (!expectedType.Equals (typeof(SolutionItem)))
				return null;

			using (var s = File.OpenText (file))
			using (var r = new XmlTextReader (s))
				return Read (file, r);
		}

		public bool SupportsFramework(Core.Assemblies.TargetFramework framework)
		{
			return false;
		}

		public bool SupportsMixedFormats
		{
			get { return true; }
		}


		public static VisualDProject Read(FilePath file, XmlReader x)
		{
			var sln = new Solution();
			
			var prj = new VisualDProject ();
			prj.FileName = file;
			
			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "ProjectGuid":
						//prj.ItemId = x.ReadString();
						break;
					case "Config":
						ReadConfig(prj, x);
						break;
					case "Folder":
						
						break;
				}
			}

			return prj;
		}


		public static void ReadConfig(VisualDProject prj, XmlReader x)
		{

		}

		public void ConvertToFormat(object obj)
		{
			
		}
	}
}

