
namespace D_Parser.Misc
{
	public class ModuleNameHelper
	{
		/// <summary>
		/// a.b.c.d => a.b.c
		/// </summary>
		public static string ExtractPackageName(string ModuleName)
		{
			if (string.IsNullOrEmpty(ModuleName))
				return "";

			var i = ModuleName.LastIndexOf('.');

			return i == -1 ? "" : ModuleName.Substring(0, i);
		}

		/// <summary>
		/// a.b.c.d => d
		/// </summary>
		public static string ExtractModuleName(string ModuleName)
		{
			if (string.IsNullOrEmpty(ModuleName))
				return "";

			var i = ModuleName.LastIndexOf('.');

			return i == -1 ? ModuleName : ModuleName.Substring(i + 1);
		}

		public static string[] SplitModuleName(string ModuleName)
		{
			return ModuleName.Split('.');
		}
	}
}
