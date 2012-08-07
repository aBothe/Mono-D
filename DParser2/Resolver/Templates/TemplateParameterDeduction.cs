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
		DeducedTypeDictionary TargetDictionary;

		/// <summary>
		/// If true and deducing a type parameter,
		/// the equality of the given and expected type is required instead of their simple convertibility.
		/// Used when evaluating IsExpressions.
		/// </summary>
		public bool EnforceTypeEqualityWhenDeducing
		{
			get;
			set;
		}

		/// <summary>
		/// Needed for resolving default types
		/// </summary>
		ResolverContextStack ctxt;

		public TemplateParameterDeduction(DeducedTypeDictionary DeducedParameters, ResolverContextStack ctxt)
		{
			this.ctxt = ctxt;
			this.TargetDictionary = DeducedParameters;
		}
		#endregion

		public bool Handle(ITemplateParameter parameter, ISemantic argumentToAnalyze)
		{
			//TODO: Handle __FILE__ and __LINE__ correctly - so don't evaluate them at the template declaration but at the point of instantiation

			/*
			 * Introduce previously deduced parameters into current resolution context
			 * to allow value parameter to be of e.g. type T whereas T is already set somewhere before 
			 */
			DeducedTypeDictionary _prefLocalsBackup = null;
			if (ctxt != null && ctxt.CurrentContext != null)
			{
				_prefLocalsBackup = ctxt.CurrentContext.DeducedTemplateParameters;

				var d = new DeducedTypeDictionary();
				foreach (var kv in TargetDictionary)
					if (kv.Value != null)
						d[kv.Key] = kv.Value;
				ctxt.CurrentContext.DeducedTemplateParameters = d;
			}

			// Packages aren't allowed at all
			if(argumentToAnalyze is PackageSymbol)
				return false;

			// Module symbols can be used as alias only
			if (argumentToAnalyze is ModuleSymbol &&
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
				res = Handle((TemplateTupleParameter)parameter, new[] { argumentToAnalyze });

			if (ctxt != null && ctxt.CurrentContext != null)
				ctxt.CurrentContext.DeducedTemplateParameters = _prefLocalsBackup;

			return res;
		}

		bool Handle(TemplateThisParameter p, ISemantic arg)
		{
			// Only special handling required for method calls
			return Handle(p.FollowParameter,arg);
		}

		public bool Handle(TemplateTupleParameter p, IEnumerable<ISemantic> arguments)
		{
			if (arguments == null)
				return false;

			var args= arguments.ToArray();

			if (args.Length < 1)
				return false;

			var l = new List<AbstractType>();

			foreach (var arg in arguments)
				if (arg is AbstractType)
					l.Add((AbstractType)arg);
				else
				{
					// Error: Argument must be a type
					break;
				}					

			return Set(p, new TypeTuple(p, l));
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
		bool Set(ITemplateParameter p, ISemantic r, string name=null)
		{
			if (string.IsNullOrEmpty(name))
				name = p.Name;

			TemplateParameterSymbol rl=null;
			if (!TargetDictionary.TryGetValue(name, out rl) || rl == null)
			{
				TargetDictionary[name] = new TemplateParameterSymbol(p, r);
				return true;
			}
			else
			{
				if (rl!=null)
					if (ResultComparer.IsEqual(rl.Base, r))
						return true;
					else
					{
						// Error: Ambiguous assignment
					}

				TargetDictionary[name] = new TemplateParameterSymbol(p, r);

				return false;
			}
		}
	}
}
