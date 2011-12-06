using System.Collections.Generic;
using System.IO;

namespace MonoDevelop.D.Building
{
	public interface IArgumentMacroProvider
	{
		string Replace(string Input);
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

		public string Replace(string Input)
		{
			if (Input == "src")
				return SourceFile;
			if (Input == "obj")
				return ObjectFile;
			if(Input=="includes")
				return importPaths;
			return Input;
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
		
		public IEnumerable<string> LibraryPaths
		{
			set{
				libPaths="";
				if(value!=null)
					foreach(var p in value)
						libPaths+='"'+p+'"'+' ';
			}
		}
		
		string libPaths;
		
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

		public string Replace(string Input)
		{
			if (Input == "objs")
				return objects;
			if (Input == "target")
				return TargetFile;
			if (Input == "relativeTargetDirectory")
				return RelativeTargetDirectory;
			if (Input == "target_noExt")
				return Path.ChangeExtension(TargetFile, null);
			if(Input=="libraryPaths")
				return libPaths;
			if(Input=="libs")
				return libs;

			return null;
		}
	}
}
