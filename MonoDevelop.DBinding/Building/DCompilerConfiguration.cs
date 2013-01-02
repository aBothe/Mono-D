using System.Collections.Generic;
using System;
using System.Xml;
using MonoDevelop.Core;
using D_Parser.Misc;

namespace MonoDevelop.D.Building
{
	/// <summary>
	/// Stores compiler commands and arguments for compiling and linking D source files.
	/// </summary>
	public class DCompilerConfiguration
	{
		#region Properties
		public readonly ParseCache ParseCache = new ParseCache ();
		public string BinPath
		{
			get { return ParseCache.FallbackPath ?? ""; }
			set { ParseCache.FallbackPath = value; }
		}
		
		public string Vendor {get;internal set;}
		public string SourceCompilerCommand;
		public readonly CmdLineArgumentPatterns ArgumentPatterns = new CmdLineArgumentPatterns();
		public bool EnableGDCLibPrefixing = false;
		
		public bool HasProfilerSupport
		{
			get { return HasVendorProfilerSupport(Vendor); }
		}
		
		public List<string> DefaultLibraries = new List<string>();
		public readonly Dictionary<DCompileTarget, LinkTargetConfiguration> LinkTargetConfigurations = new Dictionary<DCompileTarget, LinkTargetConfiguration> ();
		/// <summary>
		/// Contains compiler-specific version identifier that is used for better code completion support.
		/// See http://dlang.org/version.html, "Predefined Versions"
		/// </summary>
		public string PredefinedVersionConstant {get; private set;}
		#endregion

		#region Ctor/Init
		public DCompilerConfiguration()
		{
			ParseCache.FinishedParsing += finishedParsing;
			ParseCache.FinishedUfcsCaching += finishedUfcsAnalysis;
		}

		public DCompilerConfiguration(string vendor)
		{
			this.Vendor = vendor;
			ParseCache.FinishedParsing += finishedParsing;
			ParseCache.FinishedUfcsCaching += finishedUfcsAnalysis;
		}
		#endregion

		public static bool HasVendorProfilerSupport(string vendor)
		{
			return vendor == "DMD2" || vendor == "DMD";
		}

		#region Configuration-related methods
		public static bool ResetToDefaults(DCompilerConfiguration cfg)
		{
			return CompilerPresets.PresetLoader.TryLoadPresets(cfg);
		}

		public LinkTargetConfiguration GetOrCreateTargetConfiguration (DCompileTarget Target)
		{
			LinkTargetConfiguration ltc = null;

			if (LinkTargetConfigurations.TryGetValue (Target, out ltc))
				return ltc;

			return LinkTargetConfigurations [Target] = new LinkTargetConfiguration { TargetType = Target };
		}
		#endregion

		#region Parsing stuff
		/// <summary>
		/// Updates the configuration's global parse cache
		/// </summary>
		public void UpdateParseCacheAsync ()
		{
			UpdateParseCacheAsync (ParseCache);
		}

		void finishedParsing(ParsePerformanceData[] pfd)
		{
			foreach (var perfData in pfd)
			{
				LoggingService.LogInfo(
					"Parsed {0} files in \"{1}\" in {2}s (~{3}ms per file)",
					perfData.AmountFiles,
					perfData.BaseDirectory,
					Math.Round(perfData.TotalDuration, 3),
					Math.Round(perfData.FileDuration * 1000));
			}

			if (ParseCache.LastParseException != null)
				LoggingService.LogError("Error while updating parse cache", ParseCache.LastParseException);
		}

		void finishedUfcsAnalysis()
		{
			LoggingService.LogInfo("Finished Ufcs cache preparation in {0}s ({1} parameters parsed, ~{2}ms per resolution)",
				ParseCache.UfcsCache.CachingDuration.TotalSeconds,
				ParseCache.UfcsCache.CachedMethods.Count,
				ParseCache.UfcsCache.CachedMethods.Count==0 ? 0 : Math.Round(ParseCache.UfcsCache.CachingDuration.TotalMilliseconds / ParseCache.UfcsCache.CachedMethods.Count));
		}

