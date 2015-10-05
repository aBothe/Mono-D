using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public abstract class DubFileReader
	{
		public abstract bool CanLoad(string file);

		protected abstract void Read(DubProject target, StreamReader r);

		public DubProject Load(DubProject superPackage, Solution parentSolution, StreamReader s, string originalFile){
			bool returnSubProject = superPackage != null;

			var defaultPackage = returnSubProject ? new DubSubPackage() : new DubProject ();

			defaultPackage.FileName = originalFile;
			defaultPackage.BaseDirectory = defaultPackage.FileName.ParentDirectory;

			if (returnSubProject) {
				var sub = defaultPackage as DubSubPackage;
				sub.OriginalBasePath = superPackage is DubSubPackage ? (superPackage as DubSubPackage).OriginalBasePath : 
					superPackage.BaseDirectory;
				sub.VirtualBasePath = sub.OriginalBasePath;
			}

			defaultPackage.BeginLoad();

			defaultPackage.AddProjectAndSolutionConfiguration(new DubProjectConfiguration { Name = GettextCatalog.GetString("Default"), Id = DubProjectConfiguration.DefaultConfigId });

			if (returnSubProject) {
				superPackage.packagesToAdd.Add (defaultPackage);
			}
			
			Read (defaultPackage, s);

			if (returnSubProject) {
				defaultPackage.packageName = superPackage.packageName + ":" + (defaultPackage.packageName ?? string.Empty);

				var sub = defaultPackage as DubSubPackage;
				var sourcePaths = sub.GetSourcePaths ().ToArray();
				if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
					sub.VirtualBasePath = new FilePath(sourcePaths [0]);

				// TODO: What to do with new configurations that were declared in this sub package? Add them to all other packages as well?
			}

			defaultPackage.Items.Add(new ProjectFile(originalFile, BuildAction.None));

			defaultPackage.EndLoad();

			return defaultPackage;
		}

		public DubProject Load(string file, DubProject superProject, Solution parentSolution)
		{
			using (var fs = new FileStream(file, FileMode.Open))
			using (var sr = new StreamReader(fs))
				return Load(superProject, parentSolution, sr, file);
		}
	}
}
