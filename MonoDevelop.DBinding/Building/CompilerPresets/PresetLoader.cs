using System;
using System.Xml;
using MonoDevelop.D.Building;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;

namespace MonoDevelop.D.Building.CompilerPresets
{
	public class PresetLoader
	{
		public static string GetString(Assembly ass, string name)
		{
			using (var s = ass.GetManifestResourceStream (name))
			using (var r = new System.IO.StreamReader (s))
				return r.ReadToEnd ();
		}

		static PresetLoader()
		{
			var ass = typeof(PresetLoader).Assembly;

			presetFileContents ["DMD2"] = presetFileContents["DMD"] = GetString (ass,"CompilerPresets.dmd.xml");
			presetFileContents ["GDC"] = GetString (ass,"CompilerPresets.gdc.xml");
			presetFileContents ["ldc2"] = GetString (ass,"CompilerPresets.ldc2.xml");
		}

		static Dictionary<string, string> presetFileContents = new Dictionary<string, string>();

		public static void LoadPresets(DCompilerService svc)
		{
			foreach (var kv in presetFileContents)
			{
				var cmp = LoadFromString(kv.Value);
				cmp.Vendor = kv.Key;

				svc.Compilers.Add(cmp);
			}

			svc.DefaultCompiler = "DMD2";
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
						compiler.IncludePaths.Clear();

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
			cfg.SourceCompilerCommand = Path.ChangeExtension(cfg.SourceCompilerCommand, DCompilerService.ExecutableExtension);

			foreach (var kv in cfg.LinkTargetConfigurations)
			{
                var lt = kv.Value;
				lt.Linker = Path.ChangeExtension(lt.Linker,DCompilerService.ExecutableExtension);
			}
		}
	}
}

