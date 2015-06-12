using System;
using System.Collections.Generic;
using MonoDevelop.Core;
using System.Xml;
using D_Parser.Misc;
using MonoDevelop.D.Unittest;
using System.IO;

namespace MonoDevelop.D.Building
{
	public enum DCompileTarget
	{
		Executable,
		SharedLibrary,
		StaticLibrary
	}

	public enum DocOutlineCollapseBehaviour
	{
		CollapseAll,
		ReopenPreviouslyExpanded,
		ExpandAll
	}
	
	public class DDocumentOutlineOptions
	{
		public bool ShowFuncParams = true;
		public bool ShowFuncVariables = true;
		public bool ShowBaseTypes;
		public bool GrayOutNonPublic = true;
		public DocOutlineCollapseBehaviour ExpansionBehaviour = DocOutlineCollapseBehaviour.CollapseAll;

		public void Load (XmlReader x)
		{
			if(x.MoveToAttribute("ShowParameters"))
				ShowFuncParams = Boolean.Parse(x.ReadContentAsString());
			if(x.MoveToAttribute("ShowVariables"))
				ShowFuncVariables = Boolean.Parse(x.ReadContentAsString());
			if(x.MoveToAttribute("ShowTypes"))
				ShowBaseTypes = Boolean.Parse(x.ReadContentAsString());
			if(x.MoveToAttribute("GrayOutNonPublic"))
				GrayOutNonPublic = Boolean.Parse(x.ReadContentAsString());
			if(x.MoveToAttribute("ExpansionBehaviour"))
				Enum.TryParse(x.ReadContentAsString(),out ExpansionBehaviour);
		}
		
		public void Save (XmlWriter x)
		{
			x.WriteAttributeString("ShowParameters", ShowFuncParams.ToString());
			x.WriteAttributeString("ShowVariables", ShowFuncVariables.ToString());
			x.WriteAttributeString("ShowTypes", ShowBaseTypes.ToString());
			x.WriteAttributeString("GrayOutNonPublic", GrayOutNonPublic.ToString());
			x.WriteAttributeString("ExpansionBehaviour", ExpansionBehaviour.ToString());
		}
	}
	
	/// <summary>
	/// Central class which enables build support for D projects in MonoDevelop.
	/// </summary>
	public class DCompilerService : ICustomXmlSerializer
	{
		#region Properties
		static DCompilerService _instance = null;

		public static DCompilerService Instance {
			get {
				// If not initialized yet, load configuration
				if (!IsInitialized)
					Load ();

				return _instance;
			}
		}

		public static string ExecutableExtension {
			get{ return OS.IsWindows ? ".exe" : (OS.IsMac ? ".app" : null);}	
		}

		public static string StaticLibraryExtension {
			get{ return OS.IsWindows ? ".lib" : ".a"; } //FIXME: This is not correct: GDC on windows surely requires .a files..
		}

		public static string SharedLibraryExtension {
			get{ return OS.IsWindows ? ".dll" : (OS.IsMac ? ".dylib" : ".so");}	
		}

		public static string ObjectExtension {
			get{ return OS.IsWindows ? ".obj" : ".o";} //FIXME: Same here. ".o" object files may be linked in mingw environments..
		}

		public DDocumentOutlineOptions Outline = new DDocumentOutlineOptions();

		public string DefaultCompiler;

		public static bool IsInitialized { get { return _instance != null; } }

		public readonly List<DCompilerConfiguration> Compilers = new List<DCompilerConfiguration> ();
		#endregion

		#region Init/Loading & Saving
		public static void Load ()
		{
			// Deserialize config data
			_instance = PropertyService.Get<DCompilerService> (GlobalPropertyName);

			//LoggingService.AddLogger(new MonoDevelop.Core.Logging.FileLogger("A:\\monoDev.log", true));

			if (_instance == null || _instance.Compilers.Count == 0) {
				_instance = new DCompilerService ();
				CompilerPresets.PresetLoader.LoadPresets (_instance);
				LoadDefaultDmd2Paths (_instance.GetCompiler ("DMD2"));
			}
				
			if (Ide.IdeApp.Workspace != null) 
				Ide.IdeApp.Workbench.RootWindow.Destroyed += (o, ea) => _instance.Save ();

			// Init global parse cache
			_instance.UpdateParseCachesAsync();
		}