		public static void UpdateParseCacheAsync (ParseCache Cache)
		{
			if (Cache == null || Cache.ParsedDirectories == null || Cache.ParsedDirectories.Count < 1)
				return;

			Cache.BeginParse();
		}
		#endregion

		#region Loading & Saving
		/// <summary>
		/// Note: the ParseCache's Root package will NOT be copied but simply assigned to the local root package!
		/// Changes made to the local root package will affect o's Root package!!
		/// </summary>
		/// <param name="o"></param>
		public void AssignFrom (DCompilerConfiguration o)
		{
			Vendor = o.Vendor;
			BinPath = o.BinPath;
			SourceCompilerCommand = o.SourceCompilerCommand;
			ArgumentPatterns.CopyFrom(o.ArgumentPatterns);
			EnableGDCLibPrefixing = o.EnableGDCLibPrefixing;

			ParseCache.ParsedDirectories.Clear ();
			if (o.ParseCache.ParsedDirectories != null)
				ParseCache.ParsedDirectories.AddRange (o.ParseCache.ParsedDirectories);

			ParseCache.Root = o.ParseCache.Root;

			DefaultLibraries.Clear ();
			DefaultLibraries.AddRange (o.DefaultLibraries);

			LinkTargetConfigurations.Clear ();
			foreach (var kv in o.LinkTargetConfigurations) {
				var newLt = new LinkTargetConfiguration ();
				newLt.CopyFrom (kv.Value);
				LinkTargetConfigurations [kv.Key] = newLt;
			}
		}

		public void ReadFrom (System.Xml.XmlReader x)
		{
			XmlReader s = null;

			while (x.Read())
				switch (x.LocalName) {
				case "BinaryPath":
					BinPath = x.ReadString ();
					break;

				case "TargetConfiguration":
					s = x.ReadSubtree ();

					var t = new LinkTargetConfiguration ();
					if(t.LoadFrom (this,s))
						LinkTargetConfigurations [t.TargetType] = t;

					s.Close ();
					break;

				case "DefaultLibs":
					s = x.ReadSubtree ();

					while (s.Read())
						if (s.LocalName == "lib")
							DefaultLibraries.Add (s.ReadString ());

					s.Close ();
					break;

				case "Includes":
					s = x.ReadSubtree ();

					while (s.Read())
						if (s.LocalName == "Path")
							ParseCache.ParsedDirectories.Add (s.ReadString ());

					s.Close ();
					break;

				case "VersionId":
					PredefinedVersionConstant = x.ReadString();
					break;
					
				case "CompilerCommand":
					SourceCompilerCommand = x.ReadString ();
					break;
					
				case "Patterns":
					s = x.ReadSubtree ();
					ArgumentPatterns.ReadFrom (s);
					s.Close ();
					break;
					
				case "gdcLibPrefixing":
					EnableGDCLibPrefixing = x.ReadString() == "true";
					break;
				}
		}

		public void SaveTo (System.Xml.XmlWriter x)
		{
			x.WriteStartElement ("BinaryPath");
			x.WriteCData (BinPath);
			x.WriteEndElement ();

			x.WriteStartElement ("VersionId");
			x.WriteCData (PredefinedVersionConstant);
			x.WriteEndElement ();
			
			x.WriteStartElement ("CompilerCommand");
			x.WriteCData (SourceCompilerCommand);
			x.WriteEndElement ();
			
			x.WriteStartElement ("Patterns");
			ArgumentPatterns.SaveTo(x);
			x.WriteEndElement ();
			
			x.WriteStartElement("gdcLibPrefixing");
			x.WriteString(EnableGDCLibPrefixing ? "true" : "false");
			x.WriteEndElement();

			foreach (var kv in LinkTargetConfigurations) {
				x.WriteStartElement ("TargetConfiguration");

				kv.Value.SaveTo (x);

				x.WriteEndElement ();
			}

			x.WriteStartElement ("DefaultLibs");
			foreach (var lib in DefaultLibraries) {
				x.WriteStartElement ("lib");
				x.WriteCData (lib);
				x.WriteEndElement ();
			}
			x.WriteEndElement ();

			x.WriteStartElement ("Includes");
			foreach (var inc in ParseCache.ParsedDirectories) {
				x.WriteStartElement ("Path");
				x.WriteCData (inc);
				x.WriteEndElement ();
			}
			x.WriteEndElement ();
		}
        #endregion
	}

