using System;
using MonoDevelop.Ide.Gui.Dialogs;
using Gtk;

namespace MonoDevelop.D.OptionsPanels
{
	public class ProjectDependencyPanelBinding : MultiConfigItemOptionsPanel
	{
		ProjectDependenciesWidget w;
		
		public override Widget CreatePanelWidget ()
		{
			if(w==null)
				w = new ProjectDependenciesWidget(ConfiguredProject as DProject, CurrentConfiguration as DProjectConfiguration);

			return w;
		}

		public override void ApplyChanges ()
		{
			w.Store();
		}
		
		public override void LoadConfigData ()
		{
			w.Load();
		}
	}
}

