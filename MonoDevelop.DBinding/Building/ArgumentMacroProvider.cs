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

		public string Replace(string Input)
		{
			if (Input == "src")
				return SourceFile;
			if (Input == "obj")
				return ObjectFile;
			return Input;
		}
	}

	/// <summary>
	/// Provides macro substitution when linking D object files to one target file.
	/// </summary>
	public class DLinkerMacroProvider : IArgumentMacroProvider
	{
		public string[] Objects
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

			return null;
		}
	}
}
