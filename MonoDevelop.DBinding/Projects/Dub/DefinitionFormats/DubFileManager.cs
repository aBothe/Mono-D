using MonoDevelop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public class DubFileManager
	{
		#region Properties

		public static readonly DubFileManager Instance = new DubFileManager();

		readonly HashSet<DubFileReader> supportedDubFileFormats = new HashSet<DubFileReader> {
			new DubJson (),
			new DubSdl()
		};

		#endregion

		DubFileManager()
		{
		}

		public bool CanLoad(string file)
		{
			return supportedDubFileFormats.Any((i) => i.CanLoad(file));
		}

		public DubSolution LoadAsSolution(string file, IProgressMonitor monitor)
		{
			var sln = new DubSolution{
				FileName = file,
				BaseDirectory = new FilePath(file).ParentDirectory
			};

			var defaultPackage = LoadProject(file, sln, monitor, LoadFlags.None);

			sln.StartupItem = defaultPackage;

			LoadSubProjects(defaultPackage, monitor);

			// Populate project configurations to solution
			if (defaultPackage.Configurations.Count == 0)
				sln.CreateDefaultConfigurations ();
			else {
				foreach (var prjCfg in defaultPackage.Configurations)
					sln.AddConfiguration (prjCfg.Name, false);
			}

			// Apply subConfigurations
			var subConfigurations = new Dictionary<string, string>(defaultPackage.CommonBuildSettings.subConfigurations);

			foreach (var item in sln.Items)
			{
				var prj = item as SolutionEntityItem;
				if (prj == null)
					continue;

				foreach (var cfg in sln.Configurations)
				{
					var prjItem = cfg.GetEntryForItem(prj);
					string cfgId;
					if (subConfigurations.TryGetValue(prj.ItemId, out cfgId))
						prjItem.ItemConfiguration = cfgId;

					var prjCfg = defaultPackage.GetConfiguration(cfg.Selector) as DubProjectConfiguration;
					if (prjCfg != null && prjCfg.BuildSettings.subConfigurations.TryGetValue(prj.ItemId, out cfgId))
						prjItem.ItemConfiguration = cfgId;
				}
			}

			sln.LoadUserProperties();

			return sln;
		}

		public enum LoadFlags
		{
			None,
			LoadReferences
		}

		public DubProject LoadProject(string file, Solution parentSolution, IProgressMonitor monitor, LoadFlags flags = LoadFlags.LoadReferences, DubProject superProject = null)
		{
			if(monitor != null)
				monitor.BeginTask("Load dub project '" + file + "'", 1);
			try
			{
				var prj = supportedDubFileFormats.First((i) => i.CanLoad(file)).Load(file, superProject, parentSolution);
				if (flags.HasFlag(LoadFlags.LoadReferences))
					LoadSubProjects(prj, monitor);
				return prj;
			}
			finally
			{
				if (monitor != null)
					monitor.EndTask();
			}
		}

		public void LoadSubProjects(DubProject defaultPackage, IProgressMonitor monitor)
		{
			var sln = defaultPackage.ParentSolution;

			foreach (var dep in defaultPackage.DubReferences)
			{
				var file = DubFileReader.GetDubFilePath(defaultPackage, dep.Path);
				if (String.IsNullOrWhiteSpace(dep.Path) || !CanLoad(file))
					continue;

				var subProject = supportedDubFileFormats.First((i) => i.CanLoad(file)).Load(file, null, sln);

				if (defaultPackage != subProject) 
				{
					LoadSubProjects (subProject, monitor);
				}
			}
		}
	}
}
