using MonoDevelop.Core.Serialization;
using System.Collections.Generic;
using D_Parser.Completion;
using System.Collections.ObjectModel;
using System;
using System.Xml;
using MonoDevelop.Core;
using System.Threading;

namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Stores compiler commands and arguments for compiling and linking D source files.
	/// </summary>
	public class DCompilerConfiguration
	{
		/// <summary>
		/// Initializes all commands and arguments (also debug&amp;release args!) with default values depending on given target compiler type
		/// </summary>
		public static DCompilerConfiguration CreateWithDefaults(DCompilerVendor Compiler)
		{
			var cfg = new DCompilerConfiguration {  Vendor=Compiler };
			ResetToDefaults(cfg, Compiler);		
			return cfg;
		}
		
		public static void ResetToDefaults(DCompilerConfiguration cfg, DCompilerVendor Compiler)
		{	
			CompilerDefaultArgumentProvider cmp = null;
			switch (Compiler)
			{
				case DCompilerVendor.DMD:
					cmp = new Dmd(cfg);
					break;
				case DCompilerVendor.GDC:
					cmp = new Gdc(cfg);
					break;
				case DCompilerVendor.LDC:
					cmp = new Ldc(cfg);
					break;
			}

			// Reset arguments BEFORE reset compiler commands - only then, the 4 link target config objects will be created.
			cmp.ResetBuildArguments();
			cmp.ResetCompilerConfiguration();						
		}

		public readonly ASTStorage GlobalParseCache = new ASTStorage();
		
		public string BinPath="";
		public DCompilerVendor Vendor;

		protected List<LinkTargetConfiguration> LinkTargetConfigurations = new List<LinkTargetConfiguration>();

		public LinkTargetConfiguration GetTargetConfiguration(DCompileTarget Target)
		{
			foreach (var t in LinkTargetConfigurations)
				if (t.TargetType == Target)
					return t;

			var newTarget = new LinkTargetConfiguration { TargetType=Target };
			LinkTargetConfigurations.Add(newTarget);
			return newTarget;
		}

		public void SetAllCompilerBuildArgs(string NewCompilerArguments, bool AffectDebugArguments)
		{
			foreach (var t in LinkTargetConfigurations)
				t.GetArguments(AffectDebugArguments).CompilerArguments=NewCompilerArguments;
		}

		/// <summary>
		/// Overrides all compiler command strings of all LinkTargetConfigurations
		/// </summary>
		public void SetAllCompilerCommands(string NewCompilerPath)
		{
			foreach (var t in LinkTargetConfigurations)
				t.Compiler = NewCompilerPath;
		}

		public void SetAllLinkerCommands(string NewLinkerPath)
		{
			foreach (var t in LinkTargetConfigurations)
				t.Linker= NewLinkerPath;
		}

		/// <summary>
		/// Updates the configuration's global parse cache
		/// </summary>
		public void UpdateParseCacheAsync()
		{
			UpdateParseCacheAsync(GlobalParseCache);
		}

		public static void UpdateParseCacheAsync(ASTStorage Cache)
		{
			// Return immediately if nothing there to parse
			if (Cache.ParsedGlobalDictionaries.Count < 1)
				return;

			var th = new Thread(() =>
			{
				try
				{
					LoggingService.LogInfo("Update parse cache ({0} directories) - this may take a while!", Cache.ParsedGlobalDictionaries.Count);

					var perfResults = Cache.UpdateCache();

					foreach (var perfData in perfResults)
					{
						LoggingService.LogInfo(
							"Parsed {0} files in \"{1}\" in {2}s (~{3}ms per file)",
							perfData.AmountFiles,
							perfData.BaseDirectory,
							Math.Round(perfData.TotalDuration,3),
							Math.Round( perfData.FileDuration*1000));
					}
				}
				catch (Exception ex)
				{
					LoggingService.LogError("Error while updating parse caches", ex);
				}
			});

			th.IsBackground = true;
			th.Start();
		}

		public List<string> DefaultLibraries = new List<string>();

		#region Loading & Saving
		public void ReadFrom(System.Xml.XmlReader x)
		{
			XmlReader s = null;

			while(x.Read())
				switch (x.LocalName)
				{
					case "BinaryPath":
						BinPath=x.ReadString();
						break;
				
					case "TargetConfiguration":
						s = x.ReadSubtree();

						var t = new LinkTargetConfiguration();
						t.LoadFrom(s);
						LinkTargetConfigurations.Add(t);

						s.Close();
						break;

					case "DefaultLibs":
						s = x.ReadSubtree();
						
						while (s.Read())
							if (s.LocalName == "lib")
								DefaultLibraries.Add(s.ReadString());

						s.Close();
						break;

					case "Includes":
						s = x.ReadSubtree();

						var paths = new List<string>();
						while (s.Read())
							if (s.LocalName == "Path")
								GlobalParseCache.Add(s.ReadString());

						s.Close();
						break;
				}
		}

		public void SaveTo(System.Xml.XmlWriter x)
		{
			x.WriteStartElement("BinaryPath");
			x.WriteCData(BinPath);
			x.WriteEndElement();
			
			foreach (var t in LinkTargetConfigurations)
			{
				x.WriteStartElement("TargetConfiguration");

				t.SaveTo(x);

				x.WriteEndElement();
			}

			x.WriteStartElement("DefaultLibs");
			foreach (var lib in DefaultLibraries)
			{
				x.WriteStartElement("lib");
				x.WriteCData(lib);
				x.WriteEndElement();
			}
			x.WriteEndElement();

			x.WriteStartElement("Includes");
			foreach (var inc in GlobalParseCache.DirectoryPaths)
			{
				x.WriteStartElement("Path");
				x.WriteCData(inc);
				x.WriteEndElement();
			}
			x.WriteEndElement();
		}
		#endregion
	}

	public class LinkTargetConfiguration
	{
		public DCompileTarget TargetType;
		
		public string Compiler;
		public string Linker;

		#region Patterns
		/// <summary>
		/// Describes how each .obj/.o file shall be enumerated in the $objs linking macro
		/// </summary>
		public string ObjectFileLinkPattern = "\"{0}\"";
		/// <summary>
		/// Describes how each include path shall be enumerated in the $includes compiling macro
		/// </summary>
		public string IncludePathPattern = "-I\"{0}\"";
		#endregion

		public BuildConfiguration DebugArguments = new BuildConfiguration();
		public BuildConfiguration ReleaseArguments = new BuildConfiguration();

		public BuildConfiguration GetArguments(bool IsDebug)
		{
			return IsDebug ? DebugArguments : ReleaseArguments;
		}

		public void SaveTo(System.Xml.XmlWriter x)
		{
			x.WriteAttributeString("Target",TargetType.ToString());
			
			x.WriteStartElement("CompilerCommand");
			x.WriteCData(Compiler);
			x.WriteEndElement();

			x.WriteStartElement("LinkerCommand");
			x.WriteCData(Linker);
			x.WriteEndElement();

			x.WriteStartElement("ObjectLinkPattern");
			x.WriteCData(ObjectFileLinkPattern);
			x.WriteEndElement();

			x.WriteStartElement("IncludePathPattern");
			x.WriteCData(IncludePathPattern);
			x.WriteEndElement();

			x.WriteStartElement("DebugArgs");
			DebugArguments.SaveTo(x);
			x.WriteEndElement();

			x.WriteStartElement("ReleaseArgs");
			ReleaseArguments.SaveTo(x);
			x.WriteEndElement();
		}

		public void LoadFrom(System.Xml.XmlReader x)
		{
			if (x.ReadState == ReadState.Initial)
				x.Read();

			if (x.MoveToAttribute("Target"))
				TargetType = (DCompileTarget)Enum.Parse(typeof(DCompileTarget), x.ReadContentAsString());

			while(x.Read())
				switch (x.LocalName)
				{
					case "CompilerCommand":
						Compiler = x.ReadString();
						break;
					case "LinkerCommand":
						Linker = x.ReadString();
						break;
					case "ObjectLinkPattern":
						ObjectFileLinkPattern = x.ReadString();
						break;
					case "IncludePathPattern":
						IncludePathPattern = x.ReadString();
						break;

					case "DebugArgs":
						var s = x.ReadSubtree();
						DebugArguments.ReadFrom(s);
						s.Close();
						break;

					case "ReleaseArgs":
						var s2 = x.ReadSubtree();
						ReleaseArguments.ReadFrom(s2);
						s2.Close();
						break;
				}
		}
	}

	public class BuildConfiguration
	{
		public string CompilerArguments;
		public string LinkerArguments;

		public void SaveTo(System.Xml.XmlWriter x)
		{
			x.WriteStartElement("CompilerArg");
			x.WriteCData(CompilerArguments);
			x.WriteEndElement();

			x.WriteStartElement("LinkerArgs");
			x.WriteCData(LinkerArguments);
			x.WriteEndElement();
		}

		public void ReadFrom(System.Xml.XmlReader x)
		{
			while(x.Read())
				switch (x.LocalName)
				{
					case "CompilerArg":
						CompilerArguments = x.ReadString();
						break;
					case "LinkerArgs":
						LinkerArguments = x.ReadString();
						break;
				}
		}
	}
}

