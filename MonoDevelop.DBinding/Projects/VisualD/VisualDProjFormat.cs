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
using System.Text;

namespace MonoDevelop.D.Projects.VisualD
{
	public class VisualDProjFormat : IFileFormat
	{
		#region Properties
		public const string visualDExt = ".visualdproj";

		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.Extension.Equals (visualDExt, StringComparison.InvariantCultureIgnoreCase);
		}

		public bool SupportsFramework(Core.Assemblies.TargetFramework framework)
		{
			return false;
		}

		public bool SupportsMixedFormats
		{
			get { return true; }
		}
		#endregion

		public VisualDProjFormat ()
		{
		}

		public FilePath GetValidFormatName (object obj, FilePath fileName)
		{
			return fileName.ChangeExtension (visualDExt);
		}

		public bool CanWriteFile (object obj)
		{
			return obj is VisualDProject;
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
			try
			{
				using (var x = new XmlTextWriter(file, Encoding.UTF8))
					Write(obj as VisualDProject, x);
			}
			catch (Exception ex)
			{
				monitor.ReportError("Couldn't write project file", ex);
			}
		}

		static void Write(VisualDProject prj, XmlWriter x)
		{
			x.WriteStartDocument();

			x.WriteStartElement("DProject");

			x.WriteElementString("ProjectGuid", prj.ItemId);

			foreach (VisualDPrjConfig config in prj.Configurations)
			{
				x.WriteStartElement("Config");
				x.WriteAttributeString("name", config.Name);
				x.WriteAttributeString("platform", config.Platform);

				foreach (var kv in config.Properties)
					x.WriteElementString(kv.Key, kv.Value);

				x.WriteEndElement();
			}

			//TODO: Files

			x.WriteEndDocument();
			x.Close();
		}

		public object ReadFile (FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			VisualDProject prj = null;

			using (var s = File.OpenText (file))
				using (var r = new XmlTextReader (s))
					prj = Read (file, r);

			if (typeof(Project).IsSubclassOf (expectedType))
				return prj;

			if (typeof(Solution).IsSubclassOf (expectedType)) {
				var sln = new Solution ();
				sln.Name = prj.Name;
				sln.RootFolder.AddItem (prj);
				return sln;
			}

			return prj;
		}

		static string GetPath(Stack<string> folderStack, string filename = null)
		{
			var sb = new StringBuilder(256);
			bool isAbs = false;

			var backup = new Stack<string>(folderStack.Count);

			while (folderStack.Count > 0)
			{
				var p = folderStack.Pop();
				backup.Push(p);
				if (!string.IsNullOrWhiteSpace(p))
				{
					if (!Path.IsPathRooted(p))
						sb.Append(Path.DirectorySeparatorChar).Append(p);
					else
					{
						isAbs = true;
						sb.Clear().Append(p);
					}
				}
			}

			while (backup.Count > 0)
				folderStack.Push(backup.Pop());

			// Might be an absolute path on non-Windows systems!
			if (!isAbs && sb.Length > 0 && sb[0] == Path.DirectorySeparatorChar)
				sb.Remove(0, 1);

			if (!string.IsNullOrWhiteSpace(filename))
			{
				if (filename.StartsWith(sb.ToString()))
					sb.Clear();

				if (!Path.IsPathRooted(filename))
					sb.Append(Path.DirectorySeparatorChar).Append(filename);
				else
				{
					isAbs = true;
					sb.Clear().Append(filename);
				}
			}

			// Might be an absolute path on non-Windows systems!
			if (!isAbs && sb.Length > 0 && sb[0] == Path.DirectorySeparatorChar)
				sb.Remove(0, 1);

			return sb.ToString();
		}

		public static VisualDProject Read(FilePath file, XmlReader x)
		{
			var prj = new VisualDProject ();
			prj.FileName = file;
			var folderStack = new Stack<string>();
			string path;

			while (x.Read())
			{
				if (x.NodeType == XmlNodeType.Element)
					switch (x.LocalName)
					{
						case "ProjectGuid":
							prj.ItemIdToAssign = x.ReadString();
							break;
						case "Config":
							VisualDPrjConfig.ReadAndAdd(prj, x.GetAttribute("name"), x.GetAttribute("platform"), x.ReadSubtree());
							break;
						case "Folder":
							if (folderStack.Count == 0)
							{
								// Somehow, the very root Folder node gets merely ignored..somehow
								folderStack.Push(string.Empty);
								break;
							}

							folderStack.Push(Building.ProjectBuilder.EnsureCorrectPathSeparators(x.GetAttribute("name") ?? string.Empty));
							path = GetPath(folderStack);
							if(!string.IsNullOrWhiteSpace(path))
								prj.AddDirectory(path);
							break;
						case "File":
							var filePath = Building.ProjectBuilder.EnsureCorrectPathSeparators(x.GetAttribute("path"));
							//TODO: Custom tools that are executed right before building..gosh!
							if (!string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(path = GetPath(folderStack, filePath)))
								prj.AddFile(Path.IsPathRooted(path) ? path : prj.BaseDirectory.Combine(path).ToString(), BuildAction.Compile);
							break;
					}
				if (x.NodeType == XmlNodeType.EndElement && x.LocalName == "Folder")
					folderStack.Pop();
			}

			return prj;
		}

		public void ConvertToFormat(object obj)
		{
			
		}
	}
}

