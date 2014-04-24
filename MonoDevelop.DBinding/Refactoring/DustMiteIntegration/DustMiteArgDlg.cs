//
// DustMiteArgDlg.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using MonoDevelop.D.Projects;
using MonoDevelop.Core;
using System.Diagnostics;
using System.IO;
using MonoDevelop.Ide;
using MonoDevelop.D.Building;
using System.Collections.Generic;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Refactoring
{
	public partial class DustMiteArgDlg : Gtk.Dialog
	{
		#region Properties
		public readonly AbstractDProject Project;
		Process dustmiteProcess;

		const string DustMiteCommandLinePropId = "DustMiteCommandLine";
		public string DustMiteCommandLine 
		{
			get{
				var s = Project.ExtendedProperties [DustMiteCommandLinePropId] as string;
				if (!string.IsNullOrWhiteSpace(s))
					return s;

				return "dustmite "+Project.BaseDirectory.FileName+" '$buildCmd 2>&1'";
			}
			set{ Project.ExtendedProperties [DustMiteCommandLinePropId] = value; }
		}

		bool RunButtonActive
		{
			set{ 
				bn_Stop.Sensitive = !value;
				bn_Run.Sensitive = value;
			}
		}
		#endregion

		public DustMiteArgDlg(IntPtr raw) : base(raw) {}

		public DustMiteArgDlg (AbstractDProject prj)
		{
			this.Project = prj;
			this.Build ();
			ResetDustmiteCmd ();
			ResetBuildCommand ();
		}

		public void ResetDustmiteCmd()
		{
			tb_CommandLine.Text = DustMiteCommandLine;
		}

		public void ResetBuildCommand()
		{
			var dprj = Project as DProject;
			if(dprj != null)
			{
				var origBasePath = dprj.BaseDirectory;
				dprj.BaseDirectory = origBasePath.ParentDirectory;
				var builtObjects = new List<string> ();
				foreach (var pf in Project.Files) {
					if (pf.BuildAction != BuildAction.Compile || pf.Subtype == Subtype.Directory)
						continue;

					// Screw .rc or other files for now

					builtObjects.Add (ProjectBuilder.MakeRelativeToPrjBase(dprj,pf.FilePath));
				}

				tb_BuildCmd.Buffer.Text = "\"" + System.IO.Path.Combine(dprj.Compiler.BinPath, dprj.Compiler.SourceCompilerCommand) + "\" " + ProjectBuilder.BuildOneStepBuildString (dprj, builtObjects, IdeApp.Workspace.ActiveConfiguration);
				dprj.BaseDirectory = origBasePath;
			}
		}

		public void StopExecution()
		{
			RunButtonActive = true;
			if (dustmiteProcess == null || dustmiteProcess.HasExited)
				return;

			dustmiteProcess.Kill ();
			dustmiteProcess.Dispose ();
			dustmiteProcess = null;
		}

		class DustmiteArgProvider : IArgumentMacroProvider
		{
			string buildCmd;

			public DustmiteArgProvider(string buildCmd) { this.buildCmd = buildCmd; }
			public void ManipulateMacros (Dictionary<string, string> macros)
			{
				macros ["buildCmd"] = buildCmd;
			}
		}

		public void RunDustmite()
		{
			StopExecution ();
			tb_Log.Buffer.Clear ();

			var cmd = tb_CommandLine.Text = tb_CommandLine.Text.Trim ();
			cmd = ProjectBuilder.FillInMacros (cmd, new DustmiteArgProvider (tb_BuildCmd.Buffer.Text.Trim ()));

			if (string.IsNullOrEmpty (cmd)) {
				MessageService.ShowError ("Please enter a command that shall become executed");
				return;
			}

			int i;

			if (cmd [0] == '"')
				i = cmd.IndexOf ("\"", 1);
			else
				i = cmd.IndexOf (" ");

			if (i <= 0) {
				MessageService.ShowError ("No dustmite executable given");
				return;
			}

			var psi = new ProcessStartInfo(cmd.Substring (0, i++), cmd.Substring(i));
			psi.UseShellExecute = false;
			psi.RedirectStandardError = true;
			psi.RedirectStandardOutput = true;
			psi.CreateNoWindow = true;
			psi.WorkingDirectory = Project.BaseDirectory.ParentDirectory;

			dustmiteProcess = new Process { StartInfo = psi };

			dustmiteProcess.OutputDataReceived += (sender, e) => AddToLog(e.Data);
			dustmiteProcess.ErrorDataReceived += (sender, e) => AddToLog(e.Data);
			dustmiteProcess.Exited += (sender, e) => {
				RunButtonActive = true;
				AddToLog("Process exited with code "+dustmiteProcess.ExitCode);
			};

			RunButtonActive = false;
			try{
				dustmiteProcess.Start();
			}
			catch(Exception ex) {
				RunButtonActive = true;
				MessageService.ShowException (ex, "Couldn't launch process");
				return;
			}


		}

		void AddToLog(string s)
		{
			DispatchService.GuiDispatch(()=>
				tb_Log.Buffer.Text = tb_Log.Buffer.Text + "\n" + s);
		}

		protected override void OnClose ()
		{
			StopExecution ();
			DustMiteCommandLine = tb_CommandLine.Text;
			base.OnClose ();
		}

		public override void Dispose ()
		{
			base.Dispose ();
		}

		protected void OnBnStopClicked (object sender, EventArgs e)
		{
			StopExecution ();
		}

		protected void bn_RunClick (object sender, EventArgs e)
		{
			RunDustmite ();
		}

		protected void bn_OpenTargetFolderClick (object sender, EventArgs e)
		{
			var dir = Project.BaseDirectory.ParentDirectory.Combine (Project.BaseDirectory.FileName + ".reduced");
			if(Directory.Exists(dir))
				Process.Start (dir);
		}

		protected void OnBnResetCmdClicked (object sender, EventArgs e)
		{
			ResetDustmiteCmd ();
		}

		protected void OnBnBuildCmdResetClicked (object sender, EventArgs e)
		{
			ResetBuildCommand ();
		}

		protected void OnBnCloseClicked (object sender, EventArgs e)
		{
			base.Destroy ();
			base.Dispose ();
		}
	}
}

