using MonoDevelop.Core;
using MonoDevelop.Projects.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace MonoDevelop.D.Dub
{
	public class PackageJsonParser : IFileFormat
	{
		public bool CanReadFile(FilePath file, Type expectedObjectType)
		{
			return file.FileName == "package.json";
		}

		public bool CanWriteFile(object obj)
		{
			throw new NotImplementedException();
		}

		public void ConvertToFormat(object obj)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<string> GetCompatibilityWarnings(object obj)
		{
			throw new NotImplementedException();
		}

		public List<Core.FilePath> GetItemFiles(object obj)
		{
			throw new NotImplementedException();
		}

		public Core.FilePath GetValidFormatName(object obj, Core.FilePath fileName)
		{
			throw new NotImplementedException();
		}

		class DepConverter : Newtonsoft.Json.Converters.CustomCreationConverter<DubProjectDependency>
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

		public object ReadFile(FilePath file, Type expectedType, IProgressMonitor monitor)
		{
			var settings = new JsonSerializerSettings();
			var json = File.ReadAllText(file);
			
			settings.Converters.Add(new DepConverter());
			
			var dp = JsonConvert.DeserializeObject<DubProject>(json, settings);
			
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
			throw new NotImplementedException();
		}
	}
}
