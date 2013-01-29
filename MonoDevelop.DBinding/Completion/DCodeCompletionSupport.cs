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
			IAbstractSyntaxTree SyntaxTree, 
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

			DCompletionData dc = null;
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
		
		public void AddModule(IAbstractSyntaxTree module, string nameOverride)
		{
			CompletionDataList.Add(new NamespaceCompletionData(nameOverride ?? module.ModuleName,module) { ExplicitModulePath = module.ModuleName });
		}
		
		public void AddPackage(string packageName)
		{
			CompletionDataList.Add(new NamespaceCompletionData(packageName,null));
		}
	}

	public class TokenCompletionData : CompletionData
	{
		public byte Token { get; set; }

		public TokenCompletionData(byte Token)
		{
			this.Token = Token;
			CompletionText = DisplayText = DTokens.GetTokenString(Token);
			Description = DTokens.GetDescription(Token);
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

	public class NamespaceCompletionData : CompletionData
	{
		public string ModuleName { get; private set; }
		/// <summary>
		/// Used for building the description.
		/// </summary>
		public string ExplicitModulePath { get; set; }
		public IAbstractSyntaxTree AssociatedModule { get; private set; }

		public NamespaceCompletionData(string ModuleName, IAbstractSyntaxTree AssocModule = null)
		{
			this.ModuleName = ModuleName;
			AssociatedModule = AssocModule;

			Init();
		}

		void Init()
		{
			bool IsPackage = AssociatedModule == null;

			var descString = (IsPackage ? "(Package)" : "(Module)");

			if (!string.IsNullOrWhiteSpace(ExplicitModulePath))
				descString += ExplicitModulePath;
			else if (AssociatedModule != null)
			{
				descString += " " + AssociatedModule.FileName;

				if (AssociatedModule.Description != null)
					descString += "\r\n" + AssociatedModule.Description;
			}

			Description = descString;
			//ToolTipContentHelper.CreateToolTipContent(IsPackage ? ModuleName : AssociatedModule.ModuleName, descString);
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
			get { return ModuleName; }
		}

		public override string CompletionText
		{
			get { return ModuleName; }
		}
	}

	public class DCompletionData : CompletionData, IComparable<ICompletionData>
	{
		public DCompletionData(INode n)
		{
			Node = n;

			Icon = GetNodeIcon(n as DNode);
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

                    if (realParent is DClassLike || n.Parent is IAbstractSyntaxTree)
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

					// FIXME: looks like this is supposed to handle template parameters, but
					// it doesn't seem to work
					if (realParent.ContainsTemplateParameter(n.Name))
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

		public string NodeString
		{
			get
			{
				if (Node is DNode)
					return (Node as DNode).ToString();
				return Node.ToString();
			}
		}

		/// <summary>
		/// Returns node string without attributes and without node path
		/// </summary>
		public string PureNodeString
		{
			get
			{
				if (Node is DNode)
					return (Node as DNode).ToString(false, false);
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

		public override string Description
		{
			// If an empty description was given, do not show an empty decription tool tip
			get
			{
				try
				{
					return (PureNodeString + Environment.NewLine + Node.Description).TrimEnd();
				}
				catch (Exception ex) { LoggingService.LogError("Error while building node string", ex); }
				return null;
			}
			//TODO: Make a more smarter tool tip
			set { }
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