	public class LinkTargetConfiguration
	{
		public DCompileTarget TargetType;
		public string Linker;

		public BuildConfiguration DebugArguments = new BuildConfiguration ();
		public BuildConfiguration ReleaseArguments = new BuildConfiguration ();

		public BuildConfiguration GetArguments (bool IsDebug)
		{
			return IsDebug ? DebugArguments : ReleaseArguments;
		}

		public void CopyFrom (LinkTargetConfiguration o)
		{
			TargetType = o.TargetType;
			Linker = o.Linker;

			DebugArguments.CopyFrom (o.DebugArguments);
			ReleaseArguments.CopyFrom (o.ReleaseArguments);
		}

		public void SaveTo (System.Xml.XmlWriter x)
		{
			x.WriteAttributeString ("Target", TargetType.ToString ());

			x.WriteStartElement ("LinkerCommand");
			x.WriteCData (Linker);
			x.WriteEndElement ();

			x.WriteStartElement ("DebugArgs");
			DebugArguments.SaveTo (x);
			x.WriteEndElement ();

			x.WriteStartElement ("ReleaseArgs");
			ReleaseArguments.SaveTo (x);
			x.WriteEndElement ();
		}

		public bool LoadFrom (DCompilerConfiguration cmpCfg,System.Xml.XmlReader x)
		{
			if (x.ReadState == ReadState.Initial)
				x.Read ();

			if (x.MoveToAttribute ("Target") && 
			    !Enum.TryParse(x.ReadContentAsString(), true, out TargetType))
					return false;

			while (x.Read())
				switch (x.LocalName) {
				// For backward compatibility keep on parsing this
				case "CompilerCommand":
					cmpCfg.SourceCompilerCommand = x.ReadString ();
					break;
				case "LinkerCommand":
					Linker = x.ReadString ();
					break;
				case "Patterns": // ditto
					var s = x.ReadSubtree ();
					cmpCfg.ArgumentPatterns.ReadFrom (s);
					s.Close ();
					break;
				case "DebugArgs":
					s = x.ReadSubtree ();
					DebugArguments.ReadFrom (cmpCfg, s);
					s.Close ();
					break;
				case "ReleaseArgs":
					s = x.ReadSubtree ();
					ReleaseArguments.ReadFrom (cmpCfg,s);
					s.Close ();
					break;
				}

			return true;
		}
	}

	/// <summary>
	/// Contains patterns that are used to create the argument string for e.g. a compiler or a linker.
	/// Mostly used because GDC and DMD take different arguments.
	/// </summary>
	public class CmdLineArgumentPatterns
	{
		// The patterns will be initialized with the dmd specific
		/// <summary>
		/// Describes how each .obj/.o file shall be enumerated in the $objs linking macro
		/// </summary>
		public string ObjectFileLinkPattern = "\"{0}\"";
		/// <summary>
		/// Describes how each include path shall be enumerated in the $includes compiling macro
		/// </summary>
		public string IncludePathPattern = "-I\"{0}\"";
		public string VersionDefinition = "-version";
		public string DebugDefinition = "-debug";
		public string UnittestFlag = "-unittest";
		public string ProfileFlag = "-profile";

		public string EnableDDocFlag = "-D";
		public string DDocDefinitionFile = "\"{0}\""; // for gdc it's "-fdoc-inc="
		public string DDocExportDirectory = "\"-Dd{0}\"";
		//(probably TODO:) Handle -Df parameter?

		public void CopyFrom(CmdLineArgumentPatterns c)
		{
			ObjectFileLinkPattern = c.ObjectFileLinkPattern;
			IncludePathPattern = c.IncludePathPattern;
			VersionDefinition = c.VersionDefinition;
			DebugDefinition = c.DebugDefinition;
			UnittestFlag = c.UnittestFlag;
			ProfileFlag = c.ProfileFlag;
			EnableDDocFlag = c.EnableDDocFlag;
			DDocDefinitionFile = c.DDocDefinitionFile;
			DDocExportDirectory = c.DDocExportDirectory;
		}

