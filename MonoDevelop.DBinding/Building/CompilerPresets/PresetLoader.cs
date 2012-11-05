using System;
using System.Xml;
using MonoDevelop.D.Building;
using System.IO;
using System.Collections.Generic;

namespace MonoDevelop.D.Building.CompilerPresets
{
	public class PresetLoader
	{
		static Dictionary<string, string> presetFileContents = new Dictionary<string, string> {
			{"DMD2", ConfigPresets.dmd}, //TODO: Make specific preset for dmd2/1
			{"DMD", ConfigPresets.dmd},
			{"GDC", ConfigPresets.gdc},
			{"LDC", ConfigPresets.ldc},
			{"ldc2", ConfigPresets.ldc2}
		};

		public static void LoadPresets(DCompilerService svc)
		{
			foreach (var kv in presetFileContents)
			{
				var cmp = LoadFromString(kv.Value);
				cmp.Vendor = kv.Key;

				svc.Compilers.Add(cmp);
			}

			svc.DefaultCompiler = svc.Compilers[0].Vendor;
		}

		public static bool HasPresetsAvailable(DCompilerConfiguration compiler)
		{
			return HasPresetsAvailable(compiler.Vendor);
		}

		public static bool HasPresetsAvailable(string vendor)
		{
			foreach (var kv in presetFileContents)
				if (kv.Key == vendor)
					return true;

			return false;
		}

		public static bool TryLoadPresets(DCompilerConfiguration compiler)
		{
			if(compiler!=null)
				foreach (var kv in presetFileContents)
				{
					if (kv.Key == compiler.Vendor)
					{
						var x = new XmlTextReader(new StringReader(kv.Value));
						x.Read();

						compiler.DefaultLibraries.Clear();
						compiler.ParseCache.Clear();

						compiler.ReadFrom(x);

						x.Close();
						FitFileExtensions(compiler);
						return true;
					}
				}

			return false;
		}

		public static DCompilerConfiguration LoadFromString(string xmlCode)
		{
			var cmp = new DCompilerConfiguration();

			var x = new XmlTextReader(new StringReader(xmlCode));

			if (x.ReadToFollowing("Compiler"))
			{
				if (x.MoveToAttribute("Name"))
				{
					cmp.Vendor = x.ReadContentAsString();
					x.MoveToElement();
				}

				cmp.ReadFrom(x);
			}

			x.Close();

			FitFileExtensions(cmp);

			return cmp;
		}

		/// <summary>
		/// Call this method to make all file paths etc. existing in a compiler 
		/// config (like phobos.lib or dmd.exe) fit to the target OS properly.
		/// </summary>
		public static void FitFileExtensions(DCompilerConfiguration cfg)
		{
			for (int i = 0; i < cfg.DefaultLibraries.Count; i++)
				cfg.DefaultLibraries[i] = Path.ChangeExtension(cfg.DefaultLibraries[i], DCompilerService.StaticLibraryExtension);

			foreach (var kv in cfg.LinkTargetConfigurations)
			{
                var lt = kv.Value;
				lt.Compiler = Path.ChangeExtension(lt.Compiler, DCompilerService.ExecutableExtension);
				lt.Linker = Path.ChangeExtension(lt.Linker,DCompilerService.ExecutableExtension);
			}
		}
	}
}

