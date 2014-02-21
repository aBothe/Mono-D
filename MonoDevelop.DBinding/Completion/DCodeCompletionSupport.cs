using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using ICSharpCode.NRefactory.Completion;
using MonoDevelop.D.Parser;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.D.Resolver;

namespace MonoDevelop.D.Completion
{
	public static class DCodeCompletionSupport
	{
		public static void BuildCompletionData(Document EditorDocument, 
		                                        DModule SyntaxTree, 
		                                        CodeCompletionContext ctx, 
		                                        CompletionDataList l, 
		                                        char triggerChar)
		{
			var ed = DResolverWrapper.CreateEditorData(EditorDocument, SyntaxTree, ctx, triggerChar);
			CodeCompletion.GenerateCompletionData(ed, new CompletionDataGenerator(l, null), triggerChar);
		}

		public static ResolutionContext CreateContext(Document doc)
		{
			if (doc != null)
			{
				var ddoc = doc.ParsedDocument as ParsedDModule;
				if (ddoc != null)
				{
					var ast = ddoc.DDom;
					if (ast != null)
					{
						IStatement stmt;
						var caret = new CodeLocation(doc.Editor.Caret.Column, doc.Editor.Caret.Line);
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

						var ed = DResolverWrapper.CreateEditorData(doc);
						return new ResolutionContext(ed.ParseCache, new ConditionalCompilationFlags(ed), bn, stmt);
					}
				}
			}
			return new ResolutionContext(DResolverWrapper.CreateCacheList(),
				new ConditionalCompilationFlags(VersionIdEvaluation.GetOSAndCPUVersions(), 1, true), null);
		}

		public static ResolutionContext CreateCurrentContext()
		{
			Document doc = null;
			DispatchService.GuiSyncDispatch(() => doc = IdeApp.Workbench.ActiveDocument);
			return CreateContext(doc);
		}

		#region Image helper

		static readonly Dictionary<string, IconId> images = new Dictionary<string, IconId>();
		static bool wasInitialized = false;

		public static class DIcons
		{

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

			public static readonly IconId Local = new IconId("md-field"); // TODO: what's the difference between local & field?

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

		}
		static void InitImages()
		{
			if (wasInitialized)
				return;

			try
			{
				foreach (var f in typeof(DIcons).GetFields())
				{
					images.Add(f.Name.ToLower(), (IconId)f.GetValue(null));
				}
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error while filling icon array", ex);
			}

			wasInitialized = true;
		}

		public static IconId GetNodeImage(string key)
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
		public readonly CompletionDataList CompletionDataList;
		INode scopedBlock;

		public CompletionDataGenerator(CompletionDataList l, INode scopedBlock)
		{
			CompletionDataList = l;
			this.scopedBlock = scopedBlock;
		}

		~CompletionDataGenerator ()
		{
			DCompletionData.catCache.Clear();
		}

		Dictionary<int, DCompletionData> overloadCheckDict = new Dictionary<int, DCompletionData>();

		public void Add(INode Node)
		{
			if (Node == null || Node.NameHash == 0)
				return;

			DCompletionData dc;
			if (overloadCheckDict.TryGetValue(Node.NameHash, out dc))
			{
				dc.AddOverload(Node);
			}
			else
			{
				CompletionDataList.Add(overloadCheckDict[Node.NameHash] = new DCompletionData(Node, Node.Parent == scopedBlock));
			}
		}

		public void Add(byte Token)
		{
			CompletionDataList.Add(new TokenCompletionData(Token));
		}

		public void AddPropertyAttribute(string AttributeText)
		{
			CompletionDataList.Add(new CompletionData("@" + AttributeText, new IconId("md-keyword"), DTokens.GetDescription("@" + AttributeText)) { CompletionText = AttributeText });
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
			CompletionDataList.Add(new PackageCompletionData {
				Path = packageName,
				Name = ModuleNameHelper.ExtractModuleName(packageName)
			});
		}

		public void AddCodeGeneratingNodeItem(INode Node, string codeToGenerate)
		{
			if (Node == null || Node.NameHash == 0)
				return;

			DCompletionData dc;
			if (overloadCheckDict.TryGetValue(Node.NameHash, out dc))
			{
				dc.AddOverload(Node);
			}
			else
			{
				CompletionDataList.Add(overloadCheckDict[Node.NameHash] = new DCompletionData(Node, Node.Parent == scopedBlock) {
					CompletionText = codeToGenerate
				});
			}
		}
	}

