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
		public DProject Project {
			get;
			protected set;
		}
		
		[ItemProperty("OutputName")]
		public string Output = "";
		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		public string ExtraCompilerArguments = "";
		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		public string ExtraLinkerArguments = "";
		[ItemProperty("Libs")]
		[ItemProperty("Lib", Scope = "*")]
		public List<string> ExtraLibraries = new List<string> ();
		[ItemProperty("ObjectsDirectory", DefaultValue="obj")]
		public string ObjectDirectory = "obj";
		#endregion

		//if absent an exception occurs when opening project config	
		public DProjectConfiguration ()
		{
		}
		
		public DProjectConfiguration (DProject Project)
		{
			this.Project = Project;
		}

		public event EventHandler Changed;
		
		public override void CopyFrom (ItemConfiguration configuration)
		{
			base.CopyFrom (configuration);
			var conf = configuration as DProjectConfiguration;

			if (conf == null)
				return;

			ObjectDirectory = conf.ObjectDirectory;
			Output = conf.Output;
			ExtraCompilerArguments = conf.ExtraCompilerArguments;
			ExtraLinkerArguments = conf.ExtraLinkerArguments;

            ExtraLibraries.Clear();
            ExtraLibraries.AddRange(conf.ExtraLibraries);
			
			if (Changed != null)
				Changed (this, new EventArgs ());
		}

		/// <summary>
		/// TODO: Ensure correctness of the extensions!
		/// </summary>
		public string CompiledOutputName {
			get {
				if (Project != null) {
					var ext = "";
						
					switch (Project.CompileTarget) {
					case DCompileTarget.SharedLibrary:
						ext = DCompilerService.SharedLibraryExtension;
						break;
					case DCompileTarget.StaticLibrary:
						ext = DCompilerService.StaticLibraryExtension;
						break;
					default:
						ext = DCompilerService.ExecutableExtension;
						break;
					}
	
					return Path.ChangeExtension (ProjectBuilder.EnsureCorrectPathSeparators (Output), ext);
				}
				return ProjectBuilder.EnsureCorrectPathSeparators (Output);
			}
		}
	}
}
