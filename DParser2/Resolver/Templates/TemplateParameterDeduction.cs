using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;

namespace D_Parser.Resolver.Templates
{
	public partial class TemplateParameterDeduction
	{
		#region Properties / ctor
		/// <summary>
		/// The dictionary which stores all deduced results + their names
		/// </summary>
		Dictionary<string, ResolveResult[]> TargetDictionary;

		/// <summary>
		/// Needed for resolving default types
		/// </summary>
		ResolverContextStack ctxt;

		public TemplateParameterDeduction(Dictionary<string, ResolveResult[]> DeducedParameters, ResolverContextStack ctxt)
		{
			this.ctxt = ctxt;
			this.TargetDictionary = DeducedParameters;
		}
		#endregion

		public bool Handle(ITemplateParameter parameter, ResolveResult argumentToAnalyze)
		{
			//TODO: Handle __FILE__ and __LINE__ correctly - so don't evaluate them at the template declaration but at the point of instantiation

			/*
			 * Introduce previously deduced parameters into current resolution context
			 * to allow value parameter to be of e.g. type T whereas T is already set somewhere before 
			 */
			Dictionary<string, ResolveResult[]> _prefLocalsBackup = null;
			if (ctxt != null && ctxt.CurrentContext != null)
			{
				_prefLocalsBackup = ctxt.CurrentContext.DeducedTemplateParameters;

				var d = new Dictionary<string, ResolveResult[]>();
				foreach (var kv in TargetDictionary)
					if (kv.Value != null && kv.Value.Length != 0)
						d[kv.Key] = kv.Value;
				ctxt.CurrentContext.DeducedTemplateParameters = d;
			}

			// Packages aren't allowed at all
			if(argumentToAnalyze is ModulePackageResult)
				return false;

			// Module symbols can be used as alias only
			if (argumentToAnalyze is ModuleResult &&
				!(parameter is TemplateAliasParameter))
				return false;

			bool res = false;

			if (parameter is TemplateAliasParameter)
				res = Handle((TemplateAliasParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateThisParameter)
				res = Handle((TemplateThisParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateTypeParameter)
				res = Handle((TemplateTypeParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateValueParameter)
				res = Handle((TemplateValueParameter)parameter, argumentToAnalyze);
			else if (parameter is TemplateTupleParameter)
				res = Handle((TemplateTupleParameter)parameter, new[] { new[] { argumentToAnalyze } });

			if (ctxt != null && ctxt.CurrentContext != null)
				ctxt.CurrentContext.DeducedTemplateParameters = _prefLocalsBackup;

			return res;
		}

		bool Handle(TemplateThisParameter p, ResolveResult arg)
		{
			// Only special handling required for method calls
			return Handle(p.FollowParameter,arg);
		}

		public bool Handle(TemplateTupleParameter p, IEnumerable<ResolveResult[]> arguments)
		{
			if (arguments == null)
				return false;

			var args= arguments.ToArray();

			if (args.Length < 1)
				return false;

			return Set(p.Name, new TypeTupleResult { 
				TupleParameter=p,
				TupleItems=args 
			});
		}

		/// <summary>
		/// Returns true if <param name="parameterName">parameterName</param> is expected somewhere in the template parameter list.
		/// </summary>
		bool Contains(string parameterName)
		{
			foreach (var kv in TargetDictionary)
				if (kv.Key == parameterName)
					return true;
			return false;
		}

		/// <summary>
		/// Returns false if the item has already been set before and if the already set item is not equal to 'r'.
		/// Inserts 'r' into the target dictionary and returns true otherwise.
		/// </summary>
		bool Set(string parameterName, ResolveResult r)
		{
			ResolveResult[] rl=null;
			if (!TargetDictionary.TryGetValue(parameterName, out rl) || rl == null)
			{
				TargetDictionary[parameterName] = new[] { r };
				return true;
			}
			else
			{
				if (rl.Length == 1 && ResultComparer.IsEqual(rl[0], r))
						return true;

				var newArr = new ResolveResult[rl.Length + 1];
				rl.CopyTo(newArr, 0);
				newArr[rl.Length] = r;

				TargetDictionary[parameterName] = newArr;
				return false;
			}
		}
	}
}