		public void Save ()
		{
			DubSettings.Save ();
			PropertyService.Set (GlobalPropertyName, this);
			PropertyService.SaveProperties ();
		}

		const string GlobalPropertyName = "DBinding.DCompiler";

		static void LoadDefaultDmd2Paths(DCompilerConfiguration cmp)
		{
			if (cmp == null)
				return;

			if (OS.IsWindows) {
				foreach (var drv in Directory.GetLogicalDrives()) {
					string dir, p;

					if (string.IsNullOrEmpty(cmp.BinPath))
					{
						dir = Path.Combine(drv, "D\\dmd2\\windows\\bin");
						if (Directory.Exists(dir))
							cmp.BinPath = dir;
					}

					dir = Path.Combine (drv,"D\\dmd2\\src");
					p = Path.Combine (dir, "druntime\\import");
					if (Directory.Exists (p))
						cmp.IncludePaths.Add (p);
					p = Path.Combine (dir, "phobos");
					if (Directory.Exists(p))
					{
						cmp.IncludePaths.Add(p);
						break;
					}
				}
				return;
			}

			Dictionary<string,string> defaults;

			if (OS.IsLinux)
				defaults = new Dictionary<string,string> {
				{ "/usr/local/include/dlang/dmd/",null },
				{ "/usr/include/dlang/dmd/",null },
				{ "/usr/include/dmd", null },
				{ "/usr/local/include/dmd", null },
				};
			else if (OS.IsMac)
				defaults = new Dictionary<string,string> {
				{ "/usr/share/dmd/src/druntime/import", "/usr/share/dmd/src/phobos" },
				{ "/usr/local/opt/dmd/libexec/src/druntime", "/usr/local/opt/dmd/libexec/src/phobos" },
				{ "/usr/opt/dmd/libexec/src/druntime", "/usr/opt/dmd/libexec/src/phobos" },
				{ "/opt/dmd/libexec/src/druntime", "/opt/dmd/libexec/src/phobos" },
				};
			else
				return;

			foreach (var kv in defaults) {
				if (Directory.Exists (kv.Key)) {
					cmp.IncludePaths.Add (kv.Key);
					if (kv.Value != null && Directory.Exists (kv.Value))
						cmp.IncludePaths.Add (kv.Value);
				}
			}
		}
		#endregion

		public void UpdateParseCachesAsync ()
		{
			foreach (var cmp in Compilers)
				cmp.UpdateParseCacheAsync ();
		}

		/// <summary>
		/// Returns the default compiler configuration
		/// </summary>
		public DCompilerConfiguration GetDefaultCompiler ()
		{			
			return GetCompiler (DefaultCompiler);
		}

		public DCompilerConfiguration GetCompiler (string vendor)
		{
			foreach (var cmp in Compilers)
				if (cmp.Vendor == vendor)
					return cmp;
			
			return null;
		}
		
