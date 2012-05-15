using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Misc;

namespace D_Parser.Completion.Providers
{
	public class ImportStatementCompletionProvider : AbstractCompletionProvider
	{
		ImportStatement.Import imp;
		ImportStatement.ImportBindings impBind;

		public ImportStatementCompletionProvider(
			ICompletionDataGenerator gen, 
			ImportStatement.Import imp)
			: base(gen)
		{
			this.imp = imp;
		}

		public ImportStatementCompletionProvider(
			ICompletionDataGenerator gen, 
			ImportStatement.ImportBindings imbBind)
			: base(gen)
		{
			this.impBind = imbBind;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			if(Editor.ParseCache == null)
				return;

			if (imp != null)
			{
				string pack = null;

				if (imp.ModuleIdentifier != null && imp.ModuleIdentifier.InnerDeclaration != null)
				{
					pack = imp.ModuleIdentifier.InnerDeclaration.ToString();

					// Will occur after an initial dot  
					if (string.IsNullOrEmpty(pack))
						return;
				}

				foreach (var p in Editor.ParseCache.LookupPackage(pack))
				{
					foreach (var kv_pack in p.Packages)
						CompletionDataGenerator.Add(kv_pack.Key);

					foreach (var kv_mod in p.Modules)
						CompletionDataGenerator.Add(kv_mod.Key, kv_mod.Value);
				}
			}
			else if (impBind != null)
			{
				/*
				 * Show all members of the imported modules
				 * + public imports 
				 * + items of anonymous enums
				 */
			}
		}
	}
}
