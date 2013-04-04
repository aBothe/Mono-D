using Gtk;
using System;
using System.Collections;

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
			foreach(var prj in Project.ParentSolution.GetAllProjects())
			{
				if (prj == Project)
					continue;
				
				var cb = new Gtk.CheckButton(prj.Name){
					CanFocus=true,
					DrawIndicator=true,
					UseUnderline=false,
					Active = Project.ProjectDependencies.Contains(prj.ItemId)
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
			Project.ProjectDependencies.Clear();
			foreach (var i in vbox_ProjectDeps)
			{
				var cb = i as CheckButton;
				
				if (cb == null || !cb.Active)
					continue;
				
				var prj = cb.Data["prj"] as DProject;
				if(prj!=null)
					Project.ProjectDependencies.Add(prj.ItemId);
			}
		}
	}
}

