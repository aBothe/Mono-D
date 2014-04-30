//
// DPrjRefNode.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
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
using MonoDevelop.Ide.Gui.Components;
using System.Collections.Generic;
using MonoDevelop.D.Projects;
using MonoDevelop.Projects;
using MonoDevelop.Ide;
using System.ComponentModel;

namespace MonoDevelop.D.Projects.ProjectPad
{
	class DProjectReferenceNodeBuilder: TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof(DProjectReference); }
		}

		public override Type CommandHandlerType {
			get { return typeof(DProjectReferenceNodeCommandHandler); }
		}

		public override bool UseReferenceEquality {
			get {
				// ProjectReference is not immutable, so we can't rely on
				// object equality for comparing instances in the tree.
				// We have to use reference equality
				return true;
			}
		}

		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return ((DProjectReference)dataObject).Name;
		}

		public override void BuildNode(ITreeBuilder treeBuilder, object dataObject, NodeInfo n)
		{
			var pref = (DProjectReference)dataObject;
			switch (pref.ReferenceType)
			{
				case ReferenceType.Project:
					n.Icon = Context.GetIcon("md-reference-project");
					break;
				case ReferenceType.Package:
					n.Icon = Context.GetIcon("md-reference-folder");
					break;
				/*
			case ReferenceType.Assembly:
				label = Path.GetFileName(pref.Reference);
				icon = Context.GetIcon ("md-reference-folder");
				break;
				default:
				label = pref.Reference;
				icon = Context.GetIcon (Stock.Reference);
				break;*/
			}

			n.Label = GLib.Markup.EscapeText(pref.Name);

			if (!pref.IsValid)
			{
				n.Label = "<span color='red'>" + n.Label + "</span>";
				n.Icon = Context.GetIcon("md-reference-warning");
			}
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return !(dataObject as DProjectReference).IsValid;
		}

		public override void BuildChildNodes (ITreeBuilder treeBuilder, object dataObject)
		{
			var pref = (DProjectReference) dataObject;
			if (!pref.IsValid)
				treeBuilder.AddChild (new TreeViewItem (pref.ValidationErrorMessage, Gtk.Stock.DialogWarning));
		}

		public override void OnNodeAdded (object dataObject)
		{
			var pref = (DProjectReference) dataObject;
			pref.PropertyChanged += ReferenceStatusChanged;
		}

		public override void OnNodeRemoved (object dataObject)
		{
			var pref = (DProjectReference) dataObject;
			pref.PropertyChanged -= ReferenceStatusChanged;
		}

		void ReferenceStatusChanged (object sender, PropertyChangedEventArgs e)
		{
			var tb = Context.GetTreeBuilder (sender);
			if (tb != null)
				tb.UpdateAll ();
		}
	}

	class DProjectReferenceNodeCommandHandler: NodeCommandHandler
	{
		public override void ActivateItem ()
		{
			var pref = CurrentNode.DataItem as DProjectReference;
			/*if (pref != null) {
				foreach (string fileName in pref.GetReferencedFileNames (IdeApp.Workspace.ActiveConfiguration))
					IdeApp.Workbench.OpenDocument (fileName);
			}*/
		}

		public override bool CanDeleteItem ()
		{
			var pref = CurrentNode.DataItem as DProjectReference;
			return pref.OwnerProject.References.CanDelete;
		}

		public override void DeleteMultipleItems ()
		{
			var projects = new Dictionary<AbstractDProject,AbstractDProject> ();
			foreach (ITreeNavigator nav in CurrentNodes) {
				var pref = (DProjectReference) nav.DataItem;
				var project = nav.GetParentDataItem (typeof(AbstractDProject), false) as AbstractDProject;

				switch (pref.ReferenceType) {
					case ReferenceType.Package:
						project.References.DeleteInclude (pref.Reference);
						break;
					case ReferenceType.Project:
						project.References.DeleteProjectRef (pref.Reference);
						break;
					default:
						throw new InvalidOperationException ("Invalid removal operation");
				}

				projects [project] = project;
			}
			foreach (Project p in projects.Values)
				IdeApp.ProjectOperations.Save (p);
		}

		/*[CommandHandler (ProjectCommands.LocalCopyReference)]
		[AllowMultiSelection]
		public void ChangeLocalReference ()
		{
			var projects = new Dictionary<Project,Project> ();
			DProjectReference firstRef = null;
			foreach (ITreeNavigator node in CurrentNodes) {
				var pref = (DProjectReference) node.DataItem;
				if (!pref.CanSetLocalCopy)
					continue;
				if (firstRef == null) {
					firstRef = pref;
					pref.LocalCopy = !pref.LocalCopy;
				} else
					pref.LocalCopy = firstRef.LocalCopy;
				var project = node.GetParentDataItem (typeof(Project), false) as Project;
				projects [project] = project;
			}
			foreach (Project p in projects.Values)
				IdeApp.ProjectOperations.Save (p);
		}

		[CommandUpdateHandler (ProjectCommands.LocalCopyReference)]
		public void UpdateLocalReference (CommandInfo info)
		{
			ProjectReference lastRef = null;
			foreach (ITreeNavigator node in CurrentNodes) {
				var pref = (ProjectReference) node.DataItem;
				if (!pref.CanSetLocalCopy)
					info.Enabled = false;
				if (lastRef == null || lastRef.LocalCopy == pref.LocalCopy) {
					lastRef = pref;
					info.Checked = info.Enabled && pref.LocalCopy;
				} else {
					info.CheckedInconsistent = true;
				}
			}
		}*/

		public override DragOperation CanDragNode ()
		{
			return DragOperation.Copy;
		}
	}
}