		public void SaveTo(XmlWriter x)
		{
			x.WriteStartElement("obj");
			x.WriteCData(ObjectFileLinkPattern);
			x.WriteEndElement();

			x.WriteStartElement("include");
			x.WriteCData(IncludePathPattern);
			x.WriteEndElement();

			x.WriteStartElement("version");
			x.WriteCData(VersionDefinition);
			x.WriteEndElement();

			x.WriteStartElement("debug");
			x.WriteCData(DebugDefinition);
			x.WriteEndElement();

			x.WriteStartElement("unittest");
			x.WriteCData(UnittestFlag);
			x.WriteEndElement();
			
			x.WriteStartElement("profile");
			x.WriteCData(ProfileFlag);
			x.WriteEndElement();

			x.WriteStartElement("ddFlag");
			x.WriteCData(EnableDDocFlag);
			x.WriteEndElement();

			x.WriteStartElement("ddMacroDefinition");
			x.WriteCData(DDocDefinitionFile);
			x.WriteEndElement();

			x.WriteStartElement("ddDir");
			x.WriteCData(DDocExportDirectory);
			x.WriteEndElement();
		}

		public void ReadFrom(XmlReader x)
		{
			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "obj":
						ObjectFileLinkPattern = x.ReadString();
						break;
					case "include":
						IncludePathPattern = x.ReadString();
						break;
					case "version":
						VersionDefinition = x.ReadString();
						break;
					case "debug":
						DebugDefinition = x.ReadString();
						break;
					case "unittest":
						UnittestFlag = x.ReadString();
						break;
				case "profile":
					ProfileFlag = x.ReadString();
					break;
					case "ddFlag":
						EnableDDocFlag = x.ReadString();
						break;
					case "ddMacroDefinition":
						DDocDefinitionFile = x.ReadString();
						break;
					case "ddDir":
						DDocExportDirectory = x.ReadString();
						break;
				}
			}
		}
	}

	public class BuildConfiguration
	{
		public string CompilerArguments;
		public string LinkerArguments;

		/// <summary>
		/// An argument string that is used for compiling & linking all project files together at once
		/// </summary>
		public string OneStepBuildArguments;
		
		public bool SupportsOneStepBuild {
			get { return !string.IsNullOrEmpty (OneStepBuildArguments); }
		}
		
		public void CopyFrom (BuildConfiguration o)
		{
			CompilerArguments = o.CompilerArguments;
			LinkerArguments = o.LinkerArguments;
			OneStepBuildArguments = o.OneStepBuildArguments;
		}

		public BuildConfiguration Clone ()
		{
			return new BuildConfiguration{
				CompilerArguments=CompilerArguments,
				LinkerArguments=LinkerArguments,
				OneStepBuildArguments=OneStepBuildArguments
			};	
		}

		public void SaveTo (XmlWriter x)
		{
			x.WriteStartElement ("CompilerArg");
			x.WriteCData (CompilerArguments);
			x.WriteEndElement ();

			x.WriteStartElement ("LinkerArgs");
			x.WriteCData (LinkerArguments);
			x.WriteEndElement ();

			x.WriteStartElement ("OneStepBuildArgs");
			x.WriteCData (OneStepBuildArguments);
			x.WriteEndElement ();
		}

		public void ReadFrom (DCompilerConfiguration cmpCfg,XmlReader x)
		{
			while (x.Read())
				switch (x.LocalName) {
				case "CompilerArg":
					CompilerArguments = x.ReadString ();
					break;
				case "LinkerArgs":
					LinkerArguments = x.ReadString ();
					break;
				case "OneStepBuildArgs":
					OneStepBuildArguments = x.ReadString ();
					break;
				// Legacy support
				case "gdcLibPrefixing":
					cmpCfg.EnableGDCLibPrefixing = x.ReadString() == "true";
					break;
				}
		}
	}
}

