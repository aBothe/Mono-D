using System.Linq;

using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.D.Formatting;

namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DGlobalOptions : Gtk.Bin
	{
		public DGlobalOptions ()
		{
			this.Build ();			
		}
	
		public void Load ()
		{
			text_ManualBaseUrl.Text = D.Refactoring.DDocumentationLauncher.DigitalMarsUrl;
			check_EnableUFCSCompletion.Active = DCompilerService.Instance.CompletionOptions.ShowUFCSItems;
			check_ShowFunctionParams.Active = DCompilerService.Instance.Outline.ShowFuncParams;
			check_ShowFunctionVariables.Active = DCompilerService.Instance.Outline.ShowFuncVariables;
			check_ShowTypes.Active = DCompilerService.Instance.Outline.ShowTypes;
			check_GrayOutNonPublic.Active = DCompilerService.Instance.Outline.GrayOutNonPublic;
			check_ExpandAll.Active = DCompilerService.Instance.Outline.ExpandAll;
			check_IndentInsteadFormatCode.Active = DCodeFormatter.IndentCorrectionOnly;
		}

		public bool Validate ()
		{
			return !string.IsNullOrWhiteSpace (text_ManualBaseUrl.Text);
		}
		
		public bool Store ()
		{
			Refactoring.DDocumentationLauncher.DigitalMarsUrl = text_ManualBaseUrl.Text;

			DCompilerService.Instance.CompletionOptions.ShowUFCSItems = check_EnableUFCSCompletion.Active;
			DCompilerService.Instance.Outline.ShowFuncParams = check_ShowFunctionParams.Active;
			DCompilerService.Instance.Outline.ShowFuncVariables = check_ShowFunctionVariables.Active;
			DCompilerService.Instance.Outline.GrayOutNonPublic = check_GrayOutNonPublic.Active;
			DCompilerService.Instance.Outline.ShowTypes = check_ShowTypes.Active;
			DCompilerService.Instance.Outline.ExpandAll = check_ExpandAll.Active;
			DCodeFormatter.IndentCorrectionOnly = check_IndentInsteadFormatCode.Active;

			if(Ide.IdeApp.Workbench.ActiveDocument!=null)
				Ide.IdeApp.Workbench.ActiveDocument.ReparseDocument();

			return true;
		}
	}
	
	public class DGlobalOptionsBinding : OptionsPanel
	{
		private DGlobalOptions panel;
		
		public override Gtk.Widget CreatePanelWidget ()
		{
			panel = new DGlobalOptions ();
			panel.Load ();
			return panel;
		}

		public override bool ValidateChanges ()
		{
			return panel.Validate ();
		}
			
		public override void ApplyChanges ()
		{
			panel.Store ();
		}
	}
	
}
