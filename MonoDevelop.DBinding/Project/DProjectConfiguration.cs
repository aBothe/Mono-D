using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Collections;
using System.IO;

namespace MonoDevelop.D
{
	public enum DCompileTargetType
	{
		Bin,
		SharedLibrary,
		StaticLibrary
	}

	public class DProjectConfiguration:ProjectConfiguration
	{
		[ItemProperty("OutputName")]
		string output = string.Empty;

		[ItemProperty("CompileTarget")]
		DCompileTargetType target = DCompileTargetType.Bin;

		[ItemProperty("Includes")]
		[ItemProperty("Include", Scope = "*", ValueType = typeof(string))]
		private ArrayList includes = new ArrayList();

		[ItemProperty("LibPaths")]
		[ItemProperty("LibPath", Scope = "*", ValueType = typeof(string))]
		private ArrayList libpaths = new ArrayList();

		[ItemProperty("Libs")]
		[ItemProperty("Lib", Scope = "*", ValueType = typeof(string))]
		private ArrayList libs = new ArrayList();

		[ItemProperty("SourcePath")]
		string sourcepath = "";

		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		private string extra_compiler_args = string.Empty;

		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		private string extra_linker_args = string.Empty;




		public string Output
		{
			get { return output; }
			set { output = value; }
		}

		public DCompileTargetType CompileTarget
		{
			get { return target; }
			set { target = value; }
		}

		public ArrayList Includes
		{
			get { return includes; }
			set { includes = value; }
		}

		public ArrayList LibPaths
		{
			get { return libpaths; }
			set { libpaths = value; }
		}

		public ArrayList Libs
		{
			get { return libs; }
			set { libs = value; }
		}

		public string SourcePath
		{
			get { return sourcepath; }
			set { sourcepath = value; }
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
			target = conf.target;
			includes = conf.includes;
			libpaths = conf.libpaths;
			libs = conf.libs;
			extra_compiler_args = conf.extra_compiler_args;
			extra_linker_args = conf.extra_linker_args;
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
							case DCompileTargetType.Bin:
								ext = ".app";
								break;
							case DCompileTargetType.SharedLibrary:
								ext = ".dylib";
								break;
							case DCompileTargetType.StaticLibrary:
								ext = ".a";
								break;
						}
						break;
					case PlatformID.Unix:
						switch (CompileTarget)
						{
							case DCompileTargetType.Bin:
								ext = null;
								break;
							case DCompileTargetType.SharedLibrary:
								ext = ".so";
								break;
							case DCompileTargetType.StaticLibrary:
								ext = ".a";
								break;
						}
						break;
					default:
						switch (CompileTarget)
						{
							case DCompileTargetType.Bin:
								ext = ".exe";
								break;
							case DCompileTargetType.SharedLibrary:
								ext = ".dll";
								break;
							case DCompileTargetType.StaticLibrary:
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
