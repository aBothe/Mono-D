using System;
using MonoDevelop.Ide.Gui;
using Gtk;
using System.IO;
using System.Collections.Generic;
using MonoDevelop.D.Highlighting;
using Mono.TextEditor;
using System.Text;

namespace MonoDevelop.D
{
	public class CodeCoverageView : SourceEditor.SourceEditorView, IAttachableViewContent
	{
		class CoverageDictionary : Dictionary<int,int> {}

		#region Properties
		readonly string file;
		readonly MonoDevelop.Ide.Gui.Document origDoc;
		DateTime lastLstFileWriteAccess;
		/// <summary>
		/// Line, Amount
		/// </summary>
		readonly CoverageDictionary coverage = new CoverageDictionary();

		public override string ContentName {
			get {	return "Code Coverage";		}
			set {}
		}

		public override string TabPageLabel {			get {	return MonoDevelop.Core.GettextCatalog.GetString("Code Coverage");	}		}

		public override bool IsReadOnly {			get {				return true;			}		}
		public override bool IsFile {	get {	return true;	}	}
		#endregion

		public CodeCoverageView (MonoDevelop.Ide.Gui.Document doc)
		{
			this.origDoc = doc;
			file = doc.FileName;
			if(doc.HasProject)
				Project = doc.Project;
		}

		public override void Load (string fileName)
		{
			//base.Load (fileName);
		}

		public void Selected ()
		{
			if (!TryReloadCoverageFile ())
				return;

			var sourceEditor = origDoc.GetContent <MonoDevelop.SourceEditor.SourceEditorView> ();
			if (sourceEditor != null) {
				try {
				TextEditor.Caret.Location = sourceEditor.TextEditor.Caret.Location;
				TextEditor.VAdjustment.Value = sourceEditor.TextEditor.VAdjustment.Value;
				} catch {
				}
			}
		}

		public void Deselected ()
		{
			var sourceEditor = origDoc.GetContent <MonoDevelop.SourceEditor.SourceEditorView> ();
			if (sourceEditor != null) {
				try {
				sourceEditor.TextEditor.Caret.Location = TextEditor.Caret.Location;
				sourceEditor.TextEditor.VAdjustment.Value = TextEditor.VAdjustment.Value;
				} catch {
				}
			}
		}

		public new void BeforeSave () { }

		public void BaseContentChanged ()
		{
			
		}

		public string SearchCovFile()
		{
			var lst = Path.ChangeExtension (file, ".lst");
			var f = lst;

			if (File.Exists (f))
				return f;

			if (Project == null)
				return null;




			var fileName = Project.GetRelativeChildPath (f).ToString ().Replace(Path.DirectorySeparatorChar, '-');

			f = Project.BaseDirectory.Combine (fileName);
			if (File.Exists (f))
				return f;

			f = Project.GetOutputFileName (Ide.IdeApp.Workspace.ActiveConfiguration).ParentDirectory.Combine(fileName);
			if (File.Exists (f))
				return f;



			var execProject = Project.ParentSolution.StartupItem as MonoDevelop.Projects.Project;
			if (execProject == null)
				return null;

			f = execProject.BaseDirectory.Combine (fileName);
			if (File.Exists (f))
				return f;

			f = execProject.GetOutputFileName (Ide.IdeApp.Workspace.ActiveConfiguration).ParentDirectory.Combine(fileName);
			if (File.Exists (f))
				return f;



			fileName = execProject.GetRelativeChildPath (lst).ToString ().Replace(Path.DirectorySeparatorChar, '-');
			f = execProject.BaseDirectory.Combine (fileName);
			if (File.Exists (f))
				return f;

			f = execProject.GetOutputFileName (Ide.IdeApp.Workspace.ActiveConfiguration).ParentDirectory.Combine(fileName);
			if (File.Exists (f))
				return f;

			return null;
		}

		public bool TryReloadCoverageFile()
		{
			this.TextEditor.Document.ReadOnly = true;
			this.Document.ReadOnlyCheckDelegate = ((int _) => false);
			TextEditor.Document.MimeType = "text/x-d";


			var lstFile = SearchCovFile();

			if (lstFile == null) {
				coverage.Clear ();
				RefreshCoverageViewData ();

				Document.Text = "// No matching .lst-file found!\n// Build & execute your project with e.g. dmd's `-cov` flag set!\n";
				return false;
			}

			if (lastLstFileWriteAccess == (lastLstFileWriteAccess = File.GetLastWriteTimeUtc (lstFile)))
				return true;

			var sourceFileContent = new StringBuilder ();

			coverage.Clear ();

			string s;
			int line = 0;
			int count;
			using(var fs = File.OpenText(lstFile))
				while((s = fs.ReadLine()) != null)
				{
					line++;
					var pipe = s.IndexOf ('|');
					if (pipe < 1) {
						if (!string.IsNullOrWhiteSpace (s))
							sourceFileContent.AppendLine ("// "+s);
						break;
					}

					if (int.TryParse (s.Substring(0, pipe), System.Globalization.NumberStyles.AllowLeadingWhite | System.Globalization.NumberStyles.Integer, null, out count))
						coverage [line] = count;
					
					sourceFileContent.AppendLine(s.Substring(pipe+1));
				}

			sourceFileContent.AppendLine ();
			sourceFileContent.AppendLine ("// end of "+lstFile);

			Document.Text = sourceFileContent.ToString();

			RefreshCoverageViewData ();
			return true;
		}

		readonly List<TextLineMarker> lastMarkers = new List<TextLineMarker> ();
		void RefreshCoverageViewData()
		{
			foreach (var marker in lastMarkers)
				Document.RemoveMarker (marker, false);
			lastMarkers.Clear ();

			var red = new Cairo.Color (1, 0, 0, 0.4);
			var green = new Cairo.Color (0, 200, 0, 0.4);

			foreach (var kv in coverage) {
				var mk = new LineBackgroundMarker ( kv.Value == 0 ? red : green);
				Document.AddMarker (Document.GetLine(kv.Key), mk, false);
				lastMarkers.Add (mk);
			}

			TextEditor.TextViewMargin.PurgeLayoutCache();
			TextEditor.Parent.QueueDraw();
		}
	}
}

