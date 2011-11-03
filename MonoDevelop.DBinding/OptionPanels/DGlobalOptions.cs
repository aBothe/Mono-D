using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DGlobalOptions : Gtk.Bin
	{
		private Gtk.TreeStore categoryStore;
		private DProjectConfiguration configuration;
		private Dictionary<string,int> tvCategoriesPageIndexMap;
		
		public DGlobalOptions () 
		{
			this.Build ();	
			
			OptionsNotebook.ShowTabs = false;
			
			Gtk.TreeViewColumn defaultTreeColumn = new Gtk.TreeViewColumn ();
			defaultTreeColumn.Title = "Default";
			Gtk.CellRendererText defaultNameCell = new Gtk.CellRendererText (); 
			defaultTreeColumn.PackStart(defaultNameCell, true);	 
			tvCategories.AppendColumn (defaultTreeColumn);			
			defaultTreeColumn.AddAttribute (defaultNameCell, "text", 0);
			
			categoryStore = new Gtk.TreeStore (typeof (string), typeof (string));
			Gtk.TreeIter iter = categoryStore.AppendValues ("General");			
			iter = categoryStore.AppendValues ("Compilers");
			categoryStore.AppendValues (iter, "dmd");	
			categoryStore.AppendValues (iter, "gdc");				
			categoryStore.AppendValues (iter, "ldc");				
	
			tvCategories.Model = categoryStore;		
			tvCategories.Selection.Changed += HandleTvCategoriesSelectionChanged;
			tvCategories.ExpandAll();
			
			
			//register treeview items with pages indexess
			//TODO: figure out how to use non visual identifiers
			tvCategoriesPageIndexMap = new Dictionary<string,int>();
			tvCategoriesPageIndexMap.Add("General", 0);
			tvCategoriesPageIndexMap.Add("Compilers", 1);
			tvCategoriesPageIndexMap.Add("dmd", 2);
			tvCategoriesPageIndexMap.Add("gdc", 3);
			tvCategoriesPageIndexMap.Add("ldc", 4);				
		}

		void HandleTvCategoriesSelectionChanged (object sender, EventArgs e)
		{
        	Gtk.TreeIter iter;
          	Gtk.TreeModel model;
			
			//find the correct page
            if (((Gtk.TreeSelection)sender).GetSelected(out model, out iter))
            {
				string val = (string) model.GetValue (iter, 0);
				if (tvCategoriesPageIndexMap.ContainsKey(val))
					OptionsNotebook.CurrentPage = tvCategoriesPageIndexMap[val];
            }
		}
		
		public void Load (DProjectConfiguration config)
		{
			configuration = config;
			
			
			//DCompiler.Init();
			//DCompiler.Instance
		}


		public bool Validate()
		{
			return true;
		}
		
		public bool Store ()
		{
			if (configuration == null)
				return false;
			
			return true;
		}

	}
	
	public class DGlobalOptionsBinding : OptionsPanel
	{
		private DGlobalOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			return panel = new DGlobalOptions ();
		}

		public override bool ValidateChanges()
		{
			return panel.Validate();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
