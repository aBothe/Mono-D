
// This file has been generated by the GUI designer. Do not modify.
namespace MonoDevelop.D
{
	public partial class ProjectDependenciesWidget
	{
		private global::Gtk.VBox vbox2;
		private global::Gtk.Label label16;
		private global::Gtk.ScrolledWindow scrolledwindow2;
		private global::Gtk.VBox vbox_ProjectDeps;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget MonoDevelop.D.ProjectDependenciesWidget
			global::Stetic.BinContainer.Attach (this);
			this.Name = "MonoDevelop.D.ProjectDependenciesWidget";
			// Container child MonoDevelop.D.ProjectDependenciesWidget.Gtk.Container+ContainerChild
			this.vbox2 = new global::Gtk.VBox ();
			this.vbox2.Name = "vbox2";
			this.vbox2.Spacing = 6;
			// Container child vbox2.Gtk.Box+BoxChild
			this.label16 = new global::Gtk.Label ();
			this.label16.Name = "label16";
			this.label16.Xalign = 0F;
			this.label16.LabelProp = global::Mono.Unix.Catalog.GetString ("Checking a project in this list will add an include (that points to the respectiv" +
					"e project\'s base directory) to this project automatically.");
			this.label16.Wrap = true;
			this.vbox2.Add (this.label16);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.label16]));
			w1.Position = 0;
			w1.Expand = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.scrolledwindow2 = new global::Gtk.ScrolledWindow ();
			this.scrolledwindow2.CanFocus = true;
			this.scrolledwindow2.Name = "scrolledwindow2";
			// Container child scrolledwindow2.Gtk.Container+ContainerChild
			global::Gtk.Viewport w2 = new global::Gtk.Viewport ();
			w2.ShadowType = ((global::Gtk.ShadowType)(0));
			// Container child GtkViewport.Gtk.Container+ContainerChild
			this.vbox_ProjectDeps = new global::Gtk.VBox ();
			this.vbox_ProjectDeps.Name = "vbox_ProjectDeps";
			this.vbox_ProjectDeps.Spacing = 6;
			w2.Add (this.vbox_ProjectDeps);
			this.scrolledwindow2.Add (w2);
			this.vbox2.Add (this.scrolledwindow2);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.scrolledwindow2]));
			w5.Position = 1;
			this.Add (this.vbox2);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Show ();
		}
	}
}