	public class TokenCompletionData : CompletionData
	{
		public byte Token { get; set; }

		class TokenCompletionCategory : CompletionCategory
		{
			public static readonly TokenCompletionCategory Instance = new TokenCompletionCategory();

			TokenCompletionCategory()
			{
				base.DisplayText = GettextCatalog.GetString("Keywords");
				base.Icon = "md-keyword";
			}

			public override int CompareTo(CompletionCategory other)
			{
				return 1;
			}
		}

		public override CompletionCategory CompletionCategory
		{
			get
			{
				return TokenCompletionCategory.Instance;
			}
			set
			{
				
			}
		}

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

		class PackagesCompletionCategory : CompletionCategory
		{
			public static readonly PackagesCompletionCategory Instance = new PackagesCompletionCategory();

			PackagesCompletionCategory()
			{
				base.DisplayText = GettextCatalog.GetString("Packages");
				base.Icon = MonoDevelop.Ide.Gui.Stock.Package.Name;
			}

			public override int CompareTo(CompletionCategory other)
			{
				return 0;
			}
		}

		public override CompletionCategory CompletionCategory
		{
			get
			{
				return PackagesCompletionCategory.Instance;
			}
			set
			{
				
			}
		}

		public PackageCompletionData()
		{
			DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup | DisplayFlags.IsImportCompletion;
		}

		public override string CompletionText
		{
			get
			{
				return Name;
			}
			set{ }
		}

		public override string DisplayDescription
		{
			get
			{
				return "<i>(Package)</i>";
			}
			set{ }
		}

		public override string DisplayText
		{
			get
			{
				return Name;
			}
			set{ }
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			var tti = new TooltipInformation();

			tti.SignatureMarkup = "<i>(Package)</i> " + Path;

			return tti;
		}

		public override IconId Icon
		{
			get
			{
				return MonoDevelop.Ide.Gui.Stock.Package;
			}
		}
	}

	public class NamespaceCompletionData : CompletionData
	{
		string modName;
		public readonly DModule Module;

		class PackageCompletionCategory : CompletionCategory
		{
			public static readonly PackageCompletionCategory Instance = new PackageCompletionCategory();

			public PackageCompletionCategory()
			{
				base.DisplayText = GettextCatalog.GetString("Modules");
				base.Icon = MonoDevelop.Ide.Gui.Stock.NameSpace.Name;
			}

			public override int CompareTo(CompletionCategory other)
			{
				return 0;
			}
		}

		public override CompletionCategory CompletionCategory
		{
			get
			{
				return PackageCompletionCategory.Instance;
			}
			set
			{
				
			}
		}

