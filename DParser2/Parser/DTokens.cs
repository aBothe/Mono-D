using System.Collections;
using System.Collections.Generic;
using D_Parser.Dom;
using System;

namespace D_Parser.Parser
{
    public class DTokens
    {
        // ----- terminal classes -----
        public const int EOF = 0;
        public const int Identifier = 1;
        public const int Literal = 2;

        // ----- special character -----
        public const int Assign = 3;
        public const int Plus = 4;
        public const int Minus = 5;
        public const int Times = 6;
        public const int Div = 7;
        public const int Mod = 8;
        public const int Colon = 9;
        public const int DoubleDot = 10; // ..
        public const int Semicolon = 11;
        public const int Question = 12;
        public const int Dollar = 13;
        public const int Comma = 14;
        public const int Dot = 15;
        public const int OpenCurlyBrace = 16;
        public const int CloseCurlyBrace = 17;
        public const int OpenSquareBracket = 18;
        public const int CloseSquareBracket = 19;
        public const int OpenParenthesis = 20;
        public const int CloseParenthesis = 21;
        public const int GreaterThan = 22;
        public const int LessThan = 23;
        public const int Not = 24;
        public const int LogicalAnd = 25;
        public const int LogicalOr = 26;
        public const int Tilde = 27;
        public const int BitwiseAnd = 28;
        public const int BitwiseOr = 29;
        public const int Xor = 30;
        public const int Increment = 31;
        public const int Decrement = 32;
        public const int Equal = 33;
        public const int NotEqual = 34;
        public const int GreaterEqual = 35;
        public const int LessEqual = 36;
        public const int ShiftLeft = 37;
        public const int PlusAssign = 38;
        public const int MinusAssign = 39;
        public const int TimesAssign = 40;
        public const int DivAssign = 41;
        public const int ModAssign = 42;
        public const int BitwiseAndAssign = 43;
        public const int BitwiseOrAssign = 44;
        public const int XorAssign = 45;
        public const int ShiftLeftAssign = 46;
        public const int TildeAssign = 47;
        public const int ShiftRightAssign = 48;
        public const int TripleRightShiftAssign = 49;

        // ----- keywords -----
        public const int Align = 50;
        public const int Asm = 51;
        public const int Assert = 52;
        public const int Auto = 53;
        public const int Body = 54;
        public const int Bool = 55;
        public const int Break = 56;
        public const int Byte = 57;
        public const int Case = 58;
        public const int Cast = 59;
        public const int Catch = 60;
        public const int Cdouble = 61;
        public const int Cent = 62;
        public const int Cfloat = 63;
        public const int Char = 64;
        public const int Class = 65;
        public const int Const = 66;
        public const int Continue = 67;
        public const int Creal = 68;
        public const int Dchar = 69;
        public const int Debug = 70;
        public const int Default = 71;
        public const int Delegate = 72;
        public const int Delete = 73;
        public const int Deprecated = 74;
        public const int Do = 75;
        public const int Double = 76;
        public const int Else = 77;
        public const int Enum = 78;
        public const int Export = 79;
        public const int Extern = 80;
        public const int False = 81;
        public const int Final = 82;
        public const int Finally = 83;
        public const int Float = 84;
        public const int For = 85;
        public const int Foreach = 86;
        public const int Foreach_Reverse = 87;
        public const int Function = 88;
        public const int Goto = 89;
        public const int Idouble = 90;
        public const int If = 91;
        public const int Ifloat = 92;
        public const int Import = 93;
        public const int Immutable = 94;
        public const int In = 95;
        public const int InOut = 96;
        public const int Int = 97;
        public const int Interface = 98;
        public const int Invariant = 99;
        public const int Ireal = 100;
        public const int Is = 101;
        public const int Lazy = 102;
        public const int Long = 103;
        public const int empty1 = 104;
        public const int Mixin = 105;
        public const int Module = 106;
        public const int New = 107;
        public const int Nothrow = 108;
        public const int Null = 109;
        public const int Out = 110;
        public const int Override = 111;
        public const int Package = 112;
        public const int Pragma = 113;
        public const int Private = 114;
        public const int Protected = 115;
        public const int Public = 116;
        public const int Pure = 117;
        public const int Real = 118;
        public const int Ref = 119;
        public const int Return = 120;
        public const int Scope = 121;
        public const int Shared = 122;
        public const int Short = 123;
        public const int Static = 124;
        public const int Struct = 125;
        public const int Super = 126;
        public const int Switch = 127;
        public const int Synchronized = 128;
        public const int Template = 129;
        public const int This = 130;
        public const int Throw = 131;
        public const int True = 132;
        public const int Try = 133;
        public const int Typedef = 134;
        public const int Typeid = 135;
        public const int Typeof = 136;
        public const int Ubyte = 137;
        public const int Ucent = 138;
        public const int Uint = 139;
        public const int Ulong = 140;
        public const int Union = 141;
        public const int Unittest = 142;
        public const int Ushort = 143;
        public const int Version = 144;
        public const int Void = 145;
        public const int Volatile = 146;
        public const int Wchar = 147;
        public const int While = 148;
        public const int With = 149;
        public const int __gshared = 150;
        public const int __thread = 151;
        public const int __traits = 152;
        public const int Abstract = 153;
        public const int Alias = 154;
        public const int PropertyAttribute = 155;
        public const int GoesTo = 156; // =>  (lambda expressions)
        public const int INVALID = 157;
        public const int empty4 = 158;

