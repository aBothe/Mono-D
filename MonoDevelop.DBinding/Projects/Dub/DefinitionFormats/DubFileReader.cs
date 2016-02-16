using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using System.Text.RegularExpressions;
using MonoDevelop.D.Building;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public abstract class DubFileReader
	{
		public const string DubSelectionsJsonFile = "dub.selections.json";
		public const string PackageJsonFile = "package.json";
		public const string DubJsonFile = "dub.json";
		public const string DubSdlFile = "dub.sdl";

		public abstract bool CanLoad(string file);

		public static string GetDubFilePath(AbstractDProject @base, string subPath)
		{
			var sub = @base as DubSubPackage;
			if (sub != null)
				sub.useOriginalBasePath = true;
			var packageDir = @base.GetAbsPath(Building.ProjectBuilder.EnsureCorrectPathSeparators(subPath));

			if (sub != null)
				sub.useOriginalBasePath = false;

			string dubFileToLoad;
			if (File.Exists(dubFileToLoad = Path.Combine(packageDir, PackageJsonFile)) ||
				File.Exists(dubFileToLoad = Path.Combine(packageDir, DubJsonFile)) ||
				File.Exists(dubFileToLoad = Path.Combine(packageDir, DubSdlFile)))
				return dubFileToLoad;

			return string.Empty;
		}

		protected abstract void Read(DubProject target, Object streamReader);

		public DubProject Load(DubProject superPackage, Solution parentSolution, Object streamReader, string originalFile)
		{
			DubProject defaultPackage;

			if(parentSolution == null)
				throw new InvalidDataException("Parent solution must be specified!");

			if ((defaultPackage = parentSolution.GetProjectsContainingFile(new FilePath(originalFile)).FirstOrDefault() as DubProject) != null)
			{
				return defaultPackage;
			}

			bool returnSubProject = superPackage != null;

			defaultPackage = returnSubProject ? new DubSubPackage() : new DubProject();

			defaultPackage.FileName = originalFile;
			defaultPackage.BaseDirectory = defaultPackage.FileName.ParentDirectory;

			if (returnSubProject)
			{
				var sub = defaultPackage as DubSubPackage;
				sub.OriginalBasePath = superPackage is DubSubPackage ? (superPackage as DubSubPackage).OriginalBasePath :
					superPackage.BaseDirectory;
				sub.VirtualBasePath = sub.OriginalBasePath;
			}

			defaultPackage.BeginLoad();

			if (parentSolution is DubSolution)
				(parentSolution as DubSolution).AddProject(defaultPackage);
			else
				parentSolution.RootFolder.AddItem(defaultPackage, false);

			foreach(SolutionConfiguration slnCfg in parentSolution.Configurations)
			{
				var slnCfgItemCfg = slnCfg.GetEntryForItem(defaultPackage) ?? slnCfg.AddItem(defaultPackage);
				slnCfgItemCfg.Build = true;
				slnCfgItemCfg.Deploy = true;

				defaultPackage.Configurations.Add(new DubProjectConfiguration { Id = slnCfg.Id, Name = slnCfg.Name, Platform = slnCfg.Platform });
			}

			Read(defaultPackage, streamReader);

			// Fill dub references
			if (defaultPackage.DubReferences.Any(dep => string.IsNullOrWhiteSpace(dep.Path)))
				FillDubReferencesPaths(defaultPackage);
			else
				defaultPackage.DubReferences.FireUpdate();

			if (returnSubProject)
			{
				defaultPackage.packageName = superPackage.packageName + ":" + (defaultPackage.packageName ?? string.Empty);

				var sub = defaultPackage as DubSubPackage;
				var sourcePaths = sub.GetSourcePaths().ToArray();
				if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
					sub.VirtualBasePath = new FilePath(sourcePaths[0]);

				// TODO: What to do with new configurations that were declared in this sub package? Add them to all other packages as well?
			}

			defaultPackage.Items.Add(new ProjectFile(originalFile, BuildAction.None));

			// https://github.com/aBothe/Mono-D/issues/555
			var dubSelectionJsonPath = defaultPackage.BaseDirectory.Combine(DubSelectionsJsonFile);
			if (File.Exists(dubSelectionJsonPath))
				defaultPackage.Items.Add(new ProjectFile(dubSelectionJsonPath, BuildAction.None));

			defaultPackage.EndLoad();

			return defaultPackage;
		}

		public DubProject Load(string file, DubProject superProject, Solution parentSolution)
		{
			using (var fs = new FileStream(file, FileMode.Open))
			using (var sr = new StreamReader(fs))
				return Load(superProject, parentSolution, sr, file);
		}

		protected void IntroduceConfiguration(DubProject prj, DubProjectConfiguration projectConfiguration)
		{
			var sln = prj.ParentSolution;
			if (sln != null && sln.Configurations.Count == 1 && sln.Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
				sln.Configurations.Clear();
			if (prj.Configurations.Count == 1 && prj.Configurations[0].Id == DubProjectConfiguration.DefaultConfigId)
				prj.Configurations.Clear();



			var slnCfg = sln.GetConfiguration(projectConfiguration.Selector);

			if(slnCfg != null)
			{
				prj.Configurations.Add(projectConfiguration);

				var slnPrjCfg = slnCfg.GetEntryForItem(prj) ?? slnCfg.AddItem(prj);
				slnPrjCfg.Build = true;
			}
			else
			{
				slnCfg = new SolutionConfiguration
				{
					Id = projectConfiguration.Id,
					Name = projectConfiguration.Name,
					Platform = projectConfiguration.Platform
				};
				sln.Configurations.Add(slnCfg);

				foreach(var slnPrj in sln.GetAllProjects())
				{
					slnCfg.AddItem(slnPrj).Build = true;
					slnPrj.Configurations.Add(projectConfiguration);
				}
			}
		}

		#region Dub References
		static Regex dubInstalledPackagesOutputRegex = new Regex("  (?<name>.+) (?<version>.+): (?<path>.+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);
		internal static Dictionary<string, string> DubListOutputs = new Dictionary<string, string>();

		void FillDubReferencesPaths(DubProject prj)
		{
			string err, outp;
			var baseDir = prj.BaseDirectory.ToString();
			if (DubListOutputs.TryGetValue(baseDir, out outp))
			{
				TryInterpretDubListOutput(prj, outp);
			}
			else
			{
				try
				{
					ProjectBuilder.ExecuteCommand(DubSettings.Instance.DubCommand, "list", baseDir, null, out err, out outp);
					// Backward compatiblity
					if (!string.IsNullOrWhiteSpace(err) || !TryInterpretDubListOutput(prj, outp))
					{
						ProjectBuilder.ExecuteCommand(DubSettings.Instance.DubCommand, "list-installed", baseDir, null, out err, out outp);
						TryInterpretDubListOutput(prj, outp);
					}

					if (!string.IsNullOrWhiteSpace(outp))
						DubListOutputs[baseDir] = outp;
				}
				catch (Exception ex)
				{
					LoggingService.LogError("Error while resolving dub dependencies via executing 'dub list-installed'", ex);
				}
			}

			prj.DubReferences.FireUpdate();
		}

		bool TryInterpretDubListOutput(DubProject prj, string outp)
		{
			if (string.IsNullOrEmpty(outp))
				return false;

			bool ret = false;
			foreach (Match match in dubInstalledPackagesOutputRegex.Matches(outp))
			{
				ret = true;
				if (match.Success)
				{
					foreach (var kv in prj.DubReferences.GetDependencyEntries())
					{
						var dep = kv.Value;
						if (kv.Key == match.Groups["name"].Value && string.IsNullOrWhiteSpace(dep.Path) &&
							(string.IsNullOrEmpty(dep.Version) || CheckRequiredDepVersion(dep.Version, match.Groups["version"].Value)))
						{
							dep.Path = match.Groups["path"].Value.Trim();
						}
					}
				}
			}
			return ret;
		}

		static Regex SemVerRegex = new Regex(
			@"(?<op>~>|==|>=|<=)?" +
			@"(?<maj>0|[1-9][0-9]*)" +
			@"(\.(?<min>0|[1-9][0-9]*))?" +
			@"(\.(?<bug>0|[1-9][0-9]*))?" +
			@"(?<prerelease>-[\da-z\-]+(?:\.[\da-z\-]+)*)?" +
			@"(?<build>\+[\da-z\-]+(?:\.[\da-z\-]+)*)?", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);


		static bool CheckRequiredDepVersion(string expectedVersion, string actualVersion)
		{
			if (expectedVersion == "*")
				return true;

			var expectedVer = SemVerRegex.Match(expectedVersion);
			var actualVer = SemVerRegex.Match(actualVersion);

			// Discard invalid/obsolete stuff/*
			if (!expectedVer.Success || !actualVer.Success)
				return true;

			// also discard explicit version ranges like ">=1.3.0 <=1.3.4" for now...who uses this?
			var cmp = CompareVersions(expectedVer, actualVer);

			switch (expectedVer.Groups["op"].Value ?? string.Empty)
			{
				case "~>":
					if (cmp == 0)
						return true;

					if (cmp > 0)
					{
						int maj_expected, maj_actual;
						int min_expected, min_actual;
						int bug_expected, bug_actual;

						int.TryParse(expectedVer.Groups["maj"].Value, out maj_expected);
						int.TryParse(actualVer.Groups["maj"].Value, out maj_actual);
						int.TryParse(expectedVer.Groups["min"].Value, out min_expected);
						int.TryParse(actualVer.Groups["min"].Value, out min_actual);
						int.TryParse(expectedVer.Groups["bug"].Value, out bug_expected);
						int.TryParse(actualVer.Groups["bug"].Value, out bug_actual);

						if (bug_expected != 0)
							return maj_actual == maj_expected && min_actual - min_expected <= 1;
						if (min_expected != 0)
							return maj_actual - maj_expected <= 1;
					}
					return false;
				case ">=":
					return cmp >= 0;
				case "<=":
					return cmp <= 0;
				case "":
				case "==":
					return cmp == 0;
			}

			return true;
		}

		/// <summary>
		/// Compares the versions.
		/// </summary>
		/// <returns>
		/// greater 0 if actual greater than expected;
		/// 0 if expected equals actual;
		/// less 0 if actual less than expected</returns>
		static int CompareVersions(Match expectedVer, Match actualVer)
		{
			int maj_expected, maj_actual;
			int min_expected, min_actual;
			int bug_expected, bug_actual;

			int.TryParse(expectedVer.Groups["maj"].Value, out maj_expected);
			int.TryParse(actualVer.Groups["maj"].Value, out maj_actual);
			int.TryParse(expectedVer.Groups["min"].Value, out min_expected);
			int.TryParse(actualVer.Groups["min"].Value, out min_actual);
			int.TryParse(expectedVer.Groups["bug"].Value, out bug_expected);
			int.TryParse(actualVer.Groups["bug"].Value, out bug_actual);

			if (maj_expected != maj_actual)
				return maj_actual - maj_expected;

			if (min_expected != min_actual)
				return min_actual - min_expected;

			if (bug_expected != bug_actual)
				return bug_actual - bug_expected;

			var prerelease_expected = expectedVer.Groups["prerelease"].Value;
			var prerelease_actual = actualVer.Groups["prerelease"].Value;

			// Prefer non-prerelease versions
			var prerelease = // 1 == only expectedVersion has prerelease; -1 == only actualVersion has prerelease; 0 == both or none have prerelease.
				(string.IsNullOrWhiteSpace(prerelease_expected) ? 0 : 1) -
				(string.IsNullOrWhiteSpace(prerelease_actual) ? 0 : 1);
			if (prerelease != 0 && !string.IsNullOrWhiteSpace(prerelease_expected))
				return prerelease;

			// Don't sort lexicographically for now and discard further checks..
			return 0;
		}
		#endregion
	}
}
