using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Converters;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub
{
	public class PackageJsonParser : IFileFormat
	{
		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.FileName == "package.json" &&
				(expectedObjectType.Equals(typeof(WorkspaceItem)) ||
				expectedObjectType.Equals(typeof(SolutionEntityItem)));
		}

		public bool CanWriteFile(object obj)
		{
			return false; // Everything has to be manipulated manually (atm)!
		}

		public void ConvertToFormat(object obj)
		{
			
		}

		public IEnumerable<string> GetCompatibilityWarnings(object obj)
		{
			yield return string.Empty;
		}

		public List<FilePath> GetItemFiles(object obj)
		{
			return new List<FilePath>();
		}

		public Core.FilePath GetValidFormatName(object obj, Core.FilePath fileName)
		{
			return fileName.ParentDirectory.Combine("package.json");
		}

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			if (!expectedType.Equals (typeof(WorkspaceItem)))
				return null;

			using (var s = File.OpenText (file))
			using (var r = new JsonTextReader (s))
				return ReadPackageInformation (file, r);
		}

		public static DubSolution ReadPackageInformation(FilePath packageJsonPath,JsonReader r)
		{
			var sln = new DubSolution ();
			var defaultPackage = new DubProject();
			defaultPackage.FileName = packageJsonPath;
			defaultPackage.BaseDirectory = packageJsonPath.ParentDirectory;

			sln.RootFolder.AddItem (defaultPackage, false);
			sln.StartupItem = defaultPackage;

			defaultPackage.BeginLoad ();

			defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			while (r.Read ()) {
				if (r.TokenType == JsonToken.PropertyName) {
					var propName = r.Value as string;
					defaultPackage.TryPopulateProperty (propName, r);
				}
				else if (r.TokenType == JsonToken.EndObject)
					break;
			}

			defaultPackage.Items.Add(new ProjectFile(packageJsonPath, BuildAction.None));

			foreach (var f in defaultPackage.GetItemFiles(true))
				defaultPackage.Items.Add(new ProjectFile(f));

			sln.LoadUserProperties ();
			defaultPackage.EndLoad ();
			return sln;
		}

		public bool SupportsFramework(Core.Assemblies.TargetFramework framework)
		{
			return false;
		}

		public bool SupportsMixedFormats
		{
			get { return true; }
		}

		public void WriteFile(Core.FilePath file, object obj, Core.IProgressMonitor monitor)
		{
			monitor.ReportError ("Can't write dub package information! Change it manually in the definition file!", new InvalidOperationException ());
		}
	}
}