		public NamespaceCompletionData(DModule mod)
		{
			this.Module = mod;
			DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup | DisplayFlags.IsImportCompletion;
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

		public override IconId Icon
		{
			get
			{
				return MonoDevelop.Ide.Gui.Stock.NameSpace;
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
			{ }
		}
	}

	public class DCompletionData : CompletionData, IComparable<ICompletionData>
	{
		internal class NodeCompletionCategory : CompletionCategory
		{
			DNode n;
			bool isLocalContainer;

			public NodeCompletionCategory(DNode n, bool isLocalContainer)
			{
				this.isLocalContainer = isLocalContainer;
				this.n = n;
				DisplayText = n.ToString(false, true);
				Icon = DCompletionData.GetNodeIcon(n).Name;
			}

			public override int CompareTo(CompletionCategory other)
			{
				var ncc = other as NodeCompletionCategory;
				

				if (ncc != null)
				{
					if (isLocalContainer)
						return -1;
					if (ncc.isLocalContainer)
						return 1;

					if (n == ncc.n.Parent)
						return 1;
					if (n.Parent == ncc.n)
						return -1;

					return 0;
				}
				
				return -1;
			}
		}

		bool parentContainsLocals;
		internal static Dictionary<INode, NodeCompletionCategory> catCache = new Dictionary<INode, NodeCompletionCategory>();

		public DCompletionData(INode n, bool parentContainsLocals)
		{
			Node = n as DNode;
			this.parentContainsLocals = parentContainsLocals;

			Icon = GetNodeIcon(n as DNode);
			this.DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup;
		}

		public override CompletionCategory CompletionCategory
		{
			get
			{
				NodeCompletionCategory cat;
				var par = Node.Parent as DNode;

				if (par == null)
					return null;

				if (par is DEnum && par.NameHash == 0)
				{
					par = par.Parent as DNode;
					if (par == null)
						return null;
				}

				if (!catCache.TryGetValue(par, out cat))
					catCache[par] = cat = new NodeCompletionCategory(par, parentContainsLocals);

				return cat;
			}
			set
			{
				
			}
		}

		public static IconId GetNodeIcon(DNode n)
		{
			try
			{
				if (n == null)
					return IconId.Null;

				if (n is DClassLike)
				{
					switch ((n as DClassLike).ClassType)
					{
						case DTokens.Template:
							return iconIdWithProtectionAttr(n, "template");

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
				else if (n is NamedTemplateMixinNode)
					return iconIdWithProtectionAttr(n, "template");
				else if (n is DModule)
					return new IconId("d-file");
				else if (n is DVariable)
				{
					var dv = n as DVariable;
					if (dv.IsAlias)
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

					if (n.ContainsAttribute(DTokens.Const) || (n.ContainsAttribute(DTokens.Enum) && dv.Initializer == null))
					{
						return iconIdWithProtectionAttr(n, "literal", true);
					}

					var realParent = n.Parent as DNode;

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

					TemplateParameter tpar;
					if (realParent != null && realParent.TryGetTemplateParameter(n.NameHash, out tpar))
						return DCodeCompletionSupport.GetNodeImage("parameter");

					return DCodeCompletionSupport.GetNodeImage("local");
				}
			}
			catch (Exception ex)
			{
				LoggingService.LogError("Error while getting node icon", ex);
			}
			return IconId.Null;
		}

		/// <summary>
		/// Returns node icon id looked up from the provided base string plus the protection
		/// attribute (and, optionally, the staticness) of node.
		/// </summary>
		private static IconId iconIdWithProtectionAttr(DNode n, string image,
		                                                bool allow_static = false)
		{
			string attr = "";

			if (allow_static && n.ContainsAttribute(DTokens.Static))
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
		private static IconId classIconIdWithProtectionAttr(DNode n)
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
		private static IconId methodIconIdWithProtectionAttr(DNode n)
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

		public readonly DNode Node;
		string customCompletionText;

		public override string CompletionText
		{
			get { return customCompletionText ?? Node.Name; }
			set { customCompletionText = value; }
		}

		public override void InsertCompletionText(CompletionListWindow window, ref KeyActions ka, Gdk.Key closeChar, char keyChar, Gdk.ModifierType modifier)
		{
			if (customCompletionText == null)
			{
				base.InsertCompletionText(window, ref ka, closeChar, keyChar, modifier);
				return;
			}

			Ide.Gui.Document guiDoc = null;
			var ed = window.CompletionWidget as SourceEditor.SourceEditorView;

			foreach (var gdoc in Ide.IdeApp.Workbench.Documents)
				if (gdoc.Editor.Document == ed.Document)
				{
					guiDoc = gdoc;
					break;
				}

			if (guiDoc == null)
				return;
			
			var f = new Formatting.DCodeFormatter();
			var insertionOffset = window.CodeCompletionContext.TriggerOffset;
			
			ed.Document.Insert(insertionOffset, customCompletionText, ICSharpCode.NRefactory.Editor.AnchorMovementType.AfterInsertion);

			guiDoc.UpdateParseDocument();

			f.OnTheFlyFormat(guiDoc, insertionOffset, insertionOffset + customCompletionText.Length);
		}

		public override string DisplayText
		{
			get { return Node.Name; }
			set { }
		}

		public override TooltipInformation CreateTooltipInformation(bool smartWrap)
		{
			return TooltipInfoGen.Create(Node, IdeApp.Workbench.ActiveDocument.Editor.ColorStyle);
		}

		public int CompareTo(ICompletionData other)
		{
			return Node.Name != null ? Node.Name.CompareTo(other.DisplayText) : -1;
		}

		public void AddOverload(INode n)
		{
			AddOverload(new DCompletionData(n, parentContainsLocals));
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
				return Overloads != null && Overloads.Count > 1;
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
