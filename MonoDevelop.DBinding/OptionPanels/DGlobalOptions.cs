using System.Linq;

using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.D.Formatting;
using D_Parser.Misc;

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
			check_EnableUFCSCompletion.Active = CompletionOptions.Instance.ShowUFCSItems;
			check_EnableMixinAnalysis.Active = !CompletionOptions.Instance.DisableMixinAnalysis;

			var outline = DCompilerService.Instance.Outline;
			check_ShowFunctionParams.Active = outline.ShowFuncParams;
			check_ShowFunctionVariables.Active = outline.ShowFuncVariables;
			check_ShowTypes.Active = outline.ShowBaseTypes;
			check_GrayOutNonPublic.Active = outline.GrayOutNonPublic;
			switch (outline.ExpansionBehaviour) {
			case DocOutlineCollapseBehaviour.CollapseAll:
				combo_ExpansionBehaviour.Active = 0;
				break;
			case DocOutlineCollapseBehaviour.ReopenPreviouslyExpanded:
				combo_ExpansionBehaviour.Active = 1;
				break;
			case DocOutlineCollapseBehaviour.ExpandAll:
				combo_ExpansionBehaviour.Active = 2;
				break;
			}

			check_IndentInsteadFormatCode.Active = DCodeFormatter.IndentCorrectionOnly;
		}

		public bool Validate ()
		{
			return !string.IsNullOrWhiteSpace (text_ManualBaseUrl.Text);
		}
		
		public bool Store ()
		{
			Refactoring.DDocumentationLauncher.DigitalMarsUrl = text_ManualBaseUrl.Text;

			CompletionOptions.Instance.ShowUFCSItems = check_EnableUFCSCompletion.Active;
			CompletionOptions.Instance.DisableMixinAnalysis = !check_EnableMixinAnalysis.Active;

			var outline = DCompilerService.Instance.Outline;
			outline.ShowFuncParams = check_ShowFunctionParams.Active;
			outline.ShowFuncVariables = check_ShowFunctionVariables.Active;
			outline.GrayOutNonPublic = check_GrayOutNonPublic.Active;
			outline.ShowBaseTypes = check_ShowTypes.Active;
			switch (combo_ExpansionBehaviour.Active) {
			case 0:
				outline.ExpansionBehaviour = DocOutlineCollapseBehaviour.CollapseAll;
				break;
			case 1:
				outline.ExpansionBehaviour = DocOutlineCollapseBehaviour.ReopenPreviouslyExpanded;
				break;
			case 2:
				outline.ExpansionBehaviour = DocOutlineCollapseBehaviour.ExpandAll;
				break;
			}

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
