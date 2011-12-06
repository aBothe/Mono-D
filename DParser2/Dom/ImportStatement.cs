using System.Collections.Generic;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public class ModuleStatement:AbstractStatement
	{
		public ITypeDeclaration ModuleName;

		public override string ToCode()
		{
			return "module "+ModuleName==null?"": ModuleName.ToString();
		}
	}

	public class ImportStatement:AbstractStatement
	{
		public bool IsStatic;
		public bool IsPublic;

		/// <summary>
		/// import io=std.stdio;
		/// </summary>
		public string ModuleAlias;
		public ITypeDeclaration ModuleIdentifier;

		/// <summary>
		/// import std.stdio:writeln,foo=writeln;
		/// 
		/// Key:	symbol, alias identifier
		/// Value:	empty,	aliased symbol
		/// </summary>
		public Dictionary<string, string> ExclusivelyImportedSymbols=null;

		/// <summary>
		/// True on things like import abc.def;
		/// </summary>
		public bool IsSimpleBinding
		{
			get
			{
				return string.IsNullOrEmpty(ModuleAlias) && (ExclusivelyImportedSymbols==null || ExclusivelyImportedSymbols.Count<1);
			}
		}

		public override string ToCode()
		{
			var ret = (IsPublic ? "public " : "") + (IsStatic ? "static " : "") + "import ";

			if (!string.IsNullOrEmpty(ModuleAlias))
				ret += ModuleAlias + '=';

			ret += ModuleIdentifier;

			if (ExclusivelyImportedSymbols != null && ExclusivelyImportedSymbols.Count > 0)
			{
				ret += ':';

				foreach (var kv in ExclusivelyImportedSymbols)
				{
					ret += kv.Key;

					if (!string.IsNullOrEmpty(kv.Value))
						ret += '=' + kv.Value;

					ret += ',';
				}

				ret = ret.TrimEnd(',');
			}

			return ret;
		}
	}
}
