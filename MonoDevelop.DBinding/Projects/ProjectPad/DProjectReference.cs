//
// DProjectReference.cs
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
using D_Parser.Misc;
using MonoDevelop.D.Projects;
using System.ComponentModel;
using MonoDevelop.Projects;
using System.IO;

namespace MonoDevelop.D.Projects.ProjectPad
{
	class DProjectReference : INotifyPropertyChanged
	{
		public Func<string,string> NameGetter;
		public readonly AbstractDProject OwnerProject;
		public readonly ReferenceType ReferenceType;

		string reference;
		public string Reference
		{
			get{return reference;}
			set{
				reference = value;
				PropChanged ("Reference");
			}
		}

		public Project ReferencedProject
		{
			get{
				if (ReferenceType != ReferenceType.Project)
					return null;

				foreach (var prj in Ide.IdeApp.Workspace.GetAllProjects())
					if (prj.ItemId == reference)
						return prj;

				return new UnknownProject ();
			}
		}

		public virtual string Name {
			get{ 
				if(NameGetter != null)
					return NameGetter(reference);

				switch (ReferenceType) {
					case ReferenceType.Package:
						return reference;
					case ReferenceType.Project:
						var prj = ReferencedProject;
						return prj == null ? "<No Project specified>" : prj.Name;
					default:
						throw new InvalidDataException ("Invalid case");
				}
			}
		}

		public virtual bool IsValid {
			get{
				switch (ReferenceType) {
					case ReferenceType.Package:
						return Directory.Exists (reference);
					case ReferenceType.Project:
						var prj = ReferencedProject;
						return prj != null && !(prj is UnknownProject);
					default:
						throw new InvalidDataException ("Invalid case");
				}
			}
		}

		public virtual string ValidationErrorMessage{
			get{
				switch (ReferenceType) {
					case ReferenceType.Package:
						return "Directory '"+reference+"' not found";
					case ReferenceType.Project:
						return "Invalid or unknown project";
					default:
						throw new InvalidDataException ("Invalid case");
				}
			}
		}

		public DProjectReference (AbstractDProject Owner, ReferenceType refType, string reference)
		{
			OwnerProject = Owner;
			ReferenceType = refType;
			this.reference = reference;
		}

		protected void PropChanged(string n)
		{
			if(PropertyChanged!=null)
				PropertyChanged(this, new PropertyChangedEventArgs(n));
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}

