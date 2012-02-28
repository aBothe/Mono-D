using System.Collections.Generic;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

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

	public class ImportStatement:AbstractStatement, IDeclarationContainingStatement
	{
		public bool IsStatic
		{
			get { return DAttribute.ContainsAttribute(Attributes, DTokens.Static); }
		}

		public bool IsPublic
		{
			get { return DAttribute.ContainsAttribute(Attributes, DTokens.Public); }
		}

		public class Import
		{
			/// <summary>
			/// import io=std.stdio;
			/// </summary>
			public string ModuleAlias;
			public ITypeDeclaration ModuleIdentifier;

			public override string ToString()
			{
				var r= string.IsNullOrEmpty(ModuleAlias) ? "":(ModuleAlias+" = ");

				if (ModuleIdentifier != null)
					r += ModuleIdentifier.ToString();

				return r;
			}
		}

		public class ImportBindings
		{
			public Import Module;

			/// <summary>
			/// Keys: symbol alias
			/// Values: symbol
			/// 
			/// If value empty: Key is imported symbol
			/// </summary>
			public List<KeyValuePair<string, string>> SelectedSymbols = new List<KeyValuePair<string, string>>();

			public override string ToString()
			{
				var r = Module==null?"":Module.ToString();

				r += " : ";

				if(SelectedSymbols!=null)
					foreach (var kv in SelectedSymbols)
					{
						r += kv.Key;

						if (!string.IsNullOrEmpty(kv.Value))
							r += "="+kv.Value;

						r += ",";
					}

				return r.TrimEnd(',');
			}
		}

		public List<Import> Imports = new List<Import>();
		public ImportBindings ImportBinding;

		public override string ToCode()
		{
			var ret = AttributeString + "import ";

			foreach (var imp in Imports)
			{
				ret += imp.ToString()+",";
			}

			if (ImportBinding != null)
				ret = ret.TrimEnd(',');
			else
				ret += ImportBinding.ToString();

			return ret;
		}

		#region Pseudo alias variable generation
		/// <summary>
		/// These aliases are used for better handling of aliased modules imports and/or selective imports
		/// </summary>
		public List<DVariable> PseudoAliases = new List<DVariable>();

		public void CreatePseudoAliases()
		{
			PseudoAliases.Clear();

			foreach (var imp in Imports)
				if (!string.IsNullOrEmpty(imp.ModuleAlias))
					PseudoAliases.Add(new ImportSymbolAlias(this,imp));

			if (ImportBinding != null)
			{
				/*
				 * import cv=std.conv : Convert = to;
				 * 
				 * cv can be still used as an alias for std.conv,
				 * whereas Convert is a direct alias for std.conv.to
				 */
				if(!string.IsNullOrEmpty(ImportBinding.Module.ModuleAlias))
					PseudoAliases.Add(new ImportSymbolAlias(this,ImportBinding.Module));

				foreach (var bind in ImportBinding.SelectedSymbols)
					PseudoAliases.Add(new ImportSymbolAlias
					{
						IsAlias=true,
						IsModuleAlias = false,
						OriginalImportStatement = this,
						Name = bind.Key,
						Type = new IdentifierDeclaration(string.IsNullOrEmpty(bind.Value) ? bind.Key : bind.Value)
						{
							InnerDeclaration = ImportBinding.Module.ModuleIdentifier
						}
					});
			}
		}

		/// <summary>
		/// Returns import pseudo-alias variables
		/// </summary>
		public INode[] Declarations
		{
			get { return PseudoAliases.Count == 0 ? null : PseudoAliases.ToArray(); }
		}
		#endregion
	}

	public class ImportSymbolAlias : DVariable
	{
		public bool IsModuleAlias;
		public ImportStatement OriginalImportStatement;

		public ImportSymbolAlias(ImportStatement impStmt,ImportStatement.Import imp)
		{
			OriginalImportStatement = impStmt;

			IsModuleAlias = true;
			Name = imp.ModuleAlias;
			Type = imp.ModuleIdentifier;
			IsAlias = true;
		}

		public ImportSymbolAlias()	{}
	}
}
