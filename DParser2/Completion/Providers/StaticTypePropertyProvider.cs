using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	public class StaticTypePropertyProvider
	{
		public class StaticProperty
		{
			public readonly string Name;
			public readonly string Description;
			public readonly ITypeDeclaration OverrideType;

			public StaticProperty(string name, string desc, ITypeDeclaration overrideType = null)
			{ Name = name; Description = desc; OverrideType = overrideType; }
		}

		public static StaticProperty[] GenericProps = new[]{
				new StaticProperty("sizeof","Size of a type or variable in bytes",new IdentifierDeclaration("size_t")),
				new StaticProperty("alignof","Variable offset",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("mangleof","String representing the ‘mangled’ representation of the type",new IdentifierDeclaration("string")),
				new StaticProperty("stringof","String representing the source representation of the type",new IdentifierDeclaration("string")),
			};

		public static StaticProperty[] IntegralProps = new[] { 
				new StaticProperty("max","Maximum value"),
				new StaticProperty("min","Minimum value")
			};

		public static StaticProperty[] FloatingTypeProps = new[] { 
				new StaticProperty("infinity","Infinity value"),
				new StaticProperty("nan","Not-a-Number value"),
				new StaticProperty("dig","Number of decimal digits of precision",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("epsilon", "Smallest increment to the value 1"),
				new StaticProperty("mant_dig","Number of bits in mantissa",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("max_10_exp","Maximum int value such that 10^max_10_exp is representable",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("max_exp","Maximum int value such that 2^max_exp-1 is representable",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("min_10_exp","Minimum int value such that 10^max_10_exp is representable",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("min_exp","Minimum int value such that 2^max_exp-1 is representable",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("min_normal","Number of decimal digits of precision",new DTokenDeclaration(DTokens.Int)),
				new StaticProperty("re","Real part"),
				new StaticProperty("in","Imaginary part")
			};

		public static StaticProperty[] ClassTypeProps = new[]{
				new StaticProperty("classinfo","Information about the dynamic type of the class", new IdentifierDeclaration("TypeInfo_Class") { ExpressesVariableAccess=true, InnerDeclaration = new IdentifierDeclaration("object") })
			};

		public static StaticProperty[] ArrayProps = new[] { 
				new StaticProperty("length","Array length",new IdentifierDeclaration("size_t")),
				new StaticProperty("dup","Create a dynamic array of the same size and copy the contents of the array into it."),
				new StaticProperty("idup","D2.0 only! Creates immutable copy of the array"),
				new StaticProperty("reverse","Reverses in place the order of the elements in the array. Returns the array."),
				new StaticProperty("sort","Sorts in place the order of the elements in the array. Returns the array.")
			};

		// Associative Arrays' properties have to be inserted manually

		static void CreateArtificialProperties(StaticProperty[] Properties, ICompletionDataGenerator cdg, ITypeDeclaration DefaultPropType = null)
		{
			foreach (var prop in Properties)
			{
				var p = new DVariable()
				{
					Name = prop.Name,
					Description = prop.Description,
					Type = prop.OverrideType != null ? prop.OverrideType : DefaultPropType
				};

				cdg.Add(p);
			}
		}

		/// <summary>
		/// Adds init, sizeof, alignof, mangleof, stringof to the completion list
		/// </summary>
		public static void AddGenericProperties(ResolveResult rr, ICompletionDataGenerator cdg, INode relatedNode = null, bool DontAddInitProperty = false)
		{
			if (!DontAddInitProperty)
			{
				var prop_Init = new DVariable();

				if (relatedNode != null)
					prop_Init.AssignFrom(relatedNode);

				// Override the initializer variable's name and description
				prop_Init.Name = "init";
				prop_Init.Description = "A type's or variable's static initializer expression";

				cdg.Add(prop_Init);
			}

			CreateArtificialProperties(GenericProps, cdg);
		}

		/// <summary>
		/// Adds init, max, min to the completion list
		/// </summary>
		public static void AddIntegralTypeProperties(int TypeToken, ResolveResult rr, ICompletionDataGenerator cdg, INode relatedNode = null, bool DontAddInitProperty = false)
		{
			var intType = new DTokenDeclaration(TypeToken);

			if (!DontAddInitProperty)
			{
				var prop_Init = new DVariable() { Type = intType, Initializer = new IdentifierExpression(0, LiteralFormat.Scalar) };

				if (relatedNode != null)
					prop_Init.AssignFrom(relatedNode);

				// Override the initializer variable's name and description
				prop_Init.Name = "init";
				prop_Init.Description = "A type's or variable's static initializer expression";

				cdg.Add(prop_Init);
			}

			CreateArtificialProperties(IntegralProps, cdg, intType);
		}

		public static void AddFloatingTypeProperties(int TypeToken, ResolveResult rr, ICompletionDataGenerator cdg, INode relatedNode = null, bool DontAddInitProperty = false)
		{
			var intType = new DTokenDeclaration(TypeToken);

			if (!DontAddInitProperty)
			{
				var prop_Init = new DVariable() { 
					Type = intType, 
					Initializer = new PostfixExpression_Access() { 
						PostfixForeExpression = new TokenExpression(TypeToken), 
						AccessExpression = new IdentifierExpression("nan")
					}
				};

				if (relatedNode != null)
					prop_Init.AssignFrom(relatedNode);

				// Override the initializer variable's name and description
				prop_Init.Name = "init";
				prop_Init.Description = "A type's or variable's static initializer expression";

				cdg.Add(prop_Init);
			}

			CreateArtificialProperties(FloatingTypeProps, cdg, intType);
		}

		public static void AddClassTypeProperties(ICompletionDataGenerator cdg, INode relatedNode = null)
		{
			CreateArtificialProperties(ClassTypeProps, cdg);
		}

		public static void AddArrayProperties(ResolveResult rr, ICompletionDataGenerator cdg, ArrayDecl ArrayDecl = null)
		{
			CreateArtificialProperties(ArrayProps, cdg, ArrayDecl);

			cdg.Add(new DVariable
			{
				Name = "ptr",
				Description = "Returns pointer to the array",
				Type = new PointerDecl(ArrayDecl == null ? new DTokenDeclaration(DTokens.Void) : ArrayDecl.ValueType)
			});
		}

		public static void AddAssocArrayProperties(ResolveResult rr, ICompletionDataGenerator cdg, ArrayDecl ad)
		{
			var ll = new List<INode>();

			/*ll.Add(new DVariable()
			{
				Name = "sizeof",
				Description = "Returns the size of the reference to the associative array; it is typically 8.",
				Type = new IdentifierDeclaration("size_t"),
				Initializer = new IdentifierExpression(8, LiteralFormat.Scalar)
			});*/

			ll.Add(new DVariable() { 
				Name="length",
				Description="Returns number of values in the associative array. Unlike for dynamic arrays, it is read-only.",
				Type = new IdentifierDeclaration("size_t"),
				Initializer= ad!=null? ad.KeyExpression : null
			});

			if (ad != null)
			{
				ll.Add(new DVariable()
				{
					Name = "keys",
					Description = "Returns dynamic array, the elements of which are the keys in the associative array.",
					Type = new ArrayDecl() { ValueType = ad.KeyType }
				});

				ll.Add(new DVariable()
				{
					Name = "values",
					Description = "Returns dynamic array, the elements of which are the values in the associative array.",
					Type = new ArrayDecl() { ValueType = ad.ValueType }
				});

				ll.Add(new DVariable()
				{
					Name = "rehash",
					Description = "Reorganizes the associative array in place so that lookups are more efficient. rehash is effective when, for example, the program is done loading up a symbol table and now needs fast lookups in it. Returns a reference to the reorganized array.",
					Type = ad
				});

				ll.Add(new DVariable()
				{
					Name = "byKey",
					Description = "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the keys of the associative array.",
					Type = new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = ad.KeyType } }
				});

				ll.Add(new DVariable()
				{
					Name = "byValue",
					Description = "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the values of the associative array.",
					Type = new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = ad.ValueType } }
				});

				ll.Add(new DMethod()
				{
					Name = "get",
					Description = "Looks up key; if it exists returns corresponding value else evaluates and returns defaultValue.",
					Type = ad.ValueType,
					Parameters = new List<INode> {
						new DVariable(){
							Name="key",
							Type=ad.KeyType
						},
						new DVariable(){
							Name="defaultValue",
							Type=ad.ValueType,
							Attributes=new List<DAttribute>{ new DAttribute(DTokens.Lazy)}
						}
					}
				});
			}

			foreach (var prop in ll)
				cdg.Add(prop);
		}
	}
}
