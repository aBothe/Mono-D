// by Alexander Bothe (info@alexanderbothe.com)
using System;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using Gtk;
using MonoDevelop.D.Refactoring;
using MonoDevelop.Ide;
using MonoDevelop.Refactoring;
using MonoDevelop.Refactoring.Rename;

// Couple of code parts were taken from RenameItemDialog.cs
//TODO: Propose more general rename item dialog structures -- ValidateName, setting the initial entry text etc..

namespace MonoDevelop.D
{
	public partial class DRenameNameDialog : Gtk.Dialog
	{
		DRenameRefactoring rename;
		RefactoringOptions options;

		public DRenameNameDialog (RefactoringOptions options,DRenameRefactoring rename)
		{
			this.rename = rename;
			this.options = options;

			this.Build ();
			var ds = (INode)options.SelectedItem;

			#region Adjust dialog title
			var app = "Renaming ";

			if (ds is DClassLike)
			{
				var dc = (DClassLike)ds;
				app+=dc.ClassType.ToString();
			}
			else if (ds is DMethod)
				app += "method";
			else if (ds is DVariable)
				app += ((DVariable)ds).IsAlias ? "alias" : "variable";
			else
				app += "item";

			Title = app;
			#endregion

			text_NewId.Text = ds.Name;

			buttonPreview.Sensitive = buttonOk.Sensitive = false;

			buttonOk.Clicked += OnOKClicked;
			buttonPreview.Clicked += OnPreviewClicked;
			text_NewId.Changed += delegate { setNotifyIcon(buttonPreview.Sensitive = buttonOk.Sensitive = ValidateName()); };
			ValidateName();
		}

		bool ValidateName()
		{
			return DRenameRefactoring.IsValidIdentifier(text_NewId.Text);
		}

		void setNotifyIcon(bool hasCorrectUserInput)
		{
			img_wrongIdentifierNotification.SetFromIconName(hasCorrectUserInput ? "gtk-apply" : "gtk-cancel", IconSize.LargeToolbar);
		}

		RenameRefactoring.RenameProperties Properties
		{
			get
			{
				return new RenameRefactoring.RenameProperties()
				{
					NewName = text_NewId.Text,
					RenameFile = false //TODO
				};
			}
		}

		void OnOKClicked(object sender, EventArgs e)
		{
			var properties = Properties;
			((Widget)this).Destroy();
			var changes = rename.PerformChanges(options, properties);
			var monitor = IdeApp.Workbench.ProgressMonitors.GetBackgroundProgressMonitor(this.Title, null);
			RefactoringService.AcceptChanges(monitor, changes);
		}

		void OnPreviewClicked(object sender, EventArgs e)
		{
			var properties = Properties;
			((Widget)this).Destroy();
			var changes = rename.PerformChanges(options, properties);
			MessageService.ShowCustomDialog(new RefactoringPreviewDialog(changes));
		}
	}
}

