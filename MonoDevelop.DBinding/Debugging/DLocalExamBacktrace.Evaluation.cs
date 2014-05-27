using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using Mono.Debugging.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Debugging
{
    public partial class DLocalExamBacktrace
    {
		#region High-Level
		public virtual ObjectValue CreateObjectValue(string expression, EvaluationOptions evalOptions = null)
		{
			var variableExpression = DParser.ParseExpression(expression);

			TryUpdateStackFrameInfo();
			
			/*
			 * Usual expressions to evaluate may look like
			 * a.b.c.d
			 * In such situations, normal debugger-only variable lookup might be sufficient to get to the actual variable that is accessed.
			 * Anyway, there might be any kind of D expression incoming, such as ´a.b[0]´ or ´this[0]´.
			 * In those cases, try to look up as many initial identifier access chain parts via the debugger symbol info as possible (´a.b´), 
			 * then evaluate the remaining parts utilizing the source code info coming from Mono-D.
			 * Even if no source code is present (ctxt is null), a.b[0] should be evaluatable though. 
			 */

			var x = variableExpression;
			var lookupQueue = new Queue<PostfixExpression_Access>();

			while (x is PostfixExpression_Access)
			{
				lookupQueue.Enqueue(x as PostfixExpression_Access);
				x = (x as PostfixExpression).PostfixForeExpression;
			}

			string nameToLookup = null;
			var tokX = x as TokenExpression;
			var id = x as IdentifierExpression;
			if (tokX != null)
			{
				tokX.Location = currentSourceLocation;
				if (tokX.Token == DTokens.This)
					nameToLookup = "this";
			}
			else if (id != null)
			{
				id.Location = currentSourceLocation;
				if (id.IsIdentifier)
					nameToLookup = id.StringValue;
			}
			else if (x is TemplateInstanceExpression)
				(x as TemplateInstanceExpression).Location = currentSourceLocation;

			var symb = nameToLookup != null ? BacktraceHelper.FindSymbol(nameToLookup) : null;

			// step deeper by searching in child items
			bool foundChildItem = true;
			while (symb != null && lookupQueue.Count != 0)
			{
				x = lookupQueue.Dequeue();

				id = (x as PostfixExpression_Access).AccessExpression as IdentifierExpression;
				if (id == null || !id.IsIdentifier)
				{
					foundChildItem = false;
					break;
				}

				nameToLookup = id.StringValue;
				foundChildItem = false;
				if (symb.ChildCount != 0)
					foreach (var ch in symb.Children)
						if (ch.Name == nameToLookup)
						{
							foundChildItem = true;
							symb = ch;
							break;
						}
				if (!foundChildItem)
					break;
			}

			ISymbolValue ev = null;
			ObjectPath p;

			// If no result found, search via assisting source code resolver
			// 1) (0,0) Neither base item nor matching child found
			// 2) (1,0) There was a base item found but no matching child item
			// 3) (0,1) No base item and a child item -- that's not possible.
			// 4) (1,1) There was a base item found and a matching child item 
			if (symb == null) // 1)
				ev = Evaluation.EvaluateValue(variableExpression, SymbolProvider);
			else
			{
				ev = EvaluateSymbol(symb);

				if (foundChildItem) // 4) -- Also applies when there's no further child item is getting accessed
					p = GetPath(symb);
				else if (ev != null) // 2)
				{
					// ´a.b.c.d´
					// ´a.b´ could get resolved,
					//     ´.c.d´ remains unevaluated
					// x is now ´c´; ´.d´ is left in lookupQueue

					do
					{
						// x now contains the next expression that must be evaluated.
						ev = Evaluation.EvalPostfixAccessExpression<ISymbolValue>(null, ctxt, x as PostfixExpression_Access, ev, ValueProvider: SymbolProvider).FirstOrDefault();
					}
					while (ev != null && lookupQueue.Count != 0 && (x = lookupQueue.Dequeue()) != null);
				}
			}

			return CreateObjectValue(ev, variableExpression, p, evalOptions, symb);
		}

		public virtual ObjectValue CreateObjectValue(IDBacktraceSymbol s, EvaluationOptions evalOptions = null)
		{
			if(s == null)
				return null;

			if (evalOptions == null)
				evalOptions = EvaluationOptions.DefaultOptions;

			TryUpdateStackFrameInfo();

			return CreateObjectValue(EvaluateSymbol(s), null, GetPath(s), evalOptions, s);
		}

		public static ObjectPath GetPath(IDBacktraceSymbol s)
		{
			var l = new List<string>();

			do
				l.Insert(0,s.Name);
			while (s.HasParent && (s = s.Parent) != null);

			return new ObjectPath(l.ToArray());
		}
		#endregion

		public AbstractType TryGetDType(IDBacktraceSymbol s)
		{
			return null;
		}

		/// <summary>
		/// In here, the main evaluation to primitives, strings, arrays, objects happens
		/// </summary>
		public ISymbolValue EvaluateSymbol(IDBacktraceSymbol s)
		{
			var t = s.DType ?? TryGetDType(s);

			if (t == null)
				return null;
			try
			{
				return t.Accept(new DebugSymbolTypeEvalVisitor(this, s));
			}
			catch (NotImplementedException)
			{
				return null;
			}
		}

		ObjectValue CreateObjectValue(ISymbolValue v, IExpression originalExpression, ObjectPath pathOpt, EvaluationOptions evalOptions, IDBacktraceSymbol symbolOpt = null)
		{
			if (v == null){
				if(symbolOpt != null)
					return ObjectValue.CreatePrimitive(this, pathOpt, symbolOpt.TypeName, new Mono.Debugging.Backend.EvaluationResult(symbolOpt.Value), ObjectValueFlags.Variable);

				return ObjectValue.CreateError(this, pathOpt, "", "Couldn't evaluate expression "+ (originalExpression != null ? originalExpression.ToString() : ""), ObjectValueFlags.Error);
			}

			return v.Accept(new ObjectValueSynthVisitor { evalOptions = evalOptions, OriginalExpression = originalExpression, Path = pathOpt });
		}

		class ObjectValueSynthVisitor : ISymbolValueVisitor<ObjectValue>
		{
			public IExpression OriginalExpression;
			public ObjectPath Path;
			public EvaluationOptions evalOptions;

			public ObjectValue VisitErrorValue(ErrorValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitPrimitiveValue(PrimitiveValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitVoidValue(VoidValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitArrayValue(ArrayValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitAssociativeArrayValue(AssociativeArrayValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitDelegateValue(DelegateValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitNullValue(NullValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitTypeOverloadValue(InternalOverloadValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitVariableValue(VariableValue v)
			{
				throw new NotImplementedException();
			}

			public ObjectValue VisitTypeValue(TypeValue v)
			{
				throw new NotImplementedException();
			}
		}
    }
}
