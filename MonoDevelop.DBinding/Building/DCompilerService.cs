using System;
using System.Collections.Generic;
using MonoDevelop.Core;
using System.Xml;
using D_Parser.Misc;

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
		static DCompilerService _instance = null;

		public static DCompilerService Instance {
			get {
				// If not initialized yet, load configuration
				if (!IsInitialized)
					Load ();

				return _instance;
			}
		}

		#region Init/Loading & Saving
		public static void Load ()
		{
			// Deserialize config data
			_instance = PropertyService.Get<DCompilerService> (GlobalPropertyName);

			//LoggingService.AddLogger(new MonoDevelop.Core.Logging.FileLogger("A:\\monoDev.log", true));

			if (_instance == null || _instance.Compilers.Count == 0) {
				_instance = new DCompilerService ();
				_instance.CompletionOptions = CompletionOptions.Default;
				CompilerPresets.PresetLoader.LoadPresets (_instance);
			}

			// Init global parse cache
			_instance.UpdateParseCachesAsync();
		}

		public void Save ()
		{
			PropertyService.Set (GlobalPropertyName, this);
			PropertyService.SaveProperties ();
		}

		const string GlobalPropertyName = "DBinding.DCompiler";
		#endregion
		
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
		public CompletionOptions CompletionOptions;

		public static bool IsInitialized { get { return _instance != null; } }
		
		public readonly List<DCompilerConfiguration> Compilers = new List<DCompilerConfiguration> ();

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

				case "DDocBaseUrl":
					MonoDevelop.D.Refactoring.DDocumentationLauncher.DigitalMarsUrl = x.ReadString ();
					break;

				case "DocumentOutline":
					Outline.Load(x.ReadSubtree());
					break;
					
				case "CompletionOptions":
					CompletionOptions.Load (x.ReadSubtree ());
					break;
					
				case "FormattingCorrectsIndentOnly":
					Formatting.DCodeFormatter.IndentCorrectionOnly = x.ReadString().ToLowerInvariant() == "true";
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
			
			x.WriteStartElement ("DDocBaseUrl");
			x.WriteCData (D.Refactoring.DDocumentationLauncher.DigitalMarsUrl);
			x.WriteEndElement ();
			
			x.WriteStartElement ("CompletionOptions");
			CompletionOptions.Save (x);
			x.WriteEndElement ();

			x.WriteStartElement("DocumentOutline");
			Outline.Save(x);
			x.WriteEndElement();
			
			x.WriteStartElement("FormattingCorrectsIndentOnly");
			x.WriteString(Formatting.DCodeFormatter.IndentCorrectionOnly ? "true" : "false");
			x.WriteEndElement();
		}
		#endregion
	}
	
	public class Win32ResourceCompiler
	{
		public static Win32ResourceCompiler Instance = new Win32ResourceCompiler ();
		public string Executable = "rc.exe";
		public string Arguments = ResourceCompilerDefaultArguments;
		public const string ResourceCompilerDefaultArguments = "/fo \"$res\" \"$rc\"";
		
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
	
	public class OS
	{
		public static bool IsWindows {
			get{ return !IsMac && !IsLinux;} // acceptable here..	
		}
		
		public static bool IsMac {
			get{ return Environment.OSVersion.Platform == PlatformID.MacOSX;}	
		}
		
		public static bool IsLinux {
			get{ return Environment.OSVersion.Platform == PlatformID.Unix;}	
		}
	}
}
