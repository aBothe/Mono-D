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
		public Type ItemType => typeof(DubProject);

		public override bool CanHandleItem(SolutionEntityItem item) =>
			ItemType != null && ItemType.IsAssignableFrom(item.GetType());

		public override bool CanHandleFile(string fileName, string typeGuid) => 
			string.Compare(typeGuid, this.Guid, true) == 0 || DubFileManager.Instance.CanLoad(fileName);

		public override SolutionEntityItem LoadSolutionItem(IProgressMonitor monitor, string fileName, MSBuildFileFormat expectedFormat, string itemGuid) => 
			DubFileManager.Instance.LoadProject(fileName, Ide.IdeApp.Workspace.GetAllSolutions()[0], monitor);
	}
}
