//
// DefaultDReferencesCollection.cs
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
using System.Collections.ObjectModel;
using MonoDevelop.D.Building;
using System.Collections.Generic;

namespace MonoDevelop.D.Projects
{
	public class DefaultDReferencesCollection: DProjectReferenceCollection
	{
		public ObservableCollection<string> ProjectDependencies;

		public override event EventHandler Update;

		public DefaultDReferencesCollection(DProject prj, bool initDepCollection = true)
			: base(prj)
		{
			if(initDepCollection)
			{
				ProjectDependencies = new ObservableCollection<string>();
				ProjectDependencies.CollectionChanged+=OnProjectDepChanged;
			}
		}

		internal void InitRefCollection(IEnumerable<string> IDs, IEnumerable<string> includes)
		{
			ProjectDependencies = new ObservableCollection<string>(IDs);
			ProjectDependencies.CollectionChanged+=OnProjectDepChanged;

			foreach (var p in includes)
				RawIncludes.Add (ProjectBuilder.EnsureCorrectPathSeparators (p));
		}

		void OnProjectDepChanged(object o, System.Collections.Specialized.NotifyCollectionChangedEventArgs ea)
		{
			if(Update!=null)
				Update(o, ea);
		}

		public override void DeleteProjectRef (string projectId)
		{
			ProjectDependencies.Remove (projectId);
		}

		public override bool AddReference ()
		{
			throw new NotImplementedException ();
		}

		public override bool CanDelete {
			get {
				return true;
			}
		}

		public override bool CanAdd {
			get {
				return true;
			}
		}

		public override IEnumerable<string> ReferencedProjectIds {
			get {
				return ProjectDependencies;
			}
		}

		public override bool HasReferences {
			get {
				return ProjectDependencies.Count > 0 || base.HasReferences;
			}
		}

		public override void FireUpdate ()
		{
			if(Update!=null)
				Update (this, null);
		}
	}
}

