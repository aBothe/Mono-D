using System.Collections.Generic;
using System.IO;

namespace MonoDevelop.D.Building
{
	public interface IArgumentMacroProvider
	{
        void ManipulateMacros(Dictionary<string,string> macros);
	}

	/// <summary>
	/// Provides macro substitution for D source compilation tasks.
	/// </summary>
	public class DCompilerMacroProvider : IArgumentMacroProvider
	{
		public string IncludePathConcatPattern = "-I\"{0}\"";

		public string SourceFile;
		public string ObjectFile;

		public IEnumerable<string> Includes
		{
			set{
				importPaths="";
				if(value!=null)
					foreach(var p in value)
						importPaths+=string.Format(IncludePathConcatPattern,p)+' ';
			}
		}
		
		string importPaths;

        public void ManipulateMacros(Dictionary<string, string> macros)
        {
            macros["src"]=SourceFile;
            macros["obj"] = ObjectFile;
            macros["includes"] = importPaths;
        }
    }

	/// <summary>
	/// Provides macro substitution when linking D object files to one target file.
	/// </summary>
	public class DLinkerMacroProvider : IArgumentMacroProvider
	{
		public string ObjectsStringPattern = "\"{0}\"";

		public IEnumerable<string> Objects
		{
			set {
				objects = "";
				if(value!=null)
					foreach (var o in value)
						objects += string.Format(ObjectsStringPattern,o)+ ' ';
			}
		}
		public string TargetFile;
		public string RelativeTargetDirectory;

		string objects;
		
		public IEnumerable<string> Libraries
		{
			set{
				libs="";
				if(value!=null)
					foreach(var p in value)
						libs+='"'+p+'"'+' ';
			}
		}
		
		string libs;

        public void ManipulateMacros(Dictionary<string, string> macros)
        {
            macros["objs"] = objects;
            macros["libs"]=libs;
            macros["target"] = TargetFile;
            macros["relativeTargetDirectory"] = 
                macros["relativeTargetDir"] = RelativeTargetDirectory;
            macros["target_noExt"] = Path.ChangeExtension(TargetFile, null);
        }
    }

    public class OneStepBuildArgumentMacroProvider:IArgumentMacroProvider{
        public string ObjectsStringPattern = "\"{0}\"";
        public string IncludesStringPattern = "-I\"{0}\"";

        public string TargetFile;
        public string RelativeTargetDirectory;
        public string ObjectsDirectory;


        string sources;
        string libs;
        string includes;

        public IEnumerable<string> SourceFiles
        {
            set
            {
                sources = "";
                if (value != null)
                    foreach (var o in value)
                        sources += string.Format(ObjectsStringPattern, o) + ' ';
            }
        }
        
        public IEnumerable<string> Libraries
        {
            set
            {
                libs = "";
                if (value != null)
                    foreach (var p in value)
                        libs += '"' + p + '"' + ' ';
            }
        }

        public IEnumerable<string> Includes
        {
            set
            {
                includes = "";
                if (value != null)
                    foreach (var p in value)
                        includes += string.Format(IncludesStringPattern,p)+" ";
            }
        }

        public void ManipulateMacros(Dictionary<string, string> macros)
        {
            macros["sources"] = sources;
            macros["libs"] = libs;
            macros["includes"] = includes;
            macros["objectsDirectory"] = ObjectsDirectory;
            macros["target"] = TargetFile;
            macros["exe"] = Path.ChangeExtension(TargetFile, DCompilerService.ExecutableExtension);
            macros["lib"] = Path.ChangeExtension(TargetFile, DCompilerService.StaticLibraryExtension);
            macros["dll"] = Path.ChangeExtension(TargetFile, DCompilerService.SharedLibraryExtension);
        }
    }
}
