using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Debugging.Client;
using Mono.Debugging.Backend;
using D_Parser.Parser;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace MonoDevelop.D.Debugging
{
	public class DLocalExamBacktrace : IObjectValueSource
	{
		#region Properties
		public readonly IDBacktraceHelpers BacktraceHelper;
		public const long MaximumDisplayCount = 1000;
		public const long MaximumArrayLengthThreshold = 100000;

		bool needsStackFrameInfoUpdate = true;
		public string currentStackFrameSource {get;private set;}
		public ulong currentInstruction {get;private set;}
		public CodeLocation currentSourceLocation{ get; private set;}
		ResolutionContext ctxt;
		#endregion


		public DLocalExamBacktrace(IDBacktraceHelpers helper)
		{
			this.BacktraceHelper = helper;
		}

		/// <summary>
		/// Must be invoked after the program execution was interrupted.
		/// Resets internal value caches so no old values will be shown.
		/// </summary>
		public void Reset ()
		{
			needsStackFrameInfoUpdate = true;
		}

		void TryUpdateStackFrameInfo()
		{
			if (needsStackFrameInfoUpdate) {
				string file;
				ulong eip;
				CodeLocation loc;

				BacktraceHelper.GetCurrentStackFrameInfo (out file, out eip, out loc);
				currentStackFrameSource = file;
				currentInstruction = eip;
				currentSourceLocation = loc;

				needsStackFrameInfoUpdate = !string.IsNullOrWhiteSpace(currentStackFrameSource) && !currentSourceLocation.IsEmpty;

				var mod = GlobalParseCache.GetModule(currentStackFrameSource);
				if(ctxt == null)
					ctxt = BacktraceHelper.LocalsResolutionHelperContext;

				if (ctxt == null)
					return;

				if (mod == null) {
					if (ctxt.CurrentContext != null)
						ctxt.CurrentContext.Set (currentSourceLocation);
				} else {
					var bn = D_Parser.Resolver.TypeResolution.DResolver.SearchBlockAt (mod, currentSourceLocation);
					if (ctxt.CurrentContext == null)
						ctxt.Push (bn, currentSourceLocation);
					else
						ctxt.CurrentContext.Set (bn, currentSourceLocation);
				}
			}
		}


		#region Value exam
		string ExtractExpressionId(IExpression x)
		{
			if (x is TokenExpression)
				return x.ToString ();
			if (x is IdentifierExpression)
				return (x as IdentifierExpression).StringValue;
			if (x is TemplateInstanceExpression)
				return (x as TemplateInstanceExpression).TemplateId;
			if (x is NewExpression) {
				var nex = x as NewExpression;
				if (nex.Type is IdentifierDeclaration)
					return (nex.Type as IdentifierDeclaration).Id;
				if (nex.Type is IExpression)
					return ExtractExpressionId (nex.Type as IExpression);
			}

			return "";
		}

		public virtual ObjectValue CreateObjectValue(string expression, EvaluationOptions evalOptions = null)
		{
			var variableExpression = DParser.ParseExpression (expression);

			TryUpdateStackFrameInfo ();

			var x = variableExpression;
			var lookupQueue = new Queue<string> ();

			while (x is PostfixExpression) {
				var n = ExtractExpressionId (x);
				if (!string.IsNullOrWhiteSpace (n))
					lookupQueue.Enqueue (n);
				x = (x as PostfixExpression).PostfixForeExpression;
			}


				
			if(x is TokenExpression)
				(x as TokenExpression).Location = currentSourceLocation;
			else if (x is IdentifierExpression)
				(x as IdentifierExpression).Location = currentSourceLocation;
			else if (x is TemplateInstanceExpression)
				(x as TemplateInstanceExpression).Location = currentSourceLocation;

			var nameToLookup = ExtractExpressionId (x);

			IDBacktraceSymbol symb = null;

			// First, search in real path
			foreach (var s in BacktraceHelper.Parameters)
				if (s.Name == nameToLookup) {
					symb = s;
					break;
				}

			if (symb == null)
				foreach (var s in BacktraceHelper.Locals)
					if (s.Name == nameToLookup) {
						symb = s;
						break;
					}

			// If no result found, search via assisting source code resolver

			return CreateObjectValue (symb, evalOptions);
		}

		public virtual ObjectValue CreateObjectValue(IDBacktraceSymbol s, EvaluationOptions evalOptions = null)
		{
			if (evalOptions == null)
				evalOptions = EvaluationOptions.DefaultOptions;
			
			TryUpdateStackFrameInfo ();

			return ObjectValue.CreatePrimitive(this, new ObjectPath(s.Name), s.TypeName, new EvaluationResult(s.Value), ObjectValueFlags.Field);
		}
		#endregion

		public ObjectValue[] GetParameters(EvaluationOptions evalOptions)
		{
			var l = new List<ObjectValue>();
			foreach (var p in BacktraceHelper.Parameters)
			{
				l.Add(CreateObjectValue(p, evalOptions));
			}
			return l.ToArray();
		}

		public ObjectValue[] GetLocals(EvaluationOptions evalOptions)
		{
			var l = new List<ObjectValue>();
			foreach (var p in BacktraceHelper.Locals)
			{
				l.Add(CreateObjectValue(p, evalOptions));
			}
			return l.ToArray();
		}

		public ObjectValue[] GetChildren(ObjectPath path, int index, int count, EvaluationOptions options)
		{
			return null;
		}

		public object GetRawValue(ObjectPath path, EvaluationOptions options)
		{
			return null;
		}

		public ObjectValue GetValue(ObjectPath path, EvaluationOptions options)
		{
			return null;
		}

		public void SetRawValue(ObjectPath path, object value, EvaluationOptions options)
		{
			
		}

		public EvaluationResult SetValue(ObjectPath path, string value, EvaluationOptions options)
		{
			return null;
		}
	}
}
