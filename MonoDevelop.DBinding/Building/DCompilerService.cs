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

	public enum DCompilerVendor
	{
		DMD,
		GDC,
		LDC
	}

	/// <summary>
	/// Central class which enables build support for D projects in MonoDevelop.
	/// </summary>
	public class DCompilerService : ICustomXmlSerializer
	{
		static DCompiler _instance = null;
		public static DCompiler Instance
		{
			get
			{
				// If not initialized yet, load configuration
				if (!IsInitialized)
					Load();

				return _instance;
			}
		}

		#region Init/Loading & Saving
		public static void Load()
		{
			// Deserialize config data
			_instance=PropertyService.Get<DCompiler>(GlobalPropertyName);

			//LoggingService.AddLogger(new MonoDevelop.Core.Logging.FileLogger("A:\\monoDev.log", true));

			if (_instance == null)
				_instance = new DCompiler
				{
					Dmd = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.DMD),
					Gdc = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.GDC),
					Ldc = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.LDC)
				};
		}

		public void Save()
		{
			PropertyService.Set(GlobalPropertyName, this);
			PropertyService.SaveProperties();
		}

		const string GlobalPropertyName = "DBinding.DCompiler";
		#endregion
		
		public static string ExecutableExtension
		{
			get{ return OS.IsWindows?".exe":(OS.IsMac?".app":null);}	
		}
		public static string StaticLibraryExtension
		{
			get{ return OS.IsWindows?".lib":".a"; }
		}
		public static string SharedLibraryExtension
		{
			get{return OS.IsWindows?".dll":(OS.IsMac?".dylib":".so");}	
		}
		public static string ObjectExtension
		{
			get{return OS.IsWindows?".obj":".o";}	
		}

		public DCompilerVendor DefaultCompiler = DCompilerVendor.DMD;

		public static bool IsInitialized { get { return _instance != null; } }

		/// <summary>
		/// Static object which stores all global information about the dmd installation which probably exists on the programmer's machine.
		/// </summary>
		public DCompilerConfiguration Dmd = new DCompilerConfiguration { Vendor = DCompilerVendor.DMD };
		public DCompilerConfiguration Gdc = new DCompilerConfiguration { Vendor = DCompilerVendor.GDC };
		public DCompilerConfiguration Ldc = new DCompilerConfiguration { Vendor = DCompilerVendor.LDC };

		public IEnumerable<DCompilerConfiguration> Compilers
		{
			get { return new[] { Dmd,Gdc,Ldc }; }
		}

		public void UpdateParseCachesAsync()
		{
			foreach (var cmp in Compilers)
				cmp.UpdateParseCacheAsync();
		}

		/// <summary>
		/// Returns the default compiler configuration
		/// </summary>
		public DCompilerConfiguration GetDefaultCompiler()
		{
			return GetCompiler(DefaultCompiler);
		}

		public DCompilerConfiguration GetCompiler(DCompilerVendor type)
		{
			switch (type)
			{
				case DCompilerVendor.GDC:
					return Gdc;
				case DCompilerVendor.LDC:
					return Ldc;
			}

			return Dmd;
		}
		
		#region Loading & Saving
		public ICustomXmlSerializer ReadFrom(XmlReader x)
		{
			if (!x.Read())
				return this;

			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "DefaultCompiler":
						if (x.MoveToAttribute("Name"))
							DefaultCompiler = (DCompilerVendor)Enum.Parse(typeof(DCompilerVendor), x.ReadContentAsString());
						break;

					case "Compiler":
						var vendor = DCompilerVendor.DMD;

						if (x.MoveToAttribute("Name"))
						{
							vendor = (DCompilerVendor)Enum.Parse(typeof(DCompilerVendor), x.ReadContentAsString());

							x.MoveToElement();
						}

						var cmp=GetCompiler(vendor);
						cmp.Vendor = vendor;

						cmp.ReadFrom(x.ReadSubtree());
						break;
					
					
				case "ResCmp":
					Win32ResourceCompiler.Instance.Load( x.ReadSubtree());
					break;
				}
			}

			return this;
		}

		public void WriteTo(XmlWriter x)
		{
			x.WriteStartElement("DefaultCompiler");
			x.WriteAttributeString("Name", DefaultCompiler.ToString());
			x.WriteEndElement();

			foreach (var cmp in Compilers)
			{
				x.WriteStartElement("Compiler");
				x.WriteAttributeString("Name", cmp.Vendor.ToString());

				cmp.SaveTo(x);

				x.WriteEndElement();
			}
			
			x.WriteStartElement("ResCmp");
			Win32ResourceCompiler.Instance.Save(x);
			x.WriteEndElement();
		}
		#endregion
	}
	
	public class Win32ResourceCompiler
	{
		public static Win32ResourceCompiler Instance = new Win32ResourceCompiler();

		public string Executable="rc.exe";
		public string Arguments=ResourceCompilerDefaultArguments;

		public const string ResourceCompilerDefaultArguments = "/fo \"$res\" \"$rc\"";
		
		public void Load(XmlReader x)
		{
			while(x.Read())
			{
				switch(x.LocalName)
				{
				case "exe":
					Executable=x.ReadString();
					break;
				
				case "args":
					Arguments=x.ReadString();
					break;
				}
			}
		}
		
		public void Save(XmlWriter x)
		{
			x.WriteStartElement("exe");
			x.WriteCData(Executable);
			x.WriteEndElement();
			
			x.WriteStartElement("args");
			x.WriteCData(Arguments);
			x.WriteEndElement();
		}
		
		public class ArgProvider:IArgumentMacroProvider
		{
			public string RcFile;
			public string ResFile;
			
			public string Replace (string Input)
			{
				switch(Input)
				{
				case "rc":
					return RcFile;
					
				case "res":
					return ResFile;
				}
				return null;
			}
		}
	}
	
	public class OS
	{
		public static bool IsWindows
		{
			get{return !IsMac && !IsLinux;}	
		}
		
		public static bool IsMac{
			get{ return Environment.OSVersion.Platform==PlatformID.MacOSX;}	
		}
		
		public static bool IsLinux{
			get{return Environment.OSVersion.Platform==PlatformID.Unix;}	
		}
	}
}
