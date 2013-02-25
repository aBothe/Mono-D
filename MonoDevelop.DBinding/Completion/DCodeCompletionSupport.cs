using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using ICSharpCode.NRefactory.Completion;
using MonoDevelop.D.Parser;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using System.Text;

namespace MonoDevelop.D.Completion
{
	public class DCodeCompletionSupport
	{
		public static EditorData CreateEditorData (Document EditorDocument)
		{
			var dpd = EditorDocument.ParsedDocument as ParsedDModule;
			var ctx = new CodeCompletionContext();

			ctx.TriggerLine = EditorDocument.Editor.Caret.Line;
			ctx.TriggerLineOffset = EditorDocument.Editor.Caret.Column;
			ctx.TriggerOffset = EditorDocument.Editor.Caret.Offset;

			return CreateEditorData(EditorDocument, dpd.DDom as DModule, ctx);
		}

		public static EditorData CreateEditorData (Document EditorDocument, DModule Ast, CodeCompletionContext ctx, char triggerChar = '\0')
		{
			bool removeChar = char.IsLetter(triggerChar) || triggerChar == '_' || triggerChar == '@';
			
			var deltaOffset = 0;//removeChar ? 1 : 0;
			
			var caretOffset = ctx.TriggerOffset - (removeChar ? 1 : 0);
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset-deltaOffset, ctx.TriggerLine);
			var codeCache = EnumAvailableModules(EditorDocument);
			
			var ed=new EditorData {
				CaretLocation=caretLocation,
				CaretOffset=caretOffset,
				ModuleCode=removeChar ? EditorDocument.Editor.Text.Remove(ctx.TriggerOffset-1,1) : EditorDocument.Editor.Text,
				SyntaxTree=Ast,
				ParseCache=codeCache,
				Options = DCompilerService.Instance.CompletionOptions
			};
			
			if(EditorDocument.HasProject)
			{
				var cfg = EditorDocument.Project.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;
				
				if(cfg!=null)
				{
					ed.GlobalDebugIds = cfg.CustomDebugIdentifiers;
					ed.IsDebug = cfg.DebugMode;
					ed.DebugLevel = cfg.DebugLevel;
					ed.GlobalVersionIds = cfg.GlobalVersionIdentifiers;
					double d;
					int v;
					if(Double.TryParse(EditorDocument.Project.Version, out d))
						ed.VersionNumber = (int)d;
					else if(Int32.TryParse(EditorDocument.Project.Version, out v))
						ed.VersionNumber = v;
				}
			}
			
			if(ed.GlobalVersionIds == null)
			{
				ed.GlobalVersionIds = VersionIdEvaluation.GetOSAndCPUVersions();
			}

			return ed;
		}

		public static void BuildCompletionData(Document EditorDocument, 
			DModule SyntaxTree, 
			CodeCompletionContext ctx, 
			CompletionDataList l, 
			char triggerChar)
		{
			AbstractCompletionProvider.BuildCompletionData(
				new CompletionDataGenerator { CompletionDataList = l },
				CreateEditorData(EditorDocument, SyntaxTree as DModule, ctx, triggerChar), 
				triggerChar=='\0'?null:triggerChar.ToString());
		}

