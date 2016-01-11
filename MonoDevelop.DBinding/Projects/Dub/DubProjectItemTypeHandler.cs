using System;
using System.IO;
using System.Xml;
using Mono.Addins;
using MonoDevelop.Projects.Formats.MSBuild;
using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects.Dub.DefinitionFormats;

namespace MonoDevelop.D.Projects.Dub
{
	class DubProjectItemTypeHandler : ItemTypeNode
	{
		public Type ItemType { get { return typeof(DubProject); } }

		public override bool CanHandleItem (SolutionEntityItem item)
		{
			return ItemType != null && ItemType.IsAssignableFrom (item.GetType ());
		}

		public override bool CanHandleFile (string fileName, string typeGuid)
		{
			return string.Compare (typeGuid, this.Guid, true) == 0 || DubFileManager.Instance.CanLoad (fileName);
		}

		public override SolutionEntityItem LoadSolutionItem (IProgressMonitor monitor, string fileName, MSBuildFileFormat expectedFormat, string itemGuid)
		{
			return DubFileManager.Instance.LoadProject (fileName, Ide.IdeApp.Workspace.GetAllSolutions () [0], monitor);
		}
	}
}
