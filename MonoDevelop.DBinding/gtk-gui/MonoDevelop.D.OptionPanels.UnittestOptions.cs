
// This file has been generated by the GUI designer. Do not modify.
namespace MonoDevelop.D.OptionPanels
{
	public partial class UnittestOptions
	{
		private global::Gtk.Table table2;
		
		private global::Gtk.Button button83;
		
		private global::Gtk.Button button84;
		
		private global::Gtk.Label label3;
		
		private global::Gtk.Label label4;
		
		private global::Gtk.Entry text_MainCreationFlag;
		
		private global::Gtk.Entry text_UnittestCommand;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget MonoDevelop.D.OptionPanels.UnittestOptions
			global::Stetic.BinContainer.Attach (this);
			this.Name = "MonoDevelop.D.OptionPanels.UnittestOptions";
			// Container child MonoDevelop.D.OptionPanels.UnittestOptions.Gtk.Container+ContainerChild
			this.table2 = new global::Gtk.Table (((uint)(5)), ((uint)(2)), false);
			this.table2.Name = "table2";
			this.table2.RowSpacing = ((uint)(6));
			this.table2.ColumnSpacing = ((uint)(6));
			// Container child table2.Gtk.Table+TableChild
			this.button83 = new global::Gtk.Button ();
			this.button83.CanFocus = true;
			this.button83.Name = "button83";
			this.button83.UseUnderline = true;
			this.button83.Label = global::MonoDevelop.Core.GettextCatalog.GetString ("Reset");
			this.table2.Add (this.button83);
			global::Gtk.Table.TableChild w1 = ((global::Gtk.Table.TableChild)(this.table2 [this.button83]));
			w1.TopAttach = ((uint)(1));
			w1.BottomAttach = ((uint)(2));
			w1.LeftAttach = ((uint)(1));
			w1.RightAttach = ((uint)(2));
			w1.XOptions = ((global::Gtk.AttachOptions)(4));
			w1.YOptions = ((global::Gtk.AttachOptions)(4));
			// Container child table2.Gtk.Table+TableChild
			this.button84 = new global::Gtk.Button ();
			this.button84.CanFocus = true;
			this.button84.Name = "button84";
			this.button84.UseUnderline = true;
			this.button84.Label = global::MonoDevelop.Core.GettextCatalog.GetString ("Reset");
			this.table2.Add (this.button84);
			global::Gtk.Table.TableChild w2 = ((global::Gtk.Table.TableChild)(this.table2 [this.button84]));
			w2.TopAttach = ((uint)(3));
			w2.BottomAttach = ((uint)(4));
			w2.LeftAttach = ((uint)(1));
			w2.RightAttach = ((uint)(2));
			w2.XOptions = ((global::Gtk.AttachOptions)(4));
			w2.YOptions = ((global::Gtk.AttachOptions)(4));
			// Container child table2.Gtk.Table+TableChild
			this.label3 = new global::Gtk.Label ();
			this.label3.Name = "label3";
			this.label3.Xalign = 0F;
			this.label3.LabelProp = global::MonoDevelop.Core.GettextCatalog.GetString ("<b>Unittest Command</b>");
			this.label3.UseMarkup = true;
			this.table2.Add (this.label3);
			global::Gtk.Table.TableChild w3 = ((global::Gtk.Table.TableChild)(this.table2 [this.label3]));
			w3.YOptions = ((global::Gtk.AttachOptions)(4));
			// Container child table2.Gtk.Table+TableChild
			this.label4 = new global::Gtk.Label ();
			this.label4.Name = "label4";
			this.label4.Xalign = 0F;
			this.label4.LabelProp = global::MonoDevelop.Core.GettextCatalog.GetString ("<b>Main Method flag</b> (appended if no main method found in current module)");
			this.label4.UseMarkup = true;
			this.table2.Add (this.label4);
			global::Gtk.Table.TableChild w4 = ((global::Gtk.Table.TableChild)(this.table2 [this.label4]));
			w4.TopAttach = ((uint)(2));
			w4.BottomAttach = ((uint)(3));
			w4.XOptions = ((global::Gtk.AttachOptions)(4));
			w4.YOptions = ((global::Gtk.AttachOptions)(4));
			// Container child table2.Gtk.Table+TableChild
			this.text_MainCreationFlag = new global::Gtk.Entry ();
			this.text_MainCreationFlag.CanFocus = true;
			this.text_MainCreationFlag.Name = "text_MainCreationFlag";
			this.text_MainCreationFlag.IsEditable = true;
			this.text_MainCreationFlag.InvisibleChar = '•';
			this.table2.Add (this.text_MainCreationFlag);
			global::Gtk.Table.TableChild w5 = ((global::Gtk.Table.TableChild)(this.table2 [this.text_MainCreationFlag]));
			w5.TopAttach = ((uint)(3));
			w5.BottomAttach = ((uint)(4));
			w5.XOptions = ((global::Gtk.AttachOptions)(4));
			w5.YOptions = ((global::Gtk.AttachOptions)(4));
			// Container child table2.Gtk.Table+TableChild
			this.text_UnittestCommand = new global::Gtk.Entry ();
			this.text_UnittestCommand.CanFocus = true;
			this.text_UnittestCommand.Name = "text_UnittestCommand";
			this.text_UnittestCommand.IsEditable = true;
			this.text_UnittestCommand.InvisibleChar = '•';
			this.table2.Add (this.text_UnittestCommand);
			global::Gtk.Table.TableChild w6 = ((global::Gtk.Table.TableChild)(this.table2 [this.text_UnittestCommand]));
			w6.TopAttach = ((uint)(1));
			w6.BottomAttach = ((uint)(2));
			w6.YOptions = ((global::Gtk.AttachOptions)(4));
			this.Add (this.table2);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Hide ();
			this.button84.Clicked += new global::System.EventHandler (this.Reset_MainMethodFlag);
			this.button83.Clicked += new global::System.EventHandler (this.Reset_UnittestCmd);
		}
	}
}
