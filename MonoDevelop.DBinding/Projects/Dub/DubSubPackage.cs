//
// DubSubPackage.cs
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
using Newtonsoft.Json;
using MonoDevelop.Projects;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Core;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubSubPackage : DubProject
	{
		public bool useOriginalBasePath;
		public FilePath OriginalBasePath;
		public FilePath VirtualBasePath;

		protected override FilePath GetDefaultBaseDirectory ()
		{
			return useOriginalBasePath ? OriginalBasePath : VirtualBasePath;
		}

		public override IEnumerable<string> GetSourcePaths (ConfigurationSelector sel)
		{
			useOriginalBasePath = true;
			var en = base.GetSourcePaths (sel).ToList();
			useOriginalBasePath = false;
			return en;
		}

		protected override void DoExecute (IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			useOriginalBasePath = true;
			base.DoExecute (monitor, context, configuration);
			useOriginalBasePath = false;
		}

		protected override BuildResult DoBuild (IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			useOriginalBasePath = true;
			var res = base.DoBuild (monitor, configuration);
			useOriginalBasePath = false;
			return res;
		}
	}
}

