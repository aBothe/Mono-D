using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Converters;

namespace MonoDevelop.D.Projects.Dub
{
	public class PackageJsonParser : IFileFormat
	{
		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.FileName == "package.json";
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
			throw new NotImplementedException();
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
			var serializer = new JsonSerializer();
			var dp = new DubSolution(file);

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

			dp.FinalizeDeserialization();

			return dp;
		}

		public bool SupportsFramework(Core.Assemblies.TargetFramework framework)
		{
			return false;
		}

		public bool SupportsMixedFormats
		{
			get { return false; }
		}

		public void WriteFile(Core.FilePath file, object obj, Core.IProgressMonitor monitor)
		{
			
		}
	}
}