		public static ResolutionContext CreateCurrentContext()
		{
			Document doc = null;
			DispatchService.GuiSyncDispatch(() => doc = Ide.IdeApp.Workbench.ActiveDocument);
			if (doc != null)
			{
				var ddoc = doc.ParsedDocument as ParsedDModule;
				if (ddoc != null)
				{
					var ast = ddoc.DDom;
					if (ast != null)
					{
						IStatement stmt;
						var caret = new D_Parser.Dom.CodeLocation(doc.Editor.Caret.Column, doc.Editor.Caret.Line);
						var bn = DResolver.SearchBlockAt(ast, caret, out stmt);
						var dbn = bn as DBlockNode;
						if (stmt == null && dbn != null)
						{
							//TODO: If inside an expression statement, search the nearest function call or template instance expression - and try to evaluate that one.

							if (dbn.StaticStatements.Count != 0)
							{
								foreach (var ss in dbn.StaticStatements)
								{
									if (caret >= ss.Location && caret <= ss.EndLocation)
									{
										stmt = ss;
										break;
									}
								}
							}
						}

						var ed = Completion.DCodeCompletionSupport.CreateEditorData(doc);
						return new ResolutionContext(ed.ParseCache, new ConditionalCompilationFlags(ed), bn, stmt);
					}
				}
			}
			return new ResolutionContext(Completion.DCodeCompletionSupport.EnumAvailableModules(),
					new ConditionalCompilationFlags(VersionIdEvaluation.GetOSAndCPUVersions(), 1, true), null);
		}

		#region Module enumeration helper
		public static ParseCacheList EnumAvailableModules(Document Editor)
		{
			return EnumAvailableModules(Editor.HasProject ? Editor.Project as DProject : null);
		}

