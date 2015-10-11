//
// DubReferencesCollection.cs
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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MonoDevelop.D.Building;
using System.IO;
using MonoDevelop.Core;
using System.Collections;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubReferencesCollection : DProjectReferenceCollection, IEnumerable<DubProjectDependency>
	{
		public new DubProject Owner => base.Owner as DubProject;
		public override event EventHandler Update;

		internal Dictionary<string, DubProjectDependency> dependencies = new Dictionary<string, DubProjectDependency>();

		public DubReferencesCollection (DubProject prj) : base(prj)
		{
		}

		public override bool CanAdd => false;

		public override bool CanDelete => false;

		public override void DeleteProjectRef (string projectId)
		{
			throw new NotImplementedException ();
		}

		public override void FireUpdate ()
		{
			if (Update != null)
				Update (this, EventArgs.Empty);
		}

		public override bool HasReferences => dependencies.Count > 0;

		public override string GetIncludeName (string path)
		{
			foreach (var kv in dependencies)
				if (kv.Value.Path == path)
					return kv.Key;

			path = Path.GetFullPath(ProjectBuilder.EnsureCorrectPathSeparators(path));
			foreach (var prj in Ide.IdeApp.Workspace.GetAllProjects ())
				if (prj.BaseDirectory.ToString() == path)
					return prj.Name;

			return path;
		}

		public override IEnumerable<string> Includes {
			get {
				var sub = Owner as DubSubPackage;
				if (sub != null)
					sub.useOriginalBasePath = true;
				var dir = Owner.BaseDirectory;
				if (sub != null)
					sub.useOriginalBasePath = false;

				foreach (var settings in Owner.GetBuildSettings(null))
				{
					List<DubBuildSetting> l;
					if (settings.TryGetValue (DubBuildSettings.ImportPathsProperty, out l))
						foreach (var v in l) // Ignore architecture/os/compiler restrictions for now
							foreach (var p in v.Values)
								yield return dir.ToAbsolute (p);
				}
			}
		}

		public override IEnumerable<string> ReferencedProjectIds {
			get {
				var allProjects = Ide.IdeApp.Workspace.GetAllProjects ();
				foreach (var kv in dependencies){
					var depPath = kv.Value.Name;
					foreach (var prj in allProjects)
						if (prj is DubProject ? ((prj as DubProject).packageName == depPath) : prj.Name == depPath)
							yield return prj.ItemId;
				}
			}
		}

		public override bool AddReference ()
		{
			throw new NotImplementedException ();
		}

		public IEnumerator<DubProjectDependency> GetEnumerator() => dependencies.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}

