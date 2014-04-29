using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.D.Formatting;
using D_Parser.Misc;
using MonoDevelop.Core;

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
			var outline = DCompilerService.Instance.Outline; // Access the compiler options once to ensure that all settings are loaded - including the CompletionOptions!!

			check_EnableUFCSCompletion.Active = CompletionOptions.Instance.ShowUFCSItems;
			check_EnableMixinAnalysis.Active = !CompletionOptions.Instance.DisableMixinAnalysis;
			check_EnableSuggestionMode.Active = CompletionOptions.Instance.EnableSuggestionMode;
			check_HideDeprecatedItems.Active = CompletionOptions.Instance.HideDeprecatedNodes;
			check_HideDisabledItems.Active = CompletionOptions.Instance.HideDisabledNodes;
			check_EnableDiffbasedColoring.Active = Highlighting.DiffbasedHighlighting.Enabled;
			check_ShowStructMembersInStructInitOnly.Active = CompletionOptions.Instance.ShowStructMembersInStructInitOnly;
			text_CompletionTimeout.Text = CompletionOptions.Instance.CompletionTimeout.ToString ();

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
			int i;
			if (!int.TryParse (text_CompletionTimeout.Text, out i))
				return false;

			return true;
		}

		public bool Store ()
		{
			CompletionOptions.Instance.ShowUFCSItems = check_EnableUFCSCompletion.Active;
			CompletionOptions.Instance.DisableMixinAnalysis = !check_EnableMixinAnalysis.Active;
			CompletionOptions.Instance.EnableSuggestionMode = check_EnableSuggestionMode.Active;
			CompletionOptions.Instance.HideDeprecatedNodes = check_HideDeprecatedItems.Active;
			CompletionOptions.Instance.HideDisabledNodes = check_HideDisabledItems.Active;
			Highlighting.DiffbasedHighlighting.Enabled = check_EnableDiffbasedColoring.Active;
			CompletionOptions.Instance.ShowStructMembersInStructInitOnly = check_ShowStructMembersInStructInitOnly.Active;
			int.TryParse (text_CompletionTimeout.Text,out CompletionOptions.Instance.CompletionTimeout); 

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

			if (Ide.IdeApp.Workbench.ActiveDocument != null)
				Ide.IdeApp.Workbench.ActiveDocument.ReparseDocument ();

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