        // Additional operators
        public const int PowAssign = 159; // ^^=
        public const int NotUnequalAssign = 160; // !<>=
        public const int NotUnequal = 161; // !<>
        public const int Unequal = 162; // <>
        public const int UnequalAssign = 163; // <>=
        public const int NotGreaterThan = 164; // !>
        public const int NotGreaterThanAssign = 165; // !>=
        public const int NotLessThan = 166; // !<
        public const int NotLessThanAssign = 167; // !<=
        public const int ShiftRight = 168; // >>
        public const int ShiftRightUnsigned = 169; // >>>
        public const int Pow = 170; // ^^

        public const int TripleDot = 171; // ...

		// Meta tokens
        public const int __VERSION__ = 172;
        public const int __FILE__ = 173;
        public const int __LINE__ = 174;
        public const int __EOF__ = 175;

		public const int __DATE__ = 176;
		public const int __TIME__ = 177;
		public const int __TIMESTAMP__ = 178;
		public const int __VENDOR__ = 179;

        public const int MaxToken = 180;
        public static BitArray NewSet(params int[] values)
        {
            BitArray bitArray = new BitArray(MaxToken);
            foreach (int val in values)
            {
                bitArray[val] = true;
            }
            return bitArray;
        }

        public static readonly Dictionary<int, string> Keywords = new Dictionary<int, string>
        {
            {__gshared,"__gshared"},
            {__thread,	    "__thread"},
            {__traits,	    "__traits"},

            {__LINE__,"__LINE__"},
            {__FILE__,"__FILE__"},
            {__EOF__,"__EOF__"},

			{__VERSION__,"__VERSION__"},
			{__DATE__,"__DATE__"},
			{__TIME__,"__TIME__"},
			{__TIMESTAMP__,"__TIMESTAMP__"},
			{__VENDOR__,"__VENDOR__"},

            {Abstract,"abstract"},
            {Alias,"alias"},
            {Align,"align"},
            {Asm,"asm"},
            {Assert,"assert"},
            {Auto,"auto"},
            {Body,"body"},
            {Bool,"bool"},
            {Break,"break"},
            {Byte,"byte"},

            {Case,"case"},
            {Cast,"cast"},
            {Catch,"catch"},{Cdouble,	"cdouble"},
            {Cent,	"cent"},
            {Cfloat,	"cfloat"},
            {Char,
	"char"},{Class,
	"class"},{Const,
	"const"},{Continue,
	"continue"},{Creal,
	"creal"},{Dchar,
	"dchar"},{Debug,
	"debug"},{Default,
	"default"},{Delegate,
	"delegate"},{Delete,
	"delete"},{Deprecated,
	"deprecated"},{Do,
	"do"},{Double,
	"double"},{Else,

	"else"},{Enum,
	"enum"},{Export,
	"export"},{Extern,
	"extern"},{False,

	"false"},{Final,
	"final"},{Finally,
	"finally"},{Float,
	"float"},{For,
	"for"},{Foreach,
	"foreach"},{Foreach_Reverse,
	"foreach_reverse"},{Function,
	"function"},{Goto,

	"goto"},{Idouble,

	"idouble"},{If,
	"if"},{Ifloat,
	"ifloat"},{Import,
	"import"},{Immutable,
	"immutable"},{In,
	"in"},{InOut,
	"inout"},{Int,
	"int"},{Interface,
	"interface"},{Invariant,
	"invariant"},{Ireal,
	"ireal"},{Is,
	"is"},{Lazy,

	"lazy"},{Long,
	"long"},{empty1,

	"macro"},{Mixin,
	"mixin"},{Module,
	"module"},{New,

	"new"},{Nothrow,
	"nothrow"},{Null,
	"null"},{Out,

	"out"},{Override,
	"override"},{Package,

	"package"},{Pragma,
	"pragma"},{Private,
	"private"},{Protected,
	"protected"},{Public,
	"public"},{Pure,
	"pure"},{Real,

	"real"},{Ref,
	"ref"},{Return,
	"return"},{Scope,

	"scope"},{Shared,
	"shared"},{Short,
	"short"},{Static,
	"static"},{Struct,
	"struct"},{Super,
	"super"},{Switch,
	"switch"},{Synchronized,
	"synchronized"},{Template,

	"template"},{This,
	"this"},{Throw,
	"throw"},{True,
	"true"},{Try,
	"try"},{Typedef,
	"typedef"},{Typeid,
	"typeid"},{Typeof,
	"typeof"},
    
    {Ubyte,	"ubyte"},
    {Ucent,	"ucent"},
    {Uint,	"uint"},
    {Ulong,	"ulong"},
    {Union,	"union"},
    {Unittest,	"unittest"},
    {Ushort,	"ushort"},

    {Version,	"version"},
    {Void,	"void"},
    {Volatile,	"volatile"},

    {Wchar,	"wchar"},
    {While,	"while"},
    {With,	"with"}
        };

