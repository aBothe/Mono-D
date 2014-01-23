using MonoDevelop.Ide.Gui.Dialogs;
using Gtk;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.OptionsPanels
{
	public class ProjectIncludesWidgetBinding : MultiConfigItemOptionsPanel
	{
		ProjectIncludesWidget w;
		
		public override Widget CreatePanelWidget ()
		{
			if(w==null)
				w = new ProjectIncludesWidget(ConfiguredProject as DProject, CurrentConfiguration as DProjectConfiguration);
			
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

