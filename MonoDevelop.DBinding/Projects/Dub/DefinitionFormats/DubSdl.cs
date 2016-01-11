using MonoDevelop.D.Projects.Dub.DefinitionFormats.SDL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	class DubSdl : DubFileReader
	{
		public override bool CanLoad(string file)
		{
			return Path.GetFileName(file).ToLower() == DubSdlFile;
		}

		protected override void Read(DubProject target, Object input)
		{
			if (input is StreamReader)
			{
				var tree = SDL.SdlParser.Parse(input as StreamReader);
				//TODO: Display parse errors?

				foreach (var decl in tree.Children)
					InterpretGlobalProperty(decl, target);
			}
			else if (input is SDLObject)
				foreach (var decl in (input as SDLObject).Children)
					InterpretGlobalProperty(decl, target);
			else
				throw new ArgumentException("input");
		}

		void InterpretGlobalProperty(SDLDeclaration decl, DubProject target)
		{
			switch (decl.Name.ToLower())
			{
				case "displayname":
					target.Name = ExtractFirstAttribute(decl);
					break;
				case "name":
					target.packageName = ExtractFirstAttribute(decl);
					break;
				case "description":
					target.Description = ExtractFirstAttribute(decl);
					break;
				case "homepage":
					target.Homepage = ExtractFirstAttribute(decl);
					break;
				case "authors":
					target.Authors.Clear();
					target.Authors.AddRange(ExtractUnnamedAttributes(decl));
					break;
				case "copyright":
					target.Copyright = ExtractFirstAttribute(decl);
					break;
				case "subpackage":
					if (decl is SDLObject)
						base.Load(target, target.ParentSolution, decl, target.FileName);
					else
						DubFileManager.Instance.LoadProject(GetDubFilePath(target, ExtractFirstAttribute(decl)), target.ParentSolution, null, DubFileManager.LoadFlags.None, target);
					break;
				case "configuration":
					var o = decl as SDLObject;
					if (o != null)
					{
						var c = new DubProjectConfiguration { Name = ExtractFirstAttribute(o) };
						if (string.IsNullOrEmpty(c.Name))
							c.Name = "<Undefined>";

						foreach (var childDecl in o.Children)
						{
							switch (childDecl.Name.ToLowerInvariant())
							{
								case "platforms":
									c.Platform = string.Join("|", ExtractUnnamedAttributes(childDecl));
									break;
								default:
									InterpretBuildSetting(childDecl, c.BuildSettings);
									break;
							}
						}


						target.AddProjectAndSolutionConfiguration(c);
					}
					break;
				case "buildtype":
					var name = ExtractFirstAttribute(decl);
					if (!string.IsNullOrEmpty(name))
					{
						target.buildTypes.Add(name);
					}
					// Ignore remaining contents as they're not needed by mono-d

					target.buildTypes.Sort();
					break;
				default:
					InterpretBuildSetting(decl, target.CommonBuildSettings);
					break;
			}
		}

		void InterpretBuildSetting(SDLDeclaration decl, DubBuildSettings settings)
		{
			var propName = decl.Name.ToLowerInvariant();
			DubBuildSetting sett = null;

			switch (propName)
			{
				case "dependency":
					var depName = ExtractFirstAttribute(decl);
					var depVersion = ExtractFirstAttribute(decl, "version");
					var depPath = ExtractFirstAttribute(decl, "path");

					if (!string.IsNullOrWhiteSpace(depName))
						settings.dependencies[depName] = new DubProjectDependency
						{
							Name = depName,
							Path = depPath,
							Version = depVersion
						};
					break;
				case "targettype":
				case "targetname":
				case "targetpath":
				case "workingdirectory":
				case "mainsourcefile":
					if (decl.Attributes.Length >= 1)
					{
						sett = new DubBuildSetting { Name = propName, Values = new[] { ExtractFirstAttribute(decl) } };
					}
					break;
				case "subconfiguration":
					if (decl.Attributes.Length >= 2)
					{
						var subConfigName = decl.Attributes[0].Item2;
						if (!string.IsNullOrWhiteSpace(subConfigName))
							settings.subConfigurations[subConfigName] = decl.Attributes[1].Item2;
					}
					break;
				case "sourcefiles":
				case "sourcepaths":
				case "excludedsourcefiles":
				case "versions":
				case "debugversions":
				case "importpaths":
				case "stringimportpaths":
					sett = new DubBuildSetting();
					sett.Values = ExtractUnnamedAttributes(decl).ToArray();

					var platformConstraints = ExtractFirstAttribute(decl, "platform").Split('-');
					if (platformConstraints.Length > 0)
					{
						foreach (var constraint in platformConstraints)
						{
							var pn = constraint.ToLowerInvariant();
							if (sett.OperatingSystem == null && DubBuildSettings.OsVersions.Contains(pn))
								sett.OperatingSystem = pn;
							else if (sett.Architecture == null && DubBuildSettings.Architectures.Contains(pn))
								sett.Architecture = pn;
							else
								sett.Compiler = pn;
						}
					}
					break;
			}

			if (sett != null)
			{
				List<DubBuildSetting> setts;
				if (!settings.TryGetValue(propName, out setts))
					settings.Add(propName, setts = new List<DubBuildSetting>());

				setts.Add(sett);
			}
		}

		IEnumerable<string> ExtractUnnamedAttributes(SDLDeclaration d)
		{
			return from attr in d.Attributes
				   where attr.Item1 == null
				   select attr.Item2;
		}

		/// <returns>string.Empty if nothing found</returns>
		string ExtractFirstAttribute(SDLDeclaration d, string key = null)
		{
			var i = d.Attributes.SingleOrDefault((kv) => kv.Item1 == key);
			return i != null ? i.Item2 : string.Empty;
		}
	}
}
