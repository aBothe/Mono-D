//
// GtkDesignInfo.cs
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
using MonoDevelop.D.Projects;
using MonoDevelop.GtkCore;
using MonoDevelop.Projects;
using MonoDevelop.Ide;
using System.IO;
using MonoDevelop.Core.Serialization;

namespace MonoDevelop.D.GuiBuilder
{
	public class GtkDesignInfo: IDisposable
	{
		Project project;
		GuiBuilderProject builderProject;
		ProjectResourceProvider resourceProvider;
		ReferenceManager referenceManager;

		[ItemProperty (DefaultValue=true)]
		bool generateGettext = true;

		[ItemProperty (DefaultValue="Mono.Unix.Catalog")]
		string gettextClass = "Mono.Unix.Catalog";

		[ItemProperty (DefaultValue="Gdk.Pixbuf")]
		string imageResourceLoaderClass = "Gdk.Pixbuf";

		GtkDesignInfo ()
		{
		}

		GtkDesignInfo (Project project)
		{
			project.ExtendedProperties ["GtkDesignInfo"] = this;
			Project = project;
		}

		public virtual Project Project {
			get { return project; }
			set {
				if (project == value)
					return;

				if (project != null) {
					/*project.FileAddedToProject -= OnFileEvent;
					project.FileChangedInProject -= OnFileEvent;
					project.FileRemovedFromProject -= OnFileEvent;
					*/
					if (referenceManager != null)
						referenceManager.Dispose ();
					referenceManager = null;
				}
				project = value;
				/*if (project != null) {
					project.FileAddedToProject += OnFileEvent;
					project.FileChangedInProject += OnFileEvent;
					project.FileRemovedFromProject += OnFileEvent;
				}*/
			}
		}
		/*
		void OnFileEvent (object o, ProjectFileEventArgs args)
		{
			if (!IdeApp.IsInitialized || !IdeApp.Workspace.IsOpen || !File.Exists (ObjectsFile))
				return;

			UpdateObjectsFile ();
		}*/

		public void Dispose ()
		{
			if (resourceProvider != null)
				System.Runtime.Remoting.RemotingServices.Disconnect (resourceProvider);
			resourceProvider = null;
			if (builderProject != null)
				builderProject.Dispose ();
			builderProject = null;
			if (referenceManager != null)
				referenceManager.Dispose ();
			referenceManager = null;
			Project = null;
		}

		public GuiBuilderProject GuiBuilderProject {
			get {/*
				if (builderProject == null) {
					if (SupportsDesigner (project)) {
						if (!File.Exists (SteticFile)) {
							UpdateGtkFolder ();
							ProjectNodeBuilder.OnSupportChanged (project);
						}
						builderProject = GuiBuilderService.CreateBuilderProject (project, SteticFile);
					} else
						builderProject = GuiBuilderService.CreateBuilderProject (project, null);
				}*/
				return builderProject;
			}
		}

		public void ReloadGuiBuilderProject ()
		{
			if (builderProject != null)
				builderProject.Reload ();
		}

		public ProjectResourceProvider ResourceProvider {
			get {
				if (resourceProvider == null) {
					resourceProvider = new ProjectResourceProvider (project);
					System.Runtime.Remoting.RemotingServices.Marshal (resourceProvider, null, typeof(Stetic.IResourceProvider));
				}
				return resourceProvider;
			}
		}

		public bool GenerateGettext {
			get { return generateGettext; }
			set {
				generateGettext = value;
				// Set to default value if gettext is not enabled
				if (!generateGettext) 
					gettextClass = "Mono.Unix.Catalog";
			}
		}

		public string GettextClass {
			get { return gettextClass; }
			set { gettextClass = value; }
		}

		public string ImageResourceLoaderClass {
			get { return imageResourceLoaderClass; }
			set { imageResourceLoaderClass = value; }
		}

		public static bool SupportsDesigner (Project project)
		{
			if (!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("DISABLE_STETIC"))) {
				return false;
			}

			return true;
		}

		public void ForceCodeGenerationOnBuild ()
		{
			if (!SupportsDesigner (project))
				return;
			/*try {
				FileInfo fi = new FileInfo (SteticFile);
				fi.LastWriteTime = DateTime.Now;
			} catch {
				// Ignore errors here
			}*/
		}

		public static void DisableProject (Project project)
		{
			GtkDesignInfo info = FromProject (project);
			project.ExtendedProperties.Remove ("GtkDesignInfo");
			info.Dispose ();
			//ProjectNodeBuilder.OnSupportChanged (project);
		}

		public static GtkDesignInfo FromProject (Project project)
		{
			if (project == null)
				return null;

			var info = project.ExtendedProperties ["GtkDesignInfo"] as GtkDesignInfo;
			if (info == null)
				info = new GtkDesignInfo (project);
			else
				info.Project = project;
			return info;
		}
	}	
}

