using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Collections;
using System.IO;
using MonoDevelop.D.Building;

namespace MonoDevelop.D
{
	public class DProjectConfiguration:ProjectConfiguration
	{
		[ItemProperty("OutputName")]
		string output = string.Empty;

		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		private string extra_compiler_args = string.Empty;

		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		private string extra_linker_args = string.Empty;

		public event EventHandler Changed;
		
		public string Output
		{
			get { return output; }
			set { output = value; }
		}

		public string ExtraCompilerArguments
		{
			get { return extra_compiler_args; }
			set { extra_compiler_args = value; }
		}

		public string ExtraLinkerArguments
		{
			get { return extra_linker_args; }
			set { extra_linker_args = value; }
		}



		public override void CopyFrom(ItemConfiguration configuration)
		{
			base.CopyFrom(configuration);
			var conf = configuration as DProjectConfiguration;

			if (conf == null)
				return;

			output = conf.output;
			extra_compiler_args = conf.extra_compiler_args;
			extra_linker_args = conf.extra_linker_args;
			
			if (Changed != null)
				Changed(this, new EventArgs());
			
		}

		/// <summary>
		/// TODO: Ensure correctness of the extensions!
		/// </summary>
		public string CompiledOutputName
		{
			get {
				var ext = "";

				switch (Environment.OSVersion.Platform)
				{
					case PlatformID.MacOSX:
						switch (CompileTarget)
						{
							case DCompileTarget.ConsolelessExecutable:
							case DCompileTarget.Executable:
								ext = ".app";
								break;
							case DCompileTarget.SharedLibrary:
								ext = ".dylib";
								break;
							case DCompileTarget.StaticLibrary:
								ext = ".a";
								break;
						}
						break;
					case PlatformID.Unix:
						switch (CompileTarget)
						{
							case DCompileTarget.ConsolelessExecutable:
							case DCompileTarget.Executable:
								ext = null;
								break;
							case DCompileTarget.SharedLibrary:
								ext = ".so";
								break;
							case DCompileTarget.StaticLibrary:
								ext = ".a";
								break;
						}
						break;
					default:
						switch (CompileTarget)
						{
							case DCompileTarget.ConsolelessExecutable:
							case DCompileTarget.Executable:
								ext = ".exe";
								break;
							case DCompileTarget.SharedLibrary:
								ext = ".dll";
								break;
							case DCompileTarget.StaticLibrary:
								ext = ".lib";
								break;
						}
						break;
				}

				return Path.ChangeExtension(Output, ext);
			}
		}
	}
}
