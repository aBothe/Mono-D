using D_Parser.Dom;
using D_Parser.Parser;
using MonoDevelop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Completion
{
	public class DIcons : NodeVisitor<IconId>
	{
		readonly static DIcons Instance = new DIcons();
		static readonly Dictionary<string, IconId> images = new Dictionary<string, IconId>();

		static DIcons()
		{
			foreach (var f in typeof(DIcons).GetFields())
			{
				if (f.FieldType == typeof(IconId))
					images.Add(f.Name.ToLower(), (IconId)f.GetValue(null));
			}
		}

		public static IconId GetNodeIcon(DNode n)
		{
			if (n == null)
				return IconId.Null;

			return n.Accept(Instance);
		}

		#region LowLevel
		/// <summary>
		/// Returns node icon id looked up from the provided base string plus the protection
		/// attribute (and, optionally, the staticness) of node.
		/// </summary>
		static IconId iconIdWithProtectionAttr(DNode n, string image,
														bool allow_static = false)
		{
			string attr = "";

			if (allow_static && n.ContainsAttribute(DTokens.Static))
			{
				attr += "static_";
			}

			if (n.ContainsAttribute(DTokens.Package))
				return GetNodeImage(attr + image + "_internal");
			else if (n.ContainsAttribute(DTokens.Protected))
				return GetNodeImage(attr + image + "_protected");
			else if (n.ContainsAttribute(DTokens.Private))
				return GetNodeImage(attr + image + "_private");
			return GetNodeImage(attr + image);
		}

		/// <summary>
		/// Returns node icon id for a class, including the protection attribute, staticness and
		/// abstractness.
		/// </summary>
		static IconId classIconIdWithProtectionAttr(DNode n)
		{
			// Only nested class may be static
			bool static_allowed = n.IsClassMember;

			string attr = "";
			if (n.ContainsAttribute(DTokens.Abstract))
			{
				attr += "abstract_";
			}

			return iconIdWithProtectionAttr(n, attr + "class", static_allowed);
		}

		/// <summary>
		/// Returns node icon id for a method, including the protection attribute, staticness
		/// and abstractness.
		/// </summary>
		static IconId methodIconIdWithProtectionAttr(DNode n)
		{
			string attr = "";

			// Only class (not struct) methods, and neither static nor private methods may
			// be abstract/override
			if (n.IsClassMember &&
				!n.ContainsAttribute(DTokens.Static) && !n.ContainsAttribute(DTokens.Private))
			{
				if (n.ContainsAttribute(DTokens.Abstract))
				{
					attr += "abstract_";
				}
				else if (n.ContainsAttribute(DTokens.Override))
				{
					attr += "override_";
				}
			}

			return iconIdWithProtectionAttr(n, attr + "method", true);
		}

		public static IconId GetNodeImage(string key)
		{
			IconId id;
			return images.TryGetValue(key, out id) ? id : null;
		}
		#endregion

		#region Class-like structures
		public static readonly IconId Template = new IconId("md-template");
		public static readonly IconId Template_Internal = new IconId("md-internal-template");
		public static readonly IconId Template_Private = new IconId("md-private-template");
		public static readonly IconId Template_Protected = new IconId("md-protected-template");

		#region Class
		public static readonly IconId Class = new IconId("md-class");
		public static readonly IconId Class_Internal = new IconId("md-internal-class");
		public static readonly IconId Class_Private = new IconId("md-private-class");
		public static readonly IconId Class_Protected = new IconId("md-protected-class");

		public static readonly IconId Static_Class = new IconId("md-class-static");
		public static readonly IconId Static_Class_Internal = new IconId("md-internal-class-static");
		public static readonly IconId Static_Class_Private = new IconId("md-private-class-static");
		public static readonly IconId Static_Class_Protected = new IconId("md-protected-class-static");

		public static readonly IconId Abstract_Class = new IconId("md-class-abstract");
		public static readonly IconId Abstract_Class_Internal = new IconId("md-internal-class-abstract");
		public static readonly IconId Abstract_Class_Private = new IconId("md-private-class-abstract");
		public static readonly IconId Abstract_Class_Protected = new IconId("md-protected-class-abstract");

		public static readonly IconId Static_Abstract_Class = new IconId("md-class-static-abstract");
		public static readonly IconId Static_Abstract_Class_Internal = new IconId("md-internal-class-static-abstract");
		public static readonly IconId Static_Abstract_Class_Private = new IconId("md-private-class-static-abstract");
		public static readonly IconId Static_Abstract_Class_Protected = new IconId("md-protected-class-static-abstract");
		#endregion

		public static readonly IconId Struct = new IconId("md-struct");
		public static readonly IconId Struct_Internal = new IconId("md-internal-struct");
		public static readonly IconId Struct_Private = new IconId("md-private-struct");
		public static readonly IconId Struct_Protected = new IconId("md-protected-struct");

		public static readonly IconId Interface = new IconId("md-interface");
		public static readonly IconId Interface_Internal = new IconId("md-internal-interface");
		public static readonly IconId Interface_Private = new IconId("md-private-interface");
		public static readonly IconId Interface_Protected = new IconId("md-protected-interface");

		public static readonly IconId Enum = new IconId("md-enum");
		public static readonly IconId Enum_Internal = new IconId("md-internal-enum");
		public static readonly IconId Enum_Private = new IconId("md-private-enum");
		public static readonly IconId Enum_Protected = new IconId("md-protected-enum");

		public static readonly IconId Union = new IconId("md-union");
		public static readonly IconId Union_Internal = new IconId("md-internal-union");
		public static readonly IconId Union_Private = new IconId("md-private-union");
		public static readonly IconId Union_Protected = new IconId("md-protected-union");
		#endregion

		#region Methods
		public static readonly IconId Method = new IconId("md-method");
		public static readonly IconId Method_Internal = new IconId("md-internal-method");
		public static readonly IconId Method_Private = new IconId("md-private-method");
		public static readonly IconId Method_Protected = new IconId("md-protected-method");

		public static readonly IconId Static_Method = new IconId("md-method-static");
		public static readonly IconId Static_Method_Internal = new IconId("md-internal-method-static");
		public static readonly IconId Static_Method_Private = new IconId("md-private-method-static");
		public static readonly IconId Static_Method_Protected = new IconId("md-protected-method-static");

		public static readonly IconId Abstract_Method = new IconId("md-method-abstract");
		public static readonly IconId Abstract_Method_Internal = new IconId("md-internal-method-abstract");
		public static readonly IconId Abstract_Method_Protected = new IconId("md-protected-method-abstract");

		public static readonly IconId Override_Method = new IconId("md-method-override");
		public static readonly IconId Override_Method_Internal = new IconId("md-internal-method-override");
		public static readonly IconId Override_Method_Protected = new IconId("md-protected-method-override");
		#endregion

		#region Variables
		public static readonly IconId Parameter = new IconId("d-parameter");
		public static readonly IconId Ref_Parameter = new IconId("d-ref-parameter");
		public static readonly IconId Out_Parameter = new IconId("d-out-parameter");
		public static readonly IconId Lazy_Parameter = new IconId("d-lazy-parameter");

		public static readonly IconId Local = new IconId("d-local");

		public static readonly IconId Field = new IconId("md-field");
		public static readonly IconId Field_Internal = new IconId("md-internal-field");
		public static readonly IconId Field_Private = new IconId("md-private-field");
		public static readonly IconId Field_Protected = new IconId("md-protected-field");

		public static readonly IconId Static_Field = new IconId("md-field-static");
		public static readonly IconId Static_Field_Internal = new IconId("md-internal-field-static");
		public static readonly IconId Static_Field_Private = new IconId("md-private-field-static");
		public static readonly IconId Static_Field_Protected = new IconId("md-protected-field-static");

		public static readonly IconId Alias = new IconId("d-alias");
		public static readonly IconId Alias_Internal = new IconId("d-internal-alias");
		public static readonly IconId Alias_Private = new IconId("d-private-alias");
		public static readonly IconId Alias_Protected = new IconId("d-protected-alias");

		public static readonly IconId Property = new IconId("md-property");
		public static readonly IconId Property_Internal = new IconId("md-internal-property");
		public static readonly IconId Property_Private = new IconId("md-private-property");
		public static readonly IconId Property_Protected = new IconId("md-protected-property");

		public static readonly IconId Delegate = new IconId("md-delegate");
		public static readonly IconId Delegate_Internal = new IconId("md-internal-delegate");
		public static readonly IconId Delegate_Private = new IconId("md-private-delegate");
		public static readonly IconId Delegate_Protected = new IconId("md-protected-delegate");

		public static readonly IconId Literal = new IconId("md-literal");
		public static readonly IconId Literal_Internal = new IconId("md-internal-literal");
		public static readonly IconId Literal_Private = new IconId("md-private-literal");
		public static readonly IconId Literal_Protected = new IconId("md-protected-literal");

		public static readonly IconId Static_Literal = new IconId("md-literal-static");
		public static readonly IconId Static_Literal_Internal = new IconId("md-internal-literal-static");
		public static readonly IconId Static_Literal_Private = new IconId("md-private-literal-static");
		public static readonly IconId Static_Literal_Protected = new IconId("md-protected-literal-static");
		#endregion

		#region Visitor
		public IconId Visit(DEnumValue n)
		{
			return GetNodeImage("literal");
		}

		public IconId Visit(DVariable dv)
		{
			if (dv.IsAlias)
			{
				// TODO: does declaring an alias private/protected/package actually have a meaning?
				return iconIdWithProtectionAttr(dv, "alias");
			}

			if (dv.ContainsPropertyAttribute())
			{
				return iconIdWithProtectionAttr(dv, "property");
			}

			if (dv.Type is DelegateDeclaration)
			{
				return iconIdWithProtectionAttr(dv, "delegate");
			}

			if (dv.ContainsAttribute(DTokens.Const) || (dv.ContainsAttribute(DTokens.Enum) && dv.Initializer == null))
			{
				return iconIdWithProtectionAttr(dv, "literal", true);
			}

			var realParent = dv.Parent as DNode;

			if (realParent is DClassLike || dv.Parent is DModule)
			{
				return iconIdWithProtectionAttr(dv, "field", true);
			}

			if (realParent is DMethod)
			{
				// FIXME: first parameter of class constructors is always displayed as a local, not a parameter
				if ((realParent as DMethod).Parameters.Contains(dv))
				{
					if (dv.ContainsAttribute(DTokens.Ref))
						return GetNodeImage("ref_parameter");
					else if (dv.ContainsAttribute(DTokens.Lazy))
						return GetNodeImage("lazy_parameter");
					else if (dv.ContainsAttribute(DTokens.Out))
						return GetNodeImage("out_parameter");
					else
						return GetNodeImage("parameter");
					// TODO: immutable, scope?
				}
				return GetNodeImage("local");
			}

			TemplateParameter tpar;
			if (realParent != null && realParent.TryGetTemplateParameter(dv.NameHash, out tpar))
				return GetNodeImage("parameter");

			return GetNodeImage("local");
		}

		public IconId Visit(DMethod n)
		{
			//TODO: Getter or setter functions should be declared as a >single< property only
			if (n.ContainsPropertyAttribute())
			{
				return iconIdWithProtectionAttr(n, "property");
			}

			return methodIconIdWithProtectionAttr(n);
		}

		public IconId Visit(DClassLike n)
		{
			switch (n.ClassType)
			{
				case DTokens.Template:
					return iconIdWithProtectionAttr(n, "template");

				default:
					return classIconIdWithProtectionAttr(n);

				case DTokens.Union:
					return iconIdWithProtectionAttr(n, "union");

				case DTokens.Struct:
					return iconIdWithProtectionAttr(n, "struct");

				case DTokens.Interface:
					return iconIdWithProtectionAttr(n, "interface");
			}
		}

		public IconId Visit(DEnum n)
		{
			// TODO: does declaring an enum private/protected/package actually have a meaning?
			return iconIdWithProtectionAttr(n, "enum");
		}

		public IconId Visit(DModule dModule)
		{
			return new IconId("d-file");
		}

		public IconId Visit(DBlockNode dBlockNode)
		{
			return IconId.Null;
		}

		public IconId Visit(TemplateParameter.Node n)
		{
			return iconIdWithProtectionAttr(n, "template");
		}

		public IconId Visit(NamedTemplateMixinNode n)
		{
			return iconIdWithProtectionAttr(n, "template");
		}



		public IconId Visit(EponymousTemplate n)
		{
			return iconIdWithProtectionAttr(n, "template");
		}

		public IconId Visit(ModuleAliasNode n)
		{
			return Visit(n as DVariable);
		}

		public IconId Visit(ImportSymbolNode n)
		{
			return Visit(n as DVariable);
		}

		public IconId Visit(ImportSymbolAlias n)
		{
			return Visit(n as DVariable);
		}

		#region unused
		public IconId VisitAttribute(Modifier attr)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(DeprecatedAttribute a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(PragmaAttribute attr)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(BuiltInAtAttribute a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(UserDeclarationAttribute a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(VersionCondition a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(DebugCondition a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(StaticIfCondition a)
		{
			throw new NotImplementedException();
		}

		public IconId VisitAttribute(NegatedDeclarationCondition a)
		{
			throw new NotImplementedException();
		}
		#endregion
		#endregion
	}
}