        public static BitArray FunctionAttribute = NewSet(Pure, Nothrow, PropertyAttribute/*, DisabledAttribute, SafeAttribute, SystemAttribute, TrustedAttribute*/);
        public static BitArray MemberFunctionAttribute = NewSet(Const, Immutable, Shared, InOut, Pure, Nothrow, PropertyAttribute);
        public static BitArray ParamModifiers = NewSet(In, Out, InOut, Ref, Lazy, Scope);
        public static BitArray ClassLike = NewSet(Class, Template, Interface, Struct, Union);
        public static BitArray BasicTypes = NewSet(Bool, Byte, Ubyte, Short, Ushort, Int, Uint, Long, Ulong, Char, Wchar, Dchar, Float, Double, Real, Ifloat, Idouble, Ireal, Cfloat, Cdouble, Creal, Void);

		public static BitArray BasicTypes_Integral = NewSet(Bool, Byte,Ubyte,Short,Ushort,Int,Uint,Long,Ulong,Cent, Ucent, Char,Wchar, Dchar);
		public static BitArray BasicTypes_FloatingPoint = NewSet(Float,Double,Real,Ifloat,Idouble,Ireal,Cfloat,Cdouble,Creal);
		
		public static BitArray AssnStartOp = NewSet(Plus, Minus, Not, Tilde, Times);
        public static BitArray AssignOps = NewSet(
            Assign, // =
            PlusAssign, // +=
            MinusAssign, // -=
            TimesAssign, // *=
            DivAssign, // /=
            ModAssign, // %=
            BitwiseAndAssign, // &=
            BitwiseOrAssign, // |=
            XorAssign, // ^=
            TildeAssign, // ~=
            ShiftLeftAssign, // <<=
            ShiftRightAssign, // >>=
            TripleRightShiftAssign,// >>>=
            PowAssign,
            LessEqual,
            GreaterEqual,
            NotUnequalAssign,
            UnequalAssign,
            NotGreaterThanAssign,
            NotLessThanAssign
            );
        public static BitArray TypeDeclarationKW = NewSet(Class, Interface, Struct, Template, Enum, Delegate, Function);
        public static BitArray RelationalOperators = NewSet(
            LessThan,
            LessEqual,
            GreaterThan,
            GreaterEqual,
            //NotUnequalAssign, // !<>=
            NotUnequal, // !<>
            Unequal,
            UnequalAssign,
            NotGreaterThan,
            //NotGreaterThanAssign,
            NotLessThan
            //NotLessThanAssign
            );
        public static BitArray VisModifiers = NewSet(Public, Protected, Private, Package);
        public static BitArray Modifiers = NewSet(
            In,
            Out,
            InOut,
            Ref,
            Static,
            Override,
            Const,
            Public,
            Private,
            Protected,
            Package,
            Export,
            Shared,
            Final,
            Invariant,
            Immutable,
            Pure,
            Deprecated,
            Scope,
            __gshared,
            __thread,
            Lazy,
            Nothrow
            );
        public static BitArray StorageClass = NewSet(
            Abstract
            ,Auto
            ,Const
            ,Deprecated
            ,Extern
            ,Final
            ,Immutable
            ,InOut
            ,Shared
	        ,Nothrow
            ,Override
	        ,Pure
            ,Scope
            ,Static
            ,Synchronized
			,__gshared
            );

