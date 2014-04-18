//
// GuiBuilderDisplayBinding.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
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
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects;
using D_Parser.Misc;
using D_Parser.Dom;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.D.GuiBuilder
{
	/// <summary>
	/// Puts GuiBuilderViews next to the normal editor
	/// </summary>
	public class GuiBuilderDisplayBinding : IViewDisplayBinding
	{
		bool excludeThis = false;

		public string Name {
			get { return MonoDevelop.Core.GettextCatalog.GetString ("Window Designer"); }
		}

		public bool CanUseAsDefault {
			get { return true; }
		}

		public GuiBuilderDisplayBinding ()
		{
		}

		public virtual IViewContent CreateContent (FilePath fileName, string mimeType, Project ownerProject)
		{
			excludeThis = true;
			var db = DisplayBindingService.GetDefaultViewBinding (fileName, mimeType, ownerProject);
			var content = db.CreateContent (fileName, mimeType, ownerProject);
			var view = new GuiBuilderView (content, GetWindow (fileName));
			excludeThis = false;
			return view;
		}

		public virtual bool CanHandle (FilePath fileName, string mimeType, Project ownerProject)
		{
			if (excludeThis)
				return false;

			var dprj = ownerProject as AbstractDProject;
			if (dprj == null)
				return false;

			var mod = GlobalParseCache.GetModule (fileName.ToString ());

			if (GetGtkDMainClass (mod) == null)
				return false;

			excludeThis = true;
			var db = DisplayBindingService.GetDefaultViewBinding (fileName, mimeType, ownerProject);
			excludeThis = false;
			return db != null;
		}

		public const string BuildGuiMethodId = "buildGui";
		public static readonly int BuildGuiMethodIdHash = BuildGuiMethodId.GetHashCode();

		public static DClassLike GetGtkDMainClass(DModule m)
		{
			if (m == null)
				return null;

			foreach (var n in m) {
				var dc = n as DClassLike;
				if (dc != null && dc.BaseClasses != null) {
					var chs = dc [BuildGuiMethodIdHash];
					if (chs != null)
						foreach (var ch in chs)
							if (ch is DMethod)
								return dc;
				}
			}

			return null;
		}

		internal static GuiBuilderWindow GetWindow (string file, Project project = null)
		{
			if (!IdeApp.Workspace.IsOpen)
				return null;

			if(project == null)
				foreach (Project p in IdeApp.Workspace.GetAllProjects ()) {
					if (p.IsFileInProject (file)) {
						project = p;
						break;
					}
				}
			/*
			var info = GtkDesignInfo.FromProject (project);
			if (info == null)
				return null;
			var doc = TypeSystemService.ParseFile (project, file);
			if (doc == null)
				return null;


			foreach (var t in doc) {
				var win = info.GuiBuilderProject.GetWindowForClass (t.FullName);
				if (win != null)
					return win;
			}*/
			return null;
		}
	}
}

