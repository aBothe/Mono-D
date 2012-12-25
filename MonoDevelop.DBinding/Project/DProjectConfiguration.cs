using System;
using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core.Serialization;
using MonoDevelop.D.Building;
using MonoDevelop.Projects;
using MonoDevelop.Core;

namespace MonoDevelop.D
{
	public class DProjectConfiguration:ProjectConfiguration
	{
		#region Properties
		public DProject Project
		{
			get { return ParentItem as DProject; }
		}

		[ItemProperty("Target")]
		public DCompileTarget CompileTarget = DCompileTarget.Executable;
		[ItemProperty("OutputName")]
		public string Output = "";
		[ItemProperty("UnittestMode")]
		public bool UnittestMode = false;
		[ItemProperty("ProfilerMode")]
		public bool ProfilerMode = false;	
		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		public string ExtraCompilerArguments = "";
		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		public string ExtraLinkerArguments = "";
		[ItemProperty("Libs")]
		[ItemProperty("Lib", Scope = "*")]
		public List<string> ExtraLibraries = new List<string> ();
		[ItemProperty("ObjectsDirectory", DefaultValue="obj")]
		public string ObjectDirectory = "obj";
		[ItemProperty("DDocDirectory", DefaultValue = "doc")]
		public string DDocDirectory = "doc";

		/// <summary>
		/// Returns all libs that are included by default both by the compiler and this specific build config
 		/// </summary>
 		public IEnumerable<string> GetReferencedLibraries(ConfigurationSelector configSelector)
		{
			foreach (var i in Project.Compiler.DefaultLibraries)
				yield return i;
			foreach (var i in ExtraLibraries)
				yield return i;
			
			bool takeDefSelector = configSelector == null;
			foreach (var dep in DProject.GetSortedProjectDependencies(Project))
			{
				if(takeDefSelector)
					configSelector = dep.DefaultConfiguration.Selector;
				
				var activeConfig = dep.GetConfiguration(configSelector) as DProjectConfiguration;

				if (activeConfig != null){
					if(activeConfig.CompileTarget == DCompileTarget.StaticLibrary)
						yield return dep.GetOutputFileName(configSelector);
					// Assume there is an import lib inside the dll's output directory and add it to the linked-in libs
					// https://github.com/aBothe/Mono-D/issues/180
					else if(OS.IsWindows && 
					        activeConfig.CompileTarget == DCompileTarget.SharedLibrary)
					{
						var lib = Path.ChangeExtension(dep.GetOutputFileName(configSelector), DCompilerService.StaticLibraryExtension);
						if(File.Exists(lib))
							yield return lib;
					}
				}
			}
		}

		[ItemProperty("VersionIds")]
		public string[] CustomVersionIdentifiers;
		[ItemProperty("DebugIds")]
		public string[] CustomDebugIdentifiers;
		[ItemProperty("DebugLevel")]
		public int DebugLevel = 0;

		string[] gVersionIds;
		/// <summary>
		/// Includes custom version identifiers already.
		/// Used for code completion.
		/// </summary>
		public string[] GlobalVersionIdentifiers
		{
			get
			{ 
				if(gVersionIds == null)
					UpdateGlobalVersionIdentifiers();
				return gVersionIds; 
			}
		}
		#endregion

		public DProjectConfiguration ()
		{
			this.ExternalConsole = true;
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
			CompileTarget = conf.CompileTarget;
			CustomVersionIdentifiers = conf.CustomVersionIdentifiers;
			CustomDebugIdentifiers = conf.CustomDebugIdentifiers;
			DebugLevel = conf.DebugLevel;
			gVersionIds = conf.gVersionIds;
			UnittestMode = conf.UnittestMode;
			ProfilerMode = conf.ProfilerMode;
			//DebugMode = conf.DebugMode;
			DDocDirectory = conf.DDocDirectory;

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
						
					switch (CompileTarget) {
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

		/// <summary>
		/// Builds an array of all global version id definitions.
		/// Used for code completion.
		/// </summary>
		public void UpdateGlobalVersionIdentifiers(DProject prjOverride = null)
		{
			if (prjOverride == null)
				if ((prjOverride = Project) == null)
					return;

			var cmp = prjOverride.Compiler;

			// Compiler args + cfg args + extra args
			var buildCfg = cmp.GetOrCreateTargetConfiguration(this.CompileTarget);
			var buildArgs = buildCfg.GetArguments(this.DebugMode);
			var cmpArgs = (buildArgs.OneStepBuildArguments ?? buildArgs.CompilerArguments) + 
				ExtraCompilerArguments + ExtraLinkerArguments;

			//TODO: Distinguish between D1/D2 and probably later versions?
			var a = D_Parser.Misc.VersionIdEvaluation.GetVersionIds(cmp.PredefinedVersionConstant,cmpArgs, UnittestMode);
			var res = new string[(a== null ? 0 : a.Length) + (CustomVersionIdentifiers == null ? 0: CustomVersionIdentifiers.Length)];
			if(a!=null)
				Array.Copy(a,res,a.Length);
			if(CustomVersionIdentifiers!=null)
				Array.Copy(CustomVersionIdentifiers,0,res,res.Length - CustomVersionIdentifiers.Length,CustomVersionIdentifiers.Length);
			gVersionIds = res;
		}

		public override FilePath IntermediateOutputDirectory {
			get {
				return FilePath.Build(ObjectDirectory);
			}
		}
	}
}
