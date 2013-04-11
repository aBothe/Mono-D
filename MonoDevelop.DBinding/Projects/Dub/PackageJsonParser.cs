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
			return false; // Everything has to be manipulated manually!
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
			object ret;
			var serializer = new JsonSerializer();

			DubSolution sln;
			var dp = new DubProject { FileName = file, BaseDirectory = file.ParentDirectory };
			if (expectedType.Equals(typeof(SolutionEntityItem))){
				ret = dp;
				sln = null;
			}
			else if(expectedType.Equals(typeof(WorkspaceItem)))
			{
				ret = sln = new DubSolution();
				sln.RootFolder.AddItem(dp, false);
				sln.StartupItem = dp;
				dp.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = "Default", Id = "Default" });
			}
			else
				return null;

			using (var s = File.OpenText(file))
			using(var rdr = new JsonTextReader(s))
			{
				while (rdr.Read())
				{
					if (rdr.TokenType == JsonToken.PropertyName)
						dp.TryPopulateProperty(rdr.Value as string, rdr);
					else if (rdr.TokenType == JsonToken.EndObject)
						break;
				}
			}

			dp.UpdateFilelist();
			if (sln != null)
				sln.LoadUserProperties();

			return ret;
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
			
		}
	}
}