		#region Loading & Saving
		public ICustomXmlSerializer ReadFrom (XmlReader x)
		{
			if (!x.Read ())
				return this;

			while (x.Read()) {
				switch (x.LocalName) {
				case "DefaultCompiler":
					if (x.MoveToAttribute ("Name"))
						DefaultCompiler = x.ReadContentAsString ();
					else
						DefaultCompiler = x.ReadString ();
					break;

				case "Compiler":
					var vendor = "";

					if (x.MoveToAttribute ("Name")) {
						vendor = x.ReadContentAsString ();

						x.MoveToElement ();
					}
					
					var cmp = GetCompiler (vendor) ?? new DCompilerConfiguration(vendor);

					cmp.ReadFrom (x.ReadSubtree ());

					Compilers.Add (cmp);
					break;					
					
				case "ResCmp":
					Win32ResourceCompiler.Instance.Load (x.ReadSubtree ());
					break;

				case "DocumentOutline":
					Outline.Load(x.ReadSubtree());
					break;
					
				case "CompletionOptions":
					CompletionOptions.Instance.Load (x.ReadSubtree ());
					break;
					
				case "FormattingCorrectsIndentOnly":
					Formatting.DCodeFormatter.IndentCorrectionOnly = x.ReadString().ToLowerInvariant() == "true";
					break;
					
				case "UnittestSettings":
					UnittestSettings.Load (x.ReadSubtree ());
					break;
				}
			}

			return this;
		}

		public void WriteTo (XmlWriter x)
		{
			x.WriteStartElement ("DefaultCompiler");
			x.WriteString (DefaultCompiler);
			x.WriteEndElement ();

			foreach (var cmp in Compilers) {
				x.WriteStartElement ("Compiler");
				x.WriteAttributeString ("Name", cmp.Vendor);

				cmp.SaveTo (x);

				x.WriteEndElement ();
			}
			
			x.WriteStartElement ("ResCmp");
			Win32ResourceCompiler.Instance.Save (x);
			x.WriteEndElement ();
			
			x.WriteStartElement ("CompletionOptions");
			CompletionOptions.Instance.Save (x);
			x.WriteEndElement ();

			x.WriteStartElement("DocumentOutline");
			Outline.Save(x);
			x.WriteEndElement();
			
			x.WriteStartElement("FormattingCorrectsIndentOnly");
			x.WriteString(Formatting.DCodeFormatter.IndentCorrectionOnly ? "true" : "false");
			x.WriteEndElement();

			x.WriteStartElement ("UnittestSettings");
			UnittestSettings.Save(x);
			x.WriteEndElement();
		}
		#endregion
	}
	
	public class Win32ResourceCompiler
	{
		public static Win32ResourceCompiler Instance = new Win32ResourceCompiler ();
		public string Executable = "rc.exe";
		public string Arguments = ResourceCompilerDefaultArguments;
		public const string ResourceCompilerDefaultArguments = "/nologo /fo \"$res\" \"$rc\"";
		
		public void Load (XmlReader x)
		{
			while (x.Read()) {
				switch (x.LocalName) {
				case "exe":
					Executable = x.ReadString ();
					break;
				
				case "args":
					Arguments = x.ReadString ();
					break;
				}
			}
		}
		
		public void Save (XmlWriter x)
		{
			x.WriteStartElement ("exe");
			x.WriteCData (Executable);
			x.WriteEndElement ();
			
			x.WriteStartElement ("args");
			x.WriteCData (Arguments);
			x.WriteEndElement ();
		}
		
		public class ArgProvider:IArgumentMacroProvider
		{
			public string RcFile;
			public string ResFile;
			
			public void ManipulateMacros (Dictionary<string, string> macros)
			{
				macros ["rc"] = RcFile;
				macros ["res"] = ResFile;
			}
		}
	}
	
	public static class OS
	{
		public static bool IsWindows {
			get{ return Environment.OSVersion.Platform == PlatformID.Win32NT; } // acceptable here..	
		}

		static string unameResult;
		static string Uname
		{
			get{
				if(Environment.OSVersion.Platform != PlatformID.Unix)
					return null;

				if (unameResult == null) {
					using (var unameProc = System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo ("uname") {
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true
					})) {
						unameProc.WaitForExit (1000);
						unameResult = unameProc.StandardOutput.ReadLine ();
					}
				}

				return unameResult;
			}
		}
		
		public static bool IsMac {
			get { return Uname == "Darwin";	}
		}
		
		public static bool IsLinux {
			get{ return Uname == "Linux"; }	
		}
	}
}
