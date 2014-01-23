using MonoDevelop.D.Building;
using MonoDevelop.Ide.Gui.Dialogs;

namespace MonoDevelop.D
{
	public partial class ResourcesCompilerOptionsPanel : Gtk.Bin
	{
		public ResourcesCompilerOptionsPanel ()
		{
			this.Build ();
		}
		
		public void Load()
		{
			text_CompilerExecutable.Text=Win32ResourceCompiler.Instance. Executable;
			text_BuildArgs.Text = Win32ResourceCompiler.Instance.Arguments;
		}
		
		public bool Validate()
		{
			return text_CompilerExecutable.Text!="" && text_BuildArgs.Text!="";	
		}
		
		public void Store()
		{
			Win32ResourceCompiler.Instance.Executable = text_CompilerExecutable.Text;
			Win32ResourceCompiler.Instance.Arguments = text_BuildArgs.Text;
		}
	}
	
	public class ResourceCompilerOptionsBinding : OptionsPanel
	{
		private ResourcesCompilerOptionsPanel panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new ResourcesCompilerOptionsPanel ();
			panel.Load();
			return panel;
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