        /// <summary>
        /// Checks if modifier array contains member attributes. If so, it returns the last found attribute. Otherwise 0.
        /// </summary>
        /// <param name="mods"></param>
        /// <returns></returns>
        public static DAttribute ContainsStorageClass(DAttribute[] mods)
        {
            var r=DAttribute.Empty;
            foreach (var attr in mods)
            {
                if (attr.IsStorageClass || attr.IsProperty)
                    r = attr;
            }
            return r;
        }


        public static bool ContainsVisMod(List<int> mods)
        {
            return
            mods.Contains(Public) ||
            mods.Contains(Private) ||
            mods.Contains(Package) ||
            mods.Contains(Protected);
        }

        public static void RemoveVisMod(List<int> mods)
        {
            while (mods.Contains(Public))
                mods.Remove(Public);
            while (mods.Contains(Private))
                mods.Remove(Private);
            while (mods.Contains(Protected))
                mods.Remove(Protected);
            while (mods.Contains(Package))
                mods.Remove(Package);
        }

		static Dictionary<int, string> NonKeywords = new Dictionary<int, string> {
			// Meta
			{INVALID,"<Invalid Token>"},
			{EOF,"<EOF>"},
			{Identifier,"<Identifier>"},
			{Literal,"<Literal>"},

			// Math operations
			{Assign,"="},
			{Plus,"+"},
			{Minus,"-"},
			{Times,"*"},
			{Div,"/"},
			{Mod,"%"},
			{Pow,"^^"},

			// Special chars
			{Dot,"."},
			{DoubleDot,".."},
			{TripleDot,"..."},
			{Colon,":"},
			{Semicolon,";"},
			{Question,"?"},
			{Dollar,"$"},
			{Comma,","},
			
			// Brackets
			{OpenCurlyBrace,"{"},
			{CloseCurlyBrace,"}"},
			{OpenSquareBracket,"["},
			{CloseSquareBracket,"]"},
			{OpenParenthesis,"("},
			{CloseParenthesis,")"},

			// Relational
			{GreaterThan,">"},
			{NotGreaterThan,"!>"},
			{LessThan,"<"},
			{NotLessThan,"!<"},
			{Not,"!"},
			{Unequal,"<>"},
			{NotUnequal,"!<>"},
			{LogicalAnd,"&&"},
			{LogicalOr,"||"},
			{Tilde,"~"},
			{BitwiseAnd,"&"},
			{BitwiseOr,"|"},
			{Xor,"^"},

			// Shift
			{ShiftLeft,"<<"},
			{ShiftRight,">>"},
			{ShiftRightUnsigned,">>>"},

			// Increment
			{Increment,"++"},
			{Decrement,"--"},

			// Assign operators
			{Equal,"=="},
			{NotEqual,"!="},
			{GreaterEqual,">="},
			{LessEqual,"<="},
			{PlusAssign,"+="},
			{MinusAssign,"-="},
			{TimesAssign,"*="},
			{DivAssign,"/="},
			{ModAssign,"%="},
			{BitwiseOrAssign,"|="},
			{XorAssign,"^="},
			{TildeAssign,"~="},

			{ShiftLeftAssign,"<<="},
			{ShiftRightAssign,">>="},
			{TripleRightShiftAssign,">>>="},
			
			{PowAssign,"^^="},
			{UnequalAssign,"<>="},
			{NotUnequalAssign,"!<>="},
			{NotGreaterThanAssign,"!>="},
			{NotLessThanAssign,"!<="},

			{GoesTo,"=>"}
		};

        public static string GetTokenString(int token)
        {
			if (Keywords.ContainsKey(token))
				return Keywords[token];
			if (NonKeywords.ContainsKey(token))
				return NonKeywords[token];

			return "<Unknown>";
        }

