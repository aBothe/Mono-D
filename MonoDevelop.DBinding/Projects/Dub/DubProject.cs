using MonoDevelop.Core;
using MonoDevelop.Projects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubProject : AbstractDProject
	{
		#region Properties
		List<string> authors = new List<string>();
		Dictionary<string, DubProjectDependency> dependencies = new Dictionary<string, DubProjectDependency>();
		public readonly DubBuildSettings GlobalBuildSettings = new DubBuildSettings();

		public override string Name { get; set; } // override because the name is normally derived from the file name -- package.json is not the project's file name!
		public override FilePath FileName { get; set; }
		public string Homepage;
		public string Copyright;
		public List<string> Authors { get { return authors; } }
		public Dictionary<string, DubProjectDependency> Dependencies
		{
			get { return dependencies; }
		}

		public List<string> PhysicalDependencyPaths
		{
			get {
				var l = new List<string>(Dependencies.Count);

				foreach (var dep in Dependencies.Values)
				{
					string dir;
					if (!string.IsNullOrWhiteSpace(dep.Path))
						dir = Path.IsPathRooted(dep.Path) ? dep.Path : 
							BaseDirectory.ToAbsolute(new FilePath(dep.Path)).ToString();
					else
					{
						var depDir = BaseDirectory.Combine(".dub", "packages", dep.Name);

						//ISSUE: Theoretically, one had to load the package.json from the dependencies base directory either in order to fully determine the correct source location!
						// -> Just assume all the stuff to be either located in /source or /src!

						dir = depDir.Combine("source");
						if (!Directory.Exists(dir))
						{
							dir = depDir.Combine("src");
							if (!Directory.Exists(dir))
								continue;
						}
					}

					l.Add(dir);
				}

				return l;
			}
		}

		public List<DubBuildSettings> GetBuildSettings(ConfigurationSelector sel)
		{
			var settingsToScan = new List<DubBuildSettings>(4);
			settingsToScan.Add(GlobalBuildSettings);

			DubProjectConfiguration pcfg;
			if (sel == null || (pcfg = GetConfiguration(sel) as DubProjectConfiguration) == null)
				foreach (DubProjectConfiguration cfg in Configurations)
					settingsToScan.Add(cfg.BuildSettings);
			else
				settingsToScan.Add(pcfg.BuildSettings);

			return settingsToScan;
		}

		protected override List<FilePath> OnGetItemFiles(bool includeReferencedFiles)
		{
			var files = new List<FilePath>();

			foreach(var dir in GetSourcePaths((ConfigurationSelector)null))
				foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
					files.Add(new FilePath(f));

			return files;
		}

		public override IEnumerable<string> GetSourcePaths(ConfigurationSelector sel)
		{
			return GetSourcePaths(GetBuildSettings(sel));
		}

		public IEnumerable<string> GetSourcePaths(List<DubBuildSettings> settings)
		{
			string d;
			List<DubBuildSetting> l;
			bool returnedOneItem = false;
			foreach (var sett in settings)
				if (sett.TryGetValue(DubBuildSettings.SourcePathsProperty, out l))
				{
					for (int i = l.Count - 1; i >= 0; i--) // Ignore architecture/os/compiler restrictions for now
						for (int j = l[i].Flags.Length - 1; j >= 0; j--)
						{
							d = l[i].Flags[j];
							if (!Path.IsPathRooted(d))
								d = BaseDirectory.Combine(d).ToString();

							if (!Directory.Exists(d))
								continue;

							returnedOneItem = true;
						}
				}

			if (returnedOneItem)
				yield break;

			d = BaseDirectory.Combine("source").ToString();
			if (Directory.Exists(d))
				yield return d;

			d = BaseDirectory.Combine("src").ToString();
			if (Directory.Exists(d))
				yield return d;
		}

		public override string ToString ()
		{
			return string.Format ("[DubProject: Name={0}]", Name);
		}
		#endregion

		#region Constructor & Init
		public void UpdateFilelist()
		{
			foreach (var p in PhysicalDependencyPaths)
				LocalIncludeCache.Add (p);

			foreach (var settings in GetBuildSettings(null))
			{
				List<DubBuildSetting> l;
				if(settings.TryGetValue(DubBuildSettings.ImportPathsProperty, out l))
					for (int i = l.Count - 1; i >= 0; i--) // Ignore architecture/os/compiler restrictions for now
						for (int j = l[i].Flags.Length - 1; j >= 0; j--)
							LocalIncludeCache.Add(l[i].Flags[j]);
			}

			DCompilerConfiguration.UpdateParseCacheAsync (LocalIncludeCache, false, LocalIncludeCache_FinishedParsing);

			Items.Clear();
			foreach (var f in GetItemFiles(true))
				Items.Add(new ProjectFile(f));
		}
		#endregion

		#region Serialize & Deserialize
		public bool TryPopulateProperty(string propName, JsonReader j)
		{
			switch (propName)
			{
				case "name":
					Name = j.ReadAsString();
					break;
				case "description":
					Description = j.ReadAsString();
					break;
				case "copyright":
					Copyright = j.ReadAsString();
					break;
				case "homepage":
					Homepage = j.ReadAsString();
					break;
				case "authors":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Authors");
					authors.Clear();
					while (j.Read() && j.TokenType != JsonToken.EndArray)
						if (j.TokenType == JsonToken.String)
							authors.Add(j.Value as string);
					break;
				case "dependencies":
					if (!j.Read() || j.TokenType != JsonToken.StartObject)
						throw new JsonReaderException("Expected { when parsing Authors");
					dependencies.Clear();
					while (j.Read() && j.TokenType != JsonToken.EndObject)
					{
						if (j.TokenType == JsonToken.PropertyName)
							DeserializeDubPrjDependency(j);
					}
					break;
				case "configurations":
					if (!j.Read() || j.TokenType != JsonToken.StartArray)
						throw new JsonReaderException("Expected [ when parsing Configurations");
					if(ParentSolution != null)
						ParentSolution.Configurations.Clear();
					Configurations.Clear();

					while (j.Read() && j.TokenType != JsonToken.EndArray)
						AddProjectAndSolutionConfiguration(DubProjectConfiguration.DeserializeFromPackageJson(j));
					break;

				default:
					return GlobalBuildSettings.TryDeserializeBuildSetting(j);
			}

			return true;
		}

		void DeserializeDubPrjDependency(JsonReader j)
		{
			var depName = j.Value as string;
			string depVersion = null;
			string depPath = null;

			if (!j.Read())
				throw new JsonReaderException("Found EOF when parsing project dependency");

			if (j.TokenType == JsonToken.StartObject)
			{
				while (j.Read() && j.TokenType != JsonToken.EndObject)
				{
					if (j.TokenType == JsonToken.PropertyName)
					{
						switch (j.Value as string)
						{
							case "version":
								depVersion = j.ReadAsString();
								break;
							case "path":
								depPath = j.ReadAsString();
								break;
						}
					}
				}
			}
			else if (j.TokenType == JsonToken.String)
			{
				depVersion = j.Value as string;
			}

			dependencies[depName] = new DubProjectDependency { Name = depName, Version = depVersion, Path = depPath };
		}

		public void AddProjectAndSolutionConfiguration(DubProjectConfiguration cfg)
		{
			if (ParentSolution != null)
			{
				var slnCfg = new SolutionConfiguration(cfg.Name, cfg.Platform);
				ParentSolution.Configurations.Add(slnCfg);
				slnCfg.AddItem(this).Build = true;
			}
			Configurations.Add(cfg);

			if (Configurations.Count == 1)
				DefaultConfigurationId = cfg.Id;
		}
		#endregion

		#region Building
		protected override BuildResult DoBuild(IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			return DubBuilder.BuildProject(this, monitor, configuration);			
		}

		protected override bool OnGetCanExecute(ExecutionContext context, ConfigurationSelector configuration)
		{
			return context.ExecutionHandler.GetType().Name.EndsWith("DefaultExecutionHandler");
		}

		protected override void DoExecute(IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			DubBuilder.ExecuteProject(this,monitor, context, configuration);
		}
		#endregion
	}
}
