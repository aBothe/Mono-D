using System;
using System.Collections.Generic;
using System.IO;
using MonoDevelop.Core.Serialization;
using MonoDevelop.D.Building;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.Collections;
using MonoDevelop.D.Projects.Dub;

namespace MonoDevelop.D.Projects
{
	public class DProjectConfiguration:ProjectConfiguration
	{
		#region Properties
		DProject _prj;
		public DProject Project
		{
			get {
				if(_prj != null)
					return _prj;
				
				foreach(var prj in Ide.IdeApp.Workspace.GetAllProjects())
					if(prj.GetConfiguration(this.Selector) == this)
						return _prj = prj as DProject;
				
				return _prj = ParentItem as DProject;
			}
		}

		[ItemProperty("Target")]
		public DCompileTarget CompileTarget = DCompileTarget.Executable;
		[ItemProperty("OutputName")]
		public string Output = "";
		[ItemProperty("UnittestMode")]
		public bool UnittestMode = false;
		[ItemProperty("ExtraCompilerArguments", DefaultValue = "")]
		public string ExtraCompilerArguments = "";
		[ItemProperty("ExtraLinkerArguments", DefaultValue = "")]
		public string ExtraLinkerArguments = "";
		[ItemProperty("Libs")]
		[ItemProperty("Lib", Scope = "*")]
		public List<string> ExtraLibraries = new List<string> ();
		[ItemProperty("LinkinThirdPartyLibraries")]
		public bool LinkinThirdPartyLibraries = false;
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
			var prj = Project;
			var referencedItems = LinkinThirdPartyLibraries ? prj.ParentSolution.GetAllProjectsWithTopologicalSort (configSelector) : prj.GetReferencedDProjects (configSelector) as IEnumerable<Project>;
			foreach (var dep_ in referencedItems)
			{
				if (dep_ == prj) // Prohibit linking to itself - will occur when enlisting all projects via GetAllProjectsWithTopologicalSort
					break;

				var dep = dep_ as AbstractDProject;
				if (dep == null)
					continue;

				if(takeDefSelector)
					configSelector = dep.DefaultConfiguration.Selector;

				var cfg = dep.GetConfiguration (configSelector);

				DCompileTarget targetType;

				if (cfg is DProjectConfiguration)
					targetType = (cfg as DProjectConfiguration).CompileTarget;
				else if (cfg is DubProjectConfiguration)
					targetType = (cfg as DubProjectConfiguration).TargetType;
				else
					continue;

				if (targetType == DCompileTarget.StaticLibrary)
						yield return dep.GetOutputFileName (configSelector);
				// Assume there is an import lib inside the dll's output directory and add it to the linked-in libs
				// https://github.com/aBothe/Mono-D/issues/180
				else if (OS.IsWindows && targetType == DCompileTarget.SharedLibrary) {
					var lib = Path.ChangeExtension (dep.GetOutputFileName (configSelector), DCompilerService.StaticLibraryExtension);
					if (File.Exists (lib))
						yield return lib;
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

		public override string ToString ()
		{
			return string.Format ("[DProjectConfiguration: Name={4}, Project={0}, GlobalVersionIdentifiers={1}, CompiledOutputName={2}, IntermediateOutputDirectory={3}]", Project, GlobalVersionIdentifiers, CompiledOutputName, IntermediateOutputDirectory, Name);
		}
		#endregion

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
			//DebugMode = conf.DebugMode;
			DDocDirectory = conf.DDocDirectory;

            ExtraLibraries.Clear();
            ExtraLibraries.AddRange(conf.ExtraLibraries);
			LinkinThirdPartyLibraries = conf.LinkinThirdPartyLibraries;
			
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
							if (UnittestMode)
								goto default;
						ext = DCompilerService.SharedLibraryExtension;
						break;
					case DCompileTarget.StaticLibrary:
							if (UnittestMode)
								goto default;
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
			var cmpArgs = ProjectBuilder.BuildOneStepBuildString(prjOverride, new string[0], Selector);

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

		public override int GetHashCode ()
		{
			int hash=0;
			unchecked {
				foreach (var prop in GetType().GetFields()) {
					object v;
					if (prop.GetCustomAttributes (typeof(ItemPropertyAttribute), true).Length > 0 &&
					   (v = prop.GetValue (this)) != null) {
						if (v is IEnumerable && !(v is string)) {
							foreach (var k in v as IEnumerable)
								hash += k.GetHashCode ();
						} else
							hash += v.GetHashCode ();
					}
				}
			}
			return hash;
		}
	}
}