        public static int GetTokenID(string token)
        {
            if (token == null || token.Length < 1) 
				return -1;

			foreach (var kv in Keywords)
				if (kv.Value == token)
					return kv.Key;

			foreach (var kv in NonKeywords)
				if (kv.Value == token)
					return kv.Key;

            return -1;
        }

        public static string GetDescription(string token)
        {
			if (token.StartsWith("@"))
			{
				var n=Environment.NewLine;
				if (token == "@disable")
					return @"Disables a declaration
A ref­er­ence to a de­c­la­ra­tion marked with the @dis­able at­tribute causes a com­pile time error. 

This can be used to ex­plic­itly dis­al­low cer­tain op­er­a­tions 
or over­loads at com­pile time 
rather than re­ly­ing on gen­er­at­ing a run­time error.";

				if (token == "@property")
					return 
@"Prop­erty func­tions 
can be called with­out paren­the­ses (hence act­ing like prop­er­ties).

struct S {
  int m_x;
  @property {
    int x() { return m_x; }
    int x(int newx) { return m_x = newx; }
  }
}

void foo() {
  S s;
  s.x = 3;   // calls s.x(int)
  bar(s.x);  // calls bar(s.x())
}";

				if (token == "@safe")
					return @"Safe func­tions

The fol­low­ing op­er­a­tions are not al­lowed in safe func­tions:

- No cast­ing from a pointer type 
  to any type other than void*.
- No cast­ing from any non-pointer 
  type to a pointer type.
- No mod­i­fi­ca­tion of pointer val­ues.
- Can­not ac­cess unions that have point­ers or 
  ref­er­ences over­lap­ping with other types.
- Call­ing any sys­tem func­tions.
- No catch­ing of ex­cep­tions that 
  are not de­rived from class Ex­cep­tion.
- No in­line as­sem­bler.
- No ex­plicit cast­ing of mu­ta­ble ob­jects to im­mutable.
- No ex­plicit cast­ing of im­mutable ob­jects to mu­ta­ble.
- No ex­plicit cast­ing of thread local ob­jects to shared.
- No ex­plicit cast­ing of shared ob­jects to thread local.
- No tak­ing the ad­dress of a local 
  vari­able or func­tion pa­ra­me­ter.
- Can­not ac­cess __gshared vari­ables.
- Func­tions nested in­side safe 
  func­tions de­fault to being safe func­tions.

Safe func­tions are co­vari­ant with trusted or sys­tem func­tions.";


				if (token == "@system")
					return @"Sys­tem func­tions 
are func­tions not marked with @safe or @trusted and are not nested in­side @safe func­tions. 

Sys­tem func­tions may be marked with the @sys­tem at­tribute.
 
A func­tion being sys­tem does not mean it ac­tu­ally is un­safe, it just means that the com­piler is un­able to ver­ify that it can­not ex­hibit un­de­fined be­hav­ior.

Sys­tem func­tions are not co­vari­ant with trusted or safe func­tions.";


				if (token == "@trusted")
					return string.Join(Environment.NewLine, "Trusted func­tions","",
"- Are marked with the @trusted at­tribute,",
@"- Are guar­an­teed by the pro­gram­mer to not ex­hibit 
  any un­de­fined be­hav­ior if called by a safe func­tion,",
"- May call safe, trusted, or sys­tem func­tions,",
"- Are co­vari­ant with safe or sys­tem func­tions");
			}

            return GetDescription(GetTokenID(token));
        }

        public static string GetDescription(int token)
        {
            switch (token)
            {
                case Else:
                case If:
                    return "if(a == b)\n{\n   foo();\n}\nelse if(a < b)\n{\n   ...\n}\nelse\n{\n   bar();\n}";
                case For:
                    return "for(int i; i<500; i++)\n{\n   foo();\n}";
                case Foreach_Reverse:
                case Foreach: return
                    "foreach"+(token==Foreach_Reverse?"_reverse":"")+
					"(element; array)\n{\n   foo(element);\n}\n\nOr:\nforeach" + (token == Foreach_Reverse ? "_reverse" : "") + 
					"(element, index; array)\n{\n   foo(element);\n}";
                case While:
                    return "while(a < b)\n{\n   foo();\n   a++;\n}";
                case Do:
                    return "do\n{\n   foo();\na++;\n}\nwhile(a < b);";
                case Switch:
                    return "switch(a)\n{\n   case 1:\n      foo();\n      break;\n   case 2:\n      bar();\n      break;\n   default:\n      break;\n}";
                default: return "D Keyword";
            }
        }
    }
}
