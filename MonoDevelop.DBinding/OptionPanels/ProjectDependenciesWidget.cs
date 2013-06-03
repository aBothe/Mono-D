using Gtk;
using MonoDevelop.D.Projects;
using System;
using System.Collections;
using System.Collections.Generic;
using MonoDevelop.Projects;

namespace MonoDevelop.D
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class ProjectDependenciesWidget : Gtk.Bin
	{
		public readonly DProject Project;
		public readonly DProjectConfiguration CurrentConfig;
		public ProjectDependenciesWidget (DProject prj, DProjectConfiguration cfg)
		{
			this.Build ();
			Show();

			Project = prj;
			CurrentConfig = cfg;
		}

		public void Load()
		{
			// Remove old children list
			var depsChildren = ((ArrayList)vbox_ProjectDeps.AllChildren);
			for (int k = depsChildren.Count - 1; k >= 0; k--)
				vbox_ProjectDeps.Remove((Widget)depsChildren[k]);
			
			// Init new project dep list
			int i = 0;
			var refs_ = Project.References.ReferencedProjectIds;
			var refs = refs_ as IList<string> ?? new List<string>(refs_);

			foreach(var prj in Project.ParentSolution.GetAllProjects())
			{
				if (prj == Project)
					continue;
				
				var cb = new Gtk.CheckButton(prj.Name){
					CanFocus=true,
					DrawIndicator=true,
					UseUnderline=false,
					Active = refs.Contains(prj.ItemId)
				};
				
				cb.Data.Add("prj", prj);
				
				vbox_ProjectDeps.Add(cb);
				
				var bc=(Box.BoxChild)vbox_ProjectDeps[cb];
				bc.Expand=false;
				bc.Fill=false;
				bc.Position=i++;
			}
			vbox_ProjectDeps.ShowAll();
		}

		public void Store()
		{
			var tbl = new List<string>(Project.References.ReferencedProjectIds);
			var refs = Project.References as DProject.DefaultReferenceCollection;

			foreach (var i in vbox_ProjectDeps)
			{
				var cb = i as CheckButton;
				
				if (cb == null)
					continue;

				var prj = cb.Data["prj"] as DProject;
				if (prj == null)
					continue;

				var id = prj.ItemId;

				if (cb.Active) {
					if (!tbl.Contains (id))
						refs.ProjectDependencies.Add(id);
					else
						tbl.Remove (id);
				} else {
					if (tbl.Contains (id)) {
						refs.ProjectDependencies.Remove (id);
						tbl.Remove (id);
					}
				}
			}

			foreach (var id in tbl)
				refs.ProjectDependencies.Remove (id);
		}
	}
}

