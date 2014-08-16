using System;
using System.IO;
using System.Xml;
using Mono.Addins;
using MonoDevelop.Projects.Formats.MSBuild;
using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	class DubProjectItemTypeHandler : ItemTypeNode
	{
		public Type ItemType
		{
			get { return typeof(DubProject); }
		}

		public override bool CanHandleItem(SolutionEntityItem item)
		{
			return ItemType != null && ItemType.IsAssignableFrom(item.GetType());
		}

		public override bool CanHandleFile(string fileName, string typeGuid)
		{
			return string.Compare(typeGuid, this.Guid, true) == 0 || 
				(fileName = fileName.ToLower()).EndsWith(PackageJsonParser.DubJsonFile) || 
				fileName.EndsWith(PackageJsonParser.PackageJsonFile);
		}

		public override SolutionEntityItem LoadSolutionItem(IProgressMonitor monitor, string fileName, MSBuildFileFormat expectedFormat, string itemGuid)
		{
			return PackageJsonParser.ReadPackageInformation(new FilePath(fileName), monitor);
		}
	}
}
