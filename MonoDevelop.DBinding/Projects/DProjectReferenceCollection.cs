//
// DProjectReferenceCollection.cs
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
using MonoDevelop.D.Projects;
using System.Collections.Generic;
using MonoDevelop.D.Building;
using System.IO;

namespace MonoDevelop.D.Projects
{
	public enum ReferenceType
	{
		Project,
		Package,
	}

	public abstract class DProjectReferenceCollection
	{
		public AbstractDProject Owner {get{return owner.Target as AbstractDProject;}}
		WeakReference owner;

		internal List<string> RawIncludes = new List<string>();

		public virtual bool CanDelete {get{ return true; }}
		public virtual bool CanAdd{ get {return true;}}

		readonly LocalIncludesMacroProvider includeMacros;
		class LocalIncludesMacroProvider : IArgumentMacroProvider
		{
			readonly WeakReference p;

			public LocalIncludesMacroProvider(WeakReference prj)
			{
				p = prj;
			}

			public void ManipulateMacros(Dictionary<string,string> macros)
			{
				var p = this.p.Target as AbstractDProject;
				if (p == null)
					return;
				macros ["solution"] = p.ParentSolution.BaseDirectory;
				macros ["project"] = p.BaseDirectory;
			}
		}

		public virtual string GetIncludeName(string path) { return path; }

		public virtual IEnumerable<string> Includes {
			get { 
				foreach (var p in ProjectBuilder.FillInMacros (RawIncludes, includeMacros)) {
					var path = p;
					if (!Path.IsPathRooted (path))
						path = Path.Combine (Owner.BaseDirectory, ProjectBuilder.EnsureCorrectPathSeparators(p));

					if(path.Contains(".."))
						// http://stackoverflow.com/questions/4796254/relative-path-to-absolute-path-in-c
						path = Path.GetFullPath(path);

					yield return path;
				}

				foreach (var p in Owner.Files) {
					if (p.IsLink && p.IsExternalToProject && Directory.Exists (p.Link)) {
						yield return p.Link;
					}
				}
			}
		}
		public virtual IEnumerable<string> ReferencedProjectIds {get { return new string[0];}}
		public virtual bool HasReferences {get { return RawIncludes.Count > 0; }}

		protected DProjectReferenceCollection(AbstractDProject owner)
		{
			this.owner = new WeakReference(owner);
			includeMacros = new LocalIncludesMacroProvider (this.owner);
		}

		public virtual void DeleteInclude(string path)
		{
			RawIncludes.Remove (path);
		}
		public abstract void DeleteProjectRef(string projectId);

		public abstract event EventHandler Update;
		public abstract void FireUpdate();

		public abstract bool AddReference();
	}
}

