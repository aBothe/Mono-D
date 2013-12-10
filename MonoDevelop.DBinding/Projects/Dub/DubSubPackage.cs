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
using System.IO;
using MonoDevelop.Core;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubSubPackage : DubProject
	{
		bool useOriginalBasePath;
		FilePath OriginalBasePath;
		FilePath VirtualBasePath;

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

		public static DubSubPackage ReadAndAdd(DubProject superProject,JsonReader r)
		{
			if (r.TokenType != JsonToken.StartObject)
				throw new JsonReaderException ("Illegal token on subpackage definition beginning");

			var sub = new DubSubPackage ();
			sub.FileName = superProject.FileName;

			sub.OriginalBasePath = superProject is DubSubPackage ? (superProject as DubSubPackage).OriginalBasePath : 
				superProject.BaseDirectory;
			sub.VirtualBasePath = sub.OriginalBasePath;

			sub.BeginLoad ();

			superProject.packagesToAdd.Add(sub);
			
			while (r.Read ()) {
				if (r.TokenType == JsonToken.PropertyName)
					sub.TryPopulateProperty (r.Value as string, r);
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}
				
			sub.packageName = superProject.packageName + ":" + (sub.packageName ?? string.Empty);

			var sourcePaths = sub.GetSourcePaths ().ToArray();
			if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
				sub.VirtualBasePath = new FilePath(sourcePaths [0]);

			foreach(var f in sub.GetItemFiles(false))
				sub.Items.Add(new ProjectFile(f));

			// TODO: What to do with new configurations that were declared in this sub package? Add them to all other packages as well?
			sub.EndLoad ();

			if (r.TokenType != JsonToken.EndObject)
				throw new JsonReaderException ("Illegal token on subpackage definition end");
			return sub;
		}

		protected override bool OnGetCanExecute (MonoDevelop.Projects.ExecutionContext context, MonoDevelop.Projects.ConfigurationSelector configuration)
		{
			return false;
		}

		protected override bool CheckNeedsBuild (MonoDevelop.Projects.ConfigurationSelector configuration)
		{
			return false;
		}

		public override FilePath GetOutputFileName(ConfigurationSelector configuration)
		{
			return new FilePath();
		}

		protected override void DoClean (MonoDevelop.Core.IProgressMonitor monitor, ConfigurationSelector configuration)
		{

		}

		protected override BuildResult OnBuild (MonoDevelop.Core.IProgressMonitor monitor, MonoDevelop.Projects.ConfigurationSelector configuration)
		{
			return new BuildResult { CompilerOutput = "Can't build subpackage. Skipping." };
		}
	}
}

