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

		public abstract bool CanLoad(string file);

		protected abstract void Read(DubProject target, Object streamReader);

		public DubProject Load(DubProject superPackage, Solution parentSolution, Object streamReader, string originalFile){
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
			
			Read (defaultPackage, streamReader);

			if (returnSubProject) {
				defaultPackage.packageName = superPackage.packageName + ":" + (defaultPackage.packageName ?? string.Empty);

				var sub = defaultPackage as DubSubPackage;
				var sourcePaths = sub.GetSourcePaths ().ToArray();
				if (sourcePaths.Length > 0 && !string.IsNullOrWhiteSpace(sourcePaths[0]))
					sub.VirtualBasePath = new FilePath(sourcePaths [0]);

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

		#region Dub References
		static Regex dubInstalledPackagesOutputRegex = new Regex("  (?<name>.+) (?<version>.+): (?<path>.+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);
		internal static Dictionary<string, string> DubListOutputs = new Dictionary<string, string>();

		protected void FillDubReferencesPaths(DubProject prj)
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
			bool ret = false;
			DubProjectDependency dep;
			if (string.IsNullOrEmpty(outp))
				return false;

			foreach (Match match in dubInstalledPackagesOutputRegex.Matches(outp))
			{
				ret = true;
				if (match.Success && prj.DubReferences.dependencies.TryGetValue(match.Groups["name"].Value, out dep) &&
					string.IsNullOrEmpty(dep.Path) &&
					(string.IsNullOrEmpty(dep.Version) || CheckRequiredDepVersion(dep.Version, match.Groups["version"].Value))
					/* && !dep.Name.Contains(":") */) // Since dub v0.9.20, subpackages' paths are included in the path list as well!
					dep.Path = match.Groups["path"].Value.Trim();

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
