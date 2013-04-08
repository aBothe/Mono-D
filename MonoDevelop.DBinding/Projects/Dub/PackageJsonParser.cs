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
			var prj = obj as DubProjectDefinitionFile;
			var files = new List<FilePath>();

			foreach (var f in Directory.GetFiles(prj.BaseDirectory.Combine("source"), "*", SearchOption.AllDirectories))
				files.Add(new FilePath(f));

			return files;
		}

		public Core.FilePath GetValidFormatName(object obj, Core.FilePath fileName)
		{
			return fileName.ParentDirectory.Combine("package.json");
		}

		class DepConverter : CustomCreationConverter<DubProjectDependency>
		{
			bool isReading = false;

			public override bool CanConvert(Type objectType)
			{
				return base.CanConvert(objectType) && !isReading;
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				string name;
				var s = reader.Path.Split('.');
				
				name = s[s.Length-1];

				DubProjectDependency dpd;
				if (reader.TokenType == JsonToken.String)
					dpd = new DubProjectDependency { Version = reader.Value as string };
				else
				{
					isReading = true;
					try
					{
						dpd = serializer.Deserialize<DubProjectDependency>(reader);
					}
					finally { isReading = false; }
				}

				dpd.Name = name;
				return dpd;
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}

			public override DubProjectDependency Create(Type objectType)
			{
				return new DubProjectDependency();
			}
		}

		class BuildConfigRes : Newtonsoft.Json.Serialization.DefaultContractResolver
		{
			protected override string ResolvePropertyName(string propertyName)
			{
				return base.ResolvePropertyName(propertyName);
			}
		}

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			var serializer = new JsonSerializer();
			var dp = new DubProjectDefinitionFile { FileName = file, BaseDirectory = file.ParentDirectory };

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
			var json = JsonConvert.SerializeObject(obj, new DepConverter());
			File.WriteAllText(file, json);
		}
	}
}
