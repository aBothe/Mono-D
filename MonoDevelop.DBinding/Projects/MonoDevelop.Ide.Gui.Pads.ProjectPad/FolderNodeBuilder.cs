//
// FolderNodeBuilder.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// (Code parts are taken from MD and were not modified)

using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Projects;
using System.Collections;
using System.IO;

namespace MonoDevelop.Ide.Gui.Pads.ProjectPad
{
	public abstract class FolderNodeBuilder : TypeNodeBuilder
	{
		public override void GetNodeAttributes(ITreeNavigator treeNavigator, object dataObject, ref NodeAttributes attributes)
		{
			attributes |= NodeAttributes.AllowRename;
		}

		public abstract string GetFolderPath(object dataObject);

		public override void BuildChildNodes(ITreeBuilder builder, object dataObject)
		{
			string path = GetFolderPath(dataObject);

			Project project = builder.GetParentDataItem(typeof(Project), true) as Project;
			if (project == null)
				return;

			ProjectFileCollection files;
			ArrayList folders;
			GetFolderContent(project, path, out files, out folders);

			foreach (ProjectFile file in files)
				builder.AddChild(file);

			foreach (string folder in folders)
				builder.AddChild(new ProjectFolder(folder, project, dataObject));
		}

		void GetFolderContent(Project project, string folder, out ProjectFileCollection files, out ArrayList folders)
		{
			files = new ProjectFileCollection();
			folders = new ArrayList();
			string folderPrefix = folder + Path.DirectorySeparatorChar;

			foreach (ProjectFile file in project.Files)
			{
				string dir;

				if (file.Subtype != Subtype.Directory)
				{
					if (file.DependsOnFile != null)
						continue;

					dir = file.IsLink
						? project.BaseDirectory.Combine(file.ProjectVirtualPath).ParentDirectory
						: file.FilePath.ParentDirectory;

					if (dir == folder)
					{
						files.Add(file);
						continue;
					}
				}
				else
					dir = file.Name;

				// add the directory if it isn't already present
				if (dir.StartsWith(folderPrefix))
				{
					int i = dir.IndexOf(Path.DirectorySeparatorChar, folderPrefix.Length);
					if (i != -1) dir = dir.Substring(0, i);
					if (!folders.Contains(dir))
						folders.Add(dir);
				}
			}
		}

		public override bool HasChildNodes(ITreeBuilder builder, object dataObject)
		{
			Project project = builder.GetParentDataItem(typeof(Project), true) as Project;
			if (project == null)
				return false;

			// For big projects, a real HasChildNodes value is too slow to get
			if (project.Files.Count > 500)
				return true;

			ProjectFileCollection files;
			ArrayList folders;

			string path = GetFolderPath(dataObject);

			GetFolderContent(project, path, out files, out folders);

			if (files.Count > 0 || folders.Count > 0) return true;

			return false;
		}
	}
}
