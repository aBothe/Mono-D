using System;

namespace MonoDevelop.D.OptionPanels
{
	public partial class DGlobalBuildArgumentOptions : Gtk.Dialog
	{		
		private bool isDebug;
		public DGlobalBuildArgumentOptions ()
		{
			this.Build ();
		}
		
		public bool IsDebug
		{
			get 
			{
				return isDebug;
			} 
			set
			{
				isDebug = value;
				this.Title = (isDebug?"Debug":"Release") + " build arguments";
			}
		}		
	}
}

