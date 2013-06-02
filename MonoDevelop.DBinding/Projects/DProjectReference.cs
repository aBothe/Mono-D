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

namespace MonoDevelop.D
{
	public enum ReferenceType
	{
		Project,
		Package,
	}

	public class DProjectReference : INotifyPropertyChanged
	{
		public readonly AbstractDProject OwnerProject;
		public readonly ReferenceType ReferenceType;

		public virtual string Name {get{return "";}}
		public virtual bool IsValid {get{return false;}}
		public virtual string ValidationErrorMessage{get{return "Invalid reference";}}

		public virtual IEnumerable<string> GetIncludePaths() {
			return new[]{string.Empty};
		}

		public virtual IEnumerable<ParseCache> GetParseCaches() {
			return new[]{new ParseCache()};
		}

		public DProjectReference (AbstractDProject Owner, ReferenceType refType)
		{
			OwnerProject = Owner;
			ReferenceType = refType;
		}

		protected void PropChanged(string n)
		{
			if(PropertyChanged!=null)
				PropertyChanged(this, new PropertyChangedEventArgs(n));
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}

