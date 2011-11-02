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
		public string SourceFile;
		public string ObjectFile;
		public IEnumerable<string> ImportPaths
		{
			set{
				importPaths="";
				if(value!=null)
					foreach(var p in value)
						importPaths+='"'+p+'"'+' ';
			}
		}
		
		string importPaths;

		public string Replace(string Input)
		{
			if (Input == "src")
				return SourceFile;
			if (Input == "obj")
				return ObjectFile;
			if(Input=="importPaths")
				return importPaths;
			return Input;
		}
	}

	/// <summary>
	/// Provides macro substitution when linking D object files to one target file.
	/// </summary>
	public class DLinkerMacroProvider : IArgumentMacroProvider
	{
		public IEnumerable<string> Objects
		{
			set {
				objects = "";
				if(value!=null)
					foreach (var o in value)
						objects += '"'+o+"\" ";
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