		public static ParseCacheList EnumAvailableModules(DProject Project=null)
		{
			if (Project != null)
			{
				var pcl= ParseCacheList.Create(Project.LocalFileCache, Project.LocalIncludeCache, Project.Compiler.ParseCache);

				// Automatically include dep projects' caches
				foreach (var dep in Project.DependingProjects)
					if(dep!=null)
						pcl.Add(dep.LocalFileCache);

				return pcl;
			}
			else
				return ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);
		}
		#endregion

		#region Image helper
		static readonly Dictionary<string, Core.IconId> images = new Dictionary<string, IconId>();
		static bool wasInitialized = false;

		static void InitImages()
		{
			if (wasInitialized)
				return;

			try
			{
				#region Class-like structures
				images["template"] = new IconId("md-template");
				images["template_internal"] = new IconId("md-internal-template");
				images["template_private"] = new IconId("md-private-template");
				images["template_protected"] = new IconId("md-protected-template");

				#region Class
				images["class"] = new IconId("md-class");
				images["class_internal"] = new IconId("md-internal-class");
				images["class_private"] = new IconId("md-private-class");
				images["class_protected"] = new IconId("md-protected-class");

				images["static_class"] = new IconId("md-class-static");
				images["static_class_internal"] = new IconId("md-internal-class-static");
				images["static_class_private"] = new IconId("md-private-class-static");
				images["static_class_protected"] = new IconId("md-protected-class-static");

				images["abstract_class"] = new IconId("md-class-abstract");
				images["abstract_class_internal"] = new IconId("md-internal-class-abstract");
				images["abstract_class_private"] = new IconId("md-private-class-abstract");
				images["abstract_class_protected"] = new IconId("md-protected-class-abstract");

				images["static_abstract_class"] = new IconId("md-class-static-abstract");
				images["static_abstract_class_internal"] = new IconId("md-internal-class-static-abstract");
				images["static_abstract_class_private"] = new IconId("md-private-class-static-abstract");
				images["static_abstract_class_protected"] = new IconId("md-protected-class-static-abstract");
				#endregion

				images["struct"] = new IconId("md-struct");
				images["struct_internal"] = new IconId("md-internal-struct");
				images["struct_private"] = new IconId("md-private-struct");
				images["struct_protected"] = new IconId("md-protected-struct");

				images["interface"] = new IconId("md-interface");
				images["interface_internal"] = new IconId("md-internal-interface");
				images["interface_private"] = new IconId("md-private-interface");
				images["interface_protected"] = new IconId("md-protected-interface");

				images["enum"] = new IconId("md-enum");
				images["enum_internal"] = new IconId("md-internal-enum");
				images["enum_private"] = new IconId("md-private-enum");
				images["enum_protected"] = new IconId("md-protected-enum");

				images["union"] = new IconId("md-union");
				images["union_internal"] = new IconId("md-internal-union");
				images["union_private"] = new IconId("md-private-union");
				images["union_protected"] = new IconId("md-protected-union");
				#endregion

				#region Methods
				images["method"] = new IconId("md-method");
				images["method_internal"] = new IconId("md-internal-method");
				images["method_private"] = new IconId("md-private-method");
				images["method_protected"] = new IconId("md-protected-method");

				images["static_method"] = new IconId("md-method-static");
				images["static_method_internal"] = new IconId("md-internal-method-static");
				images["static_method_private"] = new IconId("md-private-method-static");
				images["static_method_protected"] = new IconId("md-protected-method-static");

				images["abstract_method"] = new IconId("md-method-abstract");
				images["abstract_method_internal"] = new IconId("md-internal-method-abstract");
				images["abstract_method_protected"] = new IconId("md-protected-method-abstract");

				images["override_method"] = new IconId("md-method-override");
				images["override_method_internal"] = new IconId("md-internal-method-override");
				images["override_method_protected"] = new IconId("md-protected-method-override");
				#endregion

				#region Variables
				images["parameter"] = new IconId("d-parameter");
				images["ref_parameter"] = new IconId("d-ref-parameter");
				images["out_parameter"] = new IconId("d-out-parameter");
				images["lazy_parameter"] = new IconId("d-lazy-parameter");

				images["local"] = new IconId("md-field"); // TODO: what's the difference between local & field?

				images["field"] = new IconId("md-field");
				images["field_internal"] = new IconId("md-internal-field");
				images["field_private"] = new IconId("md-private-field");
				images["field_protected"] = new IconId("md-protected-field");

				images["static_field"] = new IconId("md-field-static");
				images["static_field_internal"] = new IconId("md-internal-field-static");
				images["static_field_private"] = new IconId("md-private-field-static");
				images["static_field_protected"] = new IconId("md-protected-field-static");

                images["alias"] = new IconId("d-alias");
                images["alias_internal"] = new IconId("d-internal-alias");
                images["alias_private"] = new IconId("d-private-alias");
                images["alias_protected"] = new IconId("d-protected-alias");

				images["property"] = new IconId("md-property");
				images["property_internal"] = new IconId("md-internal-property");
				images["property_private"] = new IconId("md-privated-property");
				images["property_protected"] = new IconId("md-protected-property");

				images["delegate"] = new IconId("md-delegate");
				images["delegate_internal"] = new IconId("md-internal-delegate");
				images["delegate_private"] = new IconId("md-private-delegate");
				images["delegate_protected"] = new IconId("md-protected-delegate");

				images["literal"] = new IconId("md-literal");
				images["literal_private"] = new IconId("md-private-literal");
				images["literal_protected"] = new IconId("md-protected-literal");
				images["literal_internal"] = new IconId("md-internal-literal");

				images["static_literal"] = new IconId("md-literal-static");
				images["static_literal_private"] = new IconId("md-private-literal-static");
				images["static_literal_protected"] = new IconId("md-protected-literal-static");
				images["static_literal_internal"] = new IconId("md-internal-literal-static");
				#endregion
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error while filling icon array", ex);
			}

			wasInitialized = true;
		}

		public static Core.IconId GetNodeImage(string key)
		{
			if (!wasInitialized)
				InitImages();

			if (images.ContainsKey(key))
				return images[key];
			return null;
		}
		#endregion
	}

	class CompletionDataGenerator : ICompletionDataGenerator
	{
		public CompletionDataList CompletionDataList;

		Dictionary<string, DCompletionData> overloadCheckDict = new Dictionary<string, DCompletionData>();
		public void Add(INode Node)
		{
			if (Node == null || Node.Name == null)
				return;

			DCompletionData dc;
			if (overloadCheckDict.TryGetValue(Node.Name, out dc))
			{
				dc.AddOverload(Node);
			}
			else
			{
				CompletionDataList.Add(overloadCheckDict[Node.Name]=new DCompletionData(Node));
			}
		}

		public void Add(byte Token)
		{
			CompletionDataList.Add(new TokenCompletionData(Token));
		}

		public void AddPropertyAttribute(string AttributeText)
		{
			CompletionDataList.Add(new CompletionData("@"+AttributeText,new IconId("md-keyword"), DTokens.GetDescription("@"+AttributeText)));
		}

		public void AddTextItem(string Text, string Description)
		{
			CompletionDataList.Add(Text, IconId.Null, Description);
		}
		
		public void AddModule(DModule module, string nameOverride)
		{
			CompletionDataList.Add(new NamespaceCompletionData(module));
		}
		
		public void AddPackage(string packageName)
		{
			CompletionDataList.Add(new PackageCompletionData { Path = packageName, Name = ModuleNameHelper.ExtractModuleName(packageName) });
		}
	}

	public class TokenCompletionData : CompletionData
	{
		public byte Token { get; set; }

		public TokenCompletionData(byte Token)
		{
			this.Token = Token;
			CompletionText = DisplayText = DTokens.GetTokenString(Token);
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			var tti = new TooltipInformation();

			tti.SignatureMarkup = DisplayText;

			tti.SummaryMarkup = DTokens.GetDescription(Token);

			return tti;
		}

		public override IconId Icon
		{
			get
			{
				return new IconId("md-keyword");
			}
			set { }
		}
	}

	public class PackageCompletionData : CompletionData
	{
		public string Path;
		public string Name;

		public PackageCompletionData()
		{
			DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup;
		}

		public override string CompletionText
		{
			get
			{
				return Name;
			}
			set{}
		}

		public override string DisplayDescription
		{
			get
			{
				return "<i>(Package)</i>";
			}
			set{}
		}

		public override string DisplayText
		{
			get
			{
				return Name;
			}
			set{}
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			var tti = new TooltipInformation();

			tti.SignatureMarkup = "<i>(Package)</i> " + Path;

			return tti;
		}

		public override Core.IconId Icon
		{
			get
			{
				return new IconId("md-name-space");
			}
		}
	}

	public class NamespaceCompletionData : CompletionData
	{
		string modName;
		public readonly DModule Module;

		public NamespaceCompletionData(DModule mod)
		{
			this.Module = mod;
			DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup;
			modName = ModuleNameHelper.ExtractModuleName(mod.ModuleName);
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			var tti = new TooltipInformation();

			tti.SignatureMarkup = "<i>(Module)</i> " + Module.ModuleName;
			tti.SummaryMarkup = AmbienceService.EscapeText(Module.Description);
			tti.FooterMarkup = Module.FileName;

			return tti;
		}

		public override Core.IconId Icon
		{
			get
			{
				return new IconId("md-name-space");
			}
		}

		

		public override string DisplayText
		{
			get { return modName; }
		}

		public override string CompletionText
		{
			get { return modName; }
		}

		public override string DisplayDescription
		{
			get
			{
				return Module.FileName;
			}
			set
			{}
		}
	}

	public class DCompletionData : CompletionData, IComparable<ICompletionData>
	{
		public DCompletionData(INode n)
		{
			Node = n;

			Icon = GetNodeIcon(n as DNode);
			this.DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup;
		}

		public static Core.IconId GetNodeIcon(DNode n)
		{
			try
			{
				if (n == null)
					return null;

				if (n is DClassLike)
				{
					switch ((n as DClassLike).ClassType)
					{
						case DTokens.Template:
							return iconIdWithProtectionAttr(n,"template");

						case DTokens.Class:
							return classIconIdWithProtectionAttr(n);

						case DTokens.Union:
							return iconIdWithProtectionAttr(n, "union");

						case DTokens.Struct:
							return iconIdWithProtectionAttr(n, "struct");

						case DTokens.Interface:
							return iconIdWithProtectionAttr(n, "interface");
					}
				}
				else if (n is DEnum)
				{
					// TODO: does declaring an enum private/protected/package actually have a meaning?
					return iconIdWithProtectionAttr(n, "enum");
				}
				else if (n is DEnumValue)
				{
					return DCodeCompletionSupport.GetNodeImage("literal");
				}
				else if (n is DMethod)
				{
					//TODO: Getter or setter functions should be declared as a >single< property only
					if (n.ContainsPropertyAttribute())
					{
						return iconIdWithProtectionAttr(n, "property");
					}

					return methodIconIdWithProtectionAttr(n);
				}
				else if (n is DVariable)
				{
                    if (((DVariable)n).IsAlias)
                    {
						// TODO: does declaring an alias private/protected/package actually have a meaning?
						return iconIdWithProtectionAttr(n, "alias");
                    }

					if (n.ContainsPropertyAttribute())
					{
						return iconIdWithProtectionAttr(n, "property");
					}

					if (n.Type is DelegateDeclaration)
					{
						return iconIdWithProtectionAttr(n, "delegate");
					}

					if (n.ContainsAttribute(DTokens.Const))
					{
						return iconIdWithProtectionAttr(n, "literal", true);
					}

					var realParent = n.Parent as DNode;

					if (realParent == null)
						return DCodeCompletionSupport.GetNodeImage("local");

                    if (realParent is DClassLike || n.Parent is DModule)
					{
						return iconIdWithProtectionAttr(n, "field", true);
					}

					if (realParent is DMethod)
					{
						// FIXME: first parameter of class constructors is always displayed as a local, not a parameter
						if ((realParent as DMethod).Parameters.Contains(n))
						{
							if (n.ContainsAttribute(DTokens.Ref))
							    return DCodeCompletionSupport.GetNodeImage("ref_parameter");
							else if (n.ContainsAttribute(DTokens.Lazy))
								return DCodeCompletionSupport.GetNodeImage("lazy_parameter");
							else if (n.ContainsAttribute(DTokens.Out))
							    return DCodeCompletionSupport.GetNodeImage("out_parameter");
							else
							    return DCodeCompletionSupport.GetNodeImage("parameter");
							// TODO: immutable, scope?
						}
						return DCodeCompletionSupport.GetNodeImage("local");
					}

					ITemplateParameter tpar;
					if (realParent.TryGetTemplateParameter(n.Name, out tpar))
						return DCodeCompletionSupport.GetNodeImage("parameter");
				}
			}
			catch (Exception ex) { LoggingService.LogError("Error while getting node icon", ex); }
			return null;
		}

		/// <summary>
		/// Returns node icon id looked up from the provided base string plus the protection
		/// attribute (and, optionally, the staticness) of node.
		/// </summary>
		private static Core.IconId iconIdWithProtectionAttr ( DNode n, string image,
        	bool allow_static = false )
		{
			string attr = "";

			if ( allow_static && n.ContainsAttribute(DTokens.Static))
			{
				attr += "static_";
			}

			if (n.ContainsAttribute(DTokens.Package))
				return DCodeCompletionSupport.GetNodeImage(attr + image + "_internal");
			else if (n.ContainsAttribute(DTokens.Protected))
				return DCodeCompletionSupport.GetNodeImage(attr + image + "_protected");
			else if (n.ContainsAttribute(DTokens.Private))
				return DCodeCompletionSupport.GetNodeImage(attr + image + "_private");
			return DCodeCompletionSupport.GetNodeImage(attr + image);
		}

		/// <summary>
		/// Returns node icon id for a class, including the protection attribute, staticness and
		/// abstractness.
		/// </summary>
		private static Core.IconId classIconIdWithProtectionAttr ( DNode n )
		{
			// Only nested class may be static
			bool static_allowed = n.IsClassMember;

			string attr = "";
			if ( n.ContainsAttribute(DTokens.Abstract) )
			{
				attr += "abstract_";
			}

			return iconIdWithProtectionAttr(n, attr + "class", static_allowed);
		}

		/// <summary>
		/// Returns node icon id for a method, including the protection attribute, staticness
		/// and abstractness.
		/// </summary>
		private static Core.IconId methodIconIdWithProtectionAttr ( DNode n )
		{
			string attr = "";

			// Only class (not struct) methods, and neither static nor private methods may
			// be abstract/override
			if ( n.IsClassMember &&
			    !n.ContainsAttribute(DTokens.Static) && !n.ContainsAttribute(DTokens.Private) )
			{
				if ( n.ContainsAttribute(DTokens.Abstract) )
				{
					attr += "abstract_";
				}
				else if ( n.ContainsAttribute(DTokens.Override) )
				{
					attr += "override_";
				}
			}

			return iconIdWithProtectionAttr(n, attr + "method", true);
		}

		/// <summary>
		/// Returns node string without attributes and without node path
		/// </summary>
		public string PureNodeString
		{
			get
			{
				if (Node is DVariable)
					return (Node as DVariable).ToString(true,false,false);
				if (Node is DNode)
					return (Node as DNode).ToString(true, false);
				return Node.ToString();
			}
		}

		public INode Node { get; protected set; }

		public override string CompletionText
		{
			get { return Node.Name; }
			set { }
		}

		public override string DisplayText
		{
			get { return CompletionText; }
			set { }
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			var tti = new TooltipInformation();

			var n = Node;
			var dn = n as DNode;

			var sb = new StringBuilder();
			sb.Append("<i>(");

			if (dn is DClassLike)
			{
				switch ((dn as DClassLike).ClassType)
				{
					case DTokens.Class:
						sb.Append("Class");
						break;
					case DTokens.Template:
						if (dn.ContainsAttribute(DTokens.Mixin))
							sb.Append("Mixin ");
						sb.Append("Template");
						break;
					case DTokens.Struct:
						sb.Append("Struct");
						break;
					case DTokens.Union:
						sb.Append("Union");
						break;
				}
			}
			else if (dn is DEnum)
			{
				sb.Append("Enum");
			}
			else if (dn is DEnumValue)
			{
				sb.Append("Enum Value");
			}
			else if (dn is DVariable)
			{
				if (dn.Parent is DMethod)
				{
					var dm = dn.Parent as DMethod;
					if (dm.Parameters.Contains(dn))
						sb.Append("Parameters");
					else
						sb.Append("Local");
				}
				else if (dn.Parent is DClassLike)
					sb.Append("Field");
				else
					sb.Append("Variable");
			}
			else if (dn is DMethod)
			{
				sb.Append("Method");
			}
			else if (dn is TemplateParameterNode)
			{
				sb.Append("Template Parameter");
			}

			sb.Append(")</i> ");

			tti.SignatureMarkup = sb.Append(AmbienceService.EscapeText(PureNodeString)).ToString();

			
			if(!string.IsNullOrWhiteSpace(n.Description))
				tti.SummaryMarkup = AmbienceService.EscapeText(n.Description);

			return tti;
		}

		public override string DisplayDescription
		{
			get
			{
				var s = PureNodeString;
				if (s.Length > 40)
					return s.Substring(0, 40) + "...";
				return s;
			}
			set
			{}
		}

		public int CompareTo(ICompletionData other)
		{
			return Node.Name != null ? Node.Name.CompareTo(other.DisplayText) : -1;
		}

		public void AddOverload(INode n)
		{
			AddOverload(new DCompletionData(n));
		}

		public override void AddOverload(ICompletionData n)
		{
			if (Overloads == null)
			{
				Overloads = new List<ICompletionData>();
				Overloads.Add(this);
			}

			Overloads.Add(n);
		}

		List<ICompletionData> Overloads = null;

		public override bool HasOverloads
		{
			get
			{
				return Overloads!=null && Overloads.Count>1;
			}
		}

		public override IEnumerable<ICompletionData> OverloadedData
		{
			get
			{
				return Overloads;
			}
		}
	}
}
