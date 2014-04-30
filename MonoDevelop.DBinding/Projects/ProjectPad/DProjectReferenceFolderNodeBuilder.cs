//
// DProjectReferenceFolderNodeBuilder.cs
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

using MonoDevelop.Ide.Gui.Components;
using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Ide;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Commands;

namespace MonoDevelop.D.Projects.ProjectPad
{
	public class DProjectReferenceFolderNodeBuilder: TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof(DProjectReferenceCollection); }
		}

		public override Type CommandHandlerType {
			get { return typeof(ProjectReferenceFolderNodeCommandHandler); }
		}

		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return "References";
		}

		public override void BuildNode(ITreeBuilder treeBuilder, object dataObject, NodeInfo n)
		{
			n.Label = GettextCatalog.GetString("References");
			n.Icon = Context.GetIcon(Stock.OpenReferenceFolder.Name);
			n.ClosedIcon = Context.GetIcon(Stock.ClosedReferenceFolder.Name);
			//base.BuildNode(treeBuilder, dataObject, nodeInfo);
		}

		public override void BuildChildNodes (ITreeBuilder ctx, object dataObject)
		{
			var refs = dataObject as DProjectReferenceCollection;
			refs.Update += ProjectReferences_CollectionChanged;

			if (refs.HasReferences)
			{
				foreach (var incl in refs.Includes)
					ctx.AddChild (new DProjectReference(refs.Owner, ReferenceType.Package, incl){ NameGetter = refs.GetIncludeName });
				foreach(var p in refs.ReferencedProjectIds)
					ctx.AddChild(new DProjectReference(refs.Owner, ReferenceType.Project, p));
			}
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return ((DProjectReferenceCollection) dataObject).HasReferences;
		}

		public override int CompareObjects (ITreeNavigator thisNode, ITreeNavigator otherNode)
		{
			return -1;
		}

		void ProjectReferences_CollectionChanged(object sender, EventArgs e)
		{
			Context.GetTreeBuilder ().UpdateChildren ();
			/*var pref = sender as DProjectReference;
			if (pref == null)
				return;

			var p = pref.OwnerProject;
			var tb = Context.GetTreeBuilder (p.References);
			int i;
			switch (e.Action) {
			case NotifyCollectionChangedAction.Add:
				for (i = e.NewStartingIndex; i < e.NewItems.Count; i++)
					tb.AddChild (e.NewItems[i]);
				break;
			case NotifyCollectionChangedAction.Remove:
				for (i = e.OldStartingIndex; i < e.OldItems.Count; i++)
					if(tb.FindChild(e.OldItems[i],true))
						tb.Remove ();
				break;
			case NotifyCollectionChangedAction.Reset:
				if(tb.MoveToFirstChild())
					while (tb.MoveToNextObject())
						tb.Remove ();
				break;
			default:
				throw new InvalidOperationException ("Invalid collection-changed action type");
			}*/
		} 

		void OnRemoveReference (object sender, ProjectReferenceEventArgs e)
		{
			var p = e.Project as AbstractDProject;
			if (p != null) {
				var tb = Context.GetTreeBuilder (p.References);
				if (tb != null && tb.FindChild (e.ProjectReference, true))
					tb.Remove ();
			}
		}

		void OnAddReference (object sender, ProjectReferenceEventArgs e)
		{
			var p = e.Project as AbstractDProject;
			if (p != null) {
				var tb = Context.GetTreeBuilder (p.References);
				if (tb != null)
					tb.AddChild (e.ProjectReference);
			}
		}
	}

	class ProjectReferenceFolderNodeCommandHandler: NodeCommandHandler
	{/*
		public override bool CanDropNode (object dataObject, DragOperation operation)
		{
			return dataObject is ProjectReference || dataObject is Project;
		}

		public override void OnNodeDrop (object dataObject, DragOperation operation)
		{
			// It allows dropping either project references or projects.
			// Dropping a project creates a new project reference to that project

			var project = dataObject as AbstractDProject;
			if (project != null) {
				var pr = new DProjectReference (project);
				DotNetProject p = CurrentNode.GetParentDataItem (typeof(DotNetProject), false) as DotNetProject;
				// Circular dependencies are not allowed.
				if (HasCircularReference (project, p.Name))
					return;

				// If the reference already exists, bail out
				if (ProjectReferencesProject (p, project.Name))
					return;
				p.References.Add (pr);
				IdeApp.ProjectOperations.Save (p);
				return;
			}

			// It's dropping a ProjectReference object.

			ProjectReference pref = dataObject as ProjectReference;
			ITreeNavigator nav = CurrentNode;

			if (operation == DragOperation.Move) {
				NodePosition pos = nav.CurrentPosition;
				nav.MoveToObject (dataObject);
				DotNetProject p = nav.GetParentDataItem (typeof(DotNetProject), true) as DotNetProject;
				nav.MoveToPosition (pos);
				DotNetProject p2 = nav.GetParentDataItem (typeof(DotNetProject), true) as DotNetProject;

				p.References.Remove (pref);

				// Check if there is a cyclic reference after removing from the source project
				if (pref.ReferenceType == ReferenceType.Project) {
					DotNetProject pdest = p.ParentSolution.FindProjectByName (pref.Reference) as DotNetProject;
					if (pdest == null || ProjectReferencesProject (pdest, p2.Name)) {
						// Restore the dep
						p.References.Add (pref);
						return;
					}
				}

				p2.References.Add (pref);
				IdeApp.ProjectOperations.Save (p);
				IdeApp.ProjectOperations.Save (p2);
			} else {
				nav.MoveToParent (typeof(DotNetProject));
				DotNetProject p = nav.DataItem as DotNetProject;

				// Check for cyclic referencies
				if (pref.ReferenceType == ReferenceType.Project) {
					DotNetProject pdest = p.ParentSolution.FindProjectByName (pref.Reference) as DotNetProject;
					if (pdest == null)
						return;
					if (HasCircularReference (pdest, p.Name))
						return;

					// The reference is already there
					if (ProjectReferencesProject (p, pdest.Name))
						return;
				}
				p.References.Add ((ProjectReference) pref.Clone ());
				IdeApp.ProjectOperations.Save (p);
			}
		}
		*/
		public override void ActivateItem ()
		{
			AddReferenceToProject ();
		}

		[CommandHandler (ProjectCommands.AddReference)]
		public void AddReferenceToProject ()
		{
			var p = (AbstractDProject) CurrentNode.GetParentDataItem (typeof(AbstractDProject), false);
			if (p.References.CanAdd && p.References.AddReference()) {
				IdeApp.ProjectOperations.Save (p);
				CurrentNode.Expanded = true;
			}
		}
		/*
		bool HasCircularReference (AbstractDProject project, string targetProject)
		{
			bool result = ProjectReferencesProject (project, targetProject);
			if (result)
				MessageService.ShowError (GettextCatalog.GetString ("Cyclic project references are not allowed."));
			return result;
		}

		bool ProjectReferencesProject (Project project, string targetProject)
		{
			if (project.Name == targetProject)
				return true;

			var prjs = project.ParentSolution.GetAllProjects ();
			var dprj = project as AbstractDProject;

			if(dprj != null)
				foreach (string prjId in dprj.References.ReferencedProjectIds) {
					foreach (var prj in prjs)
						if (prj.ItemId == prjId) {
							if (ProjectReferencesProject (prj, targetProject))
								return true;
							break;
						}
				}
			return false;
		}*/
	}
}
