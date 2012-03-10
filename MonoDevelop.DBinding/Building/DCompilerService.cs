using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Xml;
using System.Threading;

namespace MonoDevelop.D.Building
{
	public enum DCompileTarget
	{
		/// <summary>
		/// A normal console application.
		/// </summary>
		Executable,

		/// <summary>
		/// Applications which explicitly draw themselves a custom GUI and do not need a console.
		/// Usually 'Desktop' applications.
		/// </summary>
		ConsolelessExecutable,

		SharedLibrary,
		StaticLibrary
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

			if (_instance == null) {
				_instance = new DCompilerService ();

				CompilerPresets.PresetLoader.LoadPresets (_instance);
			}
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
			get{ return OS.IsWindows ? ".lib" : ".a"; }
		}

		public static string SharedLibraryExtension {
			get{ return OS.IsWindows ? ".dll" : (OS.IsMac ? ".dylib" : ".so");}	
		}

		public static string ObjectExtension {
			get{ return OS.IsWindows ? ".obj" : ".o";}	
		}

		public string DefaultCompiler;

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
					
					var cmp = GetCompiler (vendor) ?? new DCompilerConfiguration { Vendor = vendor};

					cmp.ReadFrom (x.ReadSubtree ());

					Compilers.Add (cmp);
					break;					
					
				case "ResCmp":
					Win32ResourceCompiler.Instance.Load (x.ReadSubtree ());
					break;
				case "DDocBaseUrl":
					MonoDevelop.D.Refactoring.DDocumentationLauncher.DigitalMarsUrl = x.ReadString ();
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
			get{ return !IsMac && !IsLinux;}	
		}
		
		public static bool IsMac {
			get{ return Environment.OSVersion.Platform == PlatformID.MacOSX;}	
		}
		
		public static bool IsLinux {
			get{ return Environment.OSVersion.Platform == PlatformID.Unix;}	
		}
	}
}
