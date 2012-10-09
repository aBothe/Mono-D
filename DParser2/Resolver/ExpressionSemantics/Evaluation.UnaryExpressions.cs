using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(UnaryExpression x)
		{
			if (x is NewExpression)
				return E((NewExpression)x);
			else if (x is CastExpression)
				return E((CastExpression)x);
			else if (x is UnaryExpression_Cat)
				return E((UnaryExpression_Cat)x);
			else if (x is UnaryExpression_Increment)
				return E((UnaryExpression_Increment)x);
			else if (x is UnaryExpression_Decrement)
				return E((UnaryExpression_Decrement)x);
			else if (x is UnaryExpression_Add)
				return E((UnaryExpression_Add)x);
			else if (x is UnaryExpression_Sub)
				return E((UnaryExpression_Sub)x);
			else if (x is UnaryExpression_Not)
				return E((UnaryExpression_Not)x);
			else if (x is UnaryExpression_Mul)
				return E((UnaryExpression_Mul)x);
			else if (x is UnaryExpression_And)
				return E((UnaryExpression_And)x);
			else if (x is DeleteExpression)
				return E((DeleteExpression)x);
			else if (x is UnaryExpression_Type)
				return E((UnaryExpression_Type)x);

			return null;
		}

		ISemantic E(NewExpression nex)
		{
			// http://www.d-programming-language.org/expression.html#NewExpression
			ISemantic[] possibleTypes = null;

			if (nex.Type is IdentifierDeclaration)
				possibleTypes = TypeDeclarationResolver.Resolve((IdentifierDeclaration)nex.Type, ctxt, filterForTemplateArgs: false);
			else
				possibleTypes = TypeDeclarationResolver.Resolve(nex.Type, ctxt);

			var ctors = new Dictionary<DMethod, TemplateIntermediateType>();

			if (possibleTypes == null)
				return null;

			foreach (var t in possibleTypes)
			{
				var ct = t as TemplateIntermediateType;
				if (ct!=null && 
					!ct.Definition.ContainsAttribute(DTokens.Abstract))
					foreach (var ctor in GetConstructors(ct))
						ctors.Add(ctor, ct);
			}

			MemberSymbol finalCtor = null;

			var kvArray = ctors.ToArray();

			/*
			 * TODO: Determine argument types and filter out ctor overloads.
			 */

			if (kvArray.Length != 0)
				finalCtor = new MemberSymbol(kvArray[0].Key, kvArray[0].Value, nex);
			else if (possibleTypes.Length != 0)
				return AbstractType.Get(possibleTypes[0]);

			return finalCtor;
		}

		/// <summary>
		/// Returns all constructors from the given class or struct.
		/// If no explicit constructor given, an artificial implicit constructor method stub will be created.
		/// </summary>
		public static IEnumerable<DMethod> GetConstructors(TemplateIntermediateType ct, bool canCreateExplicitStructCtor = true)
		{
			bool foundExplicitCtor = false;

			// Simply get all constructors that have the ctor id assigned. Makin' it faster ;)
			var ch = ct.Definition[DMethod.ConstructorIdentifier];
			if(ch!=null)
				foreach (var m in ch)
				{
					// Not to forget: 'this' aliases are also possible - so keep checking for m being a genuine ctor
					var dm = m as DMethod;
					if (dm!=null && dm.SpecialType == DMethod.MethodType.Constructor)
					{
						yield return dm;
						foundExplicitCtor = true;
					}
				}

			var isStruct = ct is StructType;
			if (!foundExplicitCtor || isStruct)
			{
				// Check if there is an opCall that has no parameters.
				// Only if no exists, it's allowed to make a default parameter.
				bool canMakeDefaultCtor = true;
				foreach(var opCall in GetOpCalls(ct))
					if(opCall.Parameters == null || opCall.Parameters.Count == 0)
					{
						canMakeDefaultCtor = false;
						break;
					}

				if(canMakeDefaultCtor)
					yield return new DMethod(DMethod.MethodType.Constructor) { Name = DMethod.ConstructorIdentifier, Parent = ct.Definition, Description = "Default constructor for " + ct.Name };
				
				// If struct, there's also a ctor that has all struct members as parameters.
				// Only, if there are no explicit ctors nor opCalls
				if (isStruct && !foundExplicitCtor && canCreateExplicitStructCtor)
				{
					var l = new List<INode>();

					foreach (var member in ct.Definition)
					{
						var dv = member as DVariable;
						if (dv!=null && 
							!dv.IsStatic && 
							!dv.IsAlias && 
							!dv.IsConst) //TODO dunno if public-ness of items is required..
							l.Add(dv);
					}

					yield return new DMethod(DMethod.MethodType.Constructor) { 
						Name = DMethod.ConstructorIdentifier,
						Parent = ct.Definition,
						Description = "Default constructor for struct "+ct.Name,
						Parameters = l
					};
				}
			}
		}

		public static IEnumerable<DMethod> GetOpCalls(TemplateIntermediateType t)
		{
			var opCall = t.Definition["opCall"];
			if(opCall!=null)
				foreach(var call in opCall)
				{
					var dm = call as DMethod;
					if(dm != null)
						yield return dm;
				}
		}

		ISemantic E(CastExpression ce)
		{
			AbstractType castedType = null;

			if (ce.Type != null)
			{
				var castedTypes = TypeDeclarationResolver.Resolve(ce.Type, ctxt);

				ctxt.CheckForSingleResult(castedTypes, ce.Type);

				if (castedTypes != null && castedTypes.Length != 0)
					castedType = castedTypes[0];
			}
			else
			{
				castedType = AbstractType.Get(E(ce.UnaryExpression));

				if (castedType != null && ce.CastParamTokens != null && ce.CastParamTokens.Length > 0)
				{
					//TODO: Wrap resolved type with member function attributes
				}
			}

			return castedType;
		}

		ISemantic E(UnaryExpression_Cat x) // a = ~b;
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_Increment x)
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_Decrement x)
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_Add x)
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_Sub x)
		{
			var v = E(x.UnaryExpression);

			if (eval)
			{
				if (v is AbstractType)
					v = DResolver.StripMemberSymbols((AbstractType)v);

				if (v is PrimitiveValue)
				{
					var pv = (PrimitiveValue)v;

					return new PrimitiveValue(pv.BaseTypeToken, -pv.Value, x, -pv.ImaginaryPart);
				}
			}

			return v;
		}

		ISemantic E(UnaryExpression_Not x)
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_Mul x)
		{
			return E(x.UnaryExpression);
		}

		ISemantic E(UnaryExpression_And x)
		{
			var ptrBase=E(x.UnaryExpression);

			if (eval)
			{
				// Create a new pointer
				// 
			}

			// &i -- makes an int* out of an int
			return new PointerType(AbstractType.Get(ptrBase), x);
		}

		ISemantic E(DeleteExpression x)
		{
			if (eval)
			{
				// Reset the content of the variable
			}

			return null;
		}

		ISemantic E(UnaryExpression_Type x)
		{
			var uat = x as UnaryExpression_Type;

			if (uat.Type == null)
				return null;

			var types = TypeDeclarationResolver.Resolve(uat.Type, ctxt);
			ctxt.CheckForSingleResult(types, uat.Type);

			if (types != null && types.Length != 0)
			{
				var id = new IdentifierDeclaration(uat.AccessIdentifier) { EndLocation = uat.EndLocation };

				// First off, try to resolve static properties
				var statProp = StaticPropertyResolver.TryResolveStaticProperties(types[0], uat.AccessIdentifier, ctxt, eval, id);

				if (statProp != null)
					return statProp;

				// If it's not the case, try the conservative way
				var res = TypeDeclarationResolver.Resolve(id, ctxt, types);

				ctxt.CheckForSingleResult(res, x);

				if (res != null && res.Length != 0)
					return res[0];
			}

			return null;
		}
	}
}
