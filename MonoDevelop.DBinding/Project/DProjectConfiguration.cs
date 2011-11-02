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
		#region Properties
		public DProject Project
		{
			get;protected set;
		}
		
		[ItemProperty("OutputName")]
		public string Output="";

		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		public string ExtraCompilerArguments = "";

		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		public string ExtraLinkerArguments = "";
		#endregion
		
		public DProjectConfiguration(DProject Project)
		{
			this.Project=Project;
		}

		public event EventHandler Changed;
		
		public override void CopyFrom(ItemConfiguration configuration)
		{
			base.CopyFrom(configuration);
			var conf = configuration as DProjectConfiguration;

			if (conf == null)
				return;

			Output=conf.Output;
			ExtraCompilerArguments=conf.ExtraCompilerArguments;
			ExtraLinkerArguments=conf.ExtraLinkerArguments;
			
			if (Changed != null)
				Changed(this, new EventArgs());
			
		}

		/// <summary>
		/// TODO: Ensure correctness of the extensions!
		/// </summary>
		public string CompiledOutputName
		{
			get {
				var prj=Project;
				
				if(prj!=null)
				{
				var ext = "";

				switch (Environment.OSVersion.Platform)
				{
					case PlatformID.MacOSX:
						switch (prj.CompileTarget)
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
						switch (prj.CompileTarget)
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
						switch (prj.CompileTarget)
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
				return Output;
			}
		}
	}
}
