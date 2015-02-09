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

		public static ResolutionContext CreateContext(Document doc, bool pushFirstScope = true)
		{
			if (doc != null)
			{
				var ed = DResolverWrapper.CreateEditorData(doc);
				if(pushFirstScope)
					return new ResolutionContext(ed.ParseCache, new ConditionalCompilationFlags(ed), ed.SyntaxTree, ed.CaretLocation);
				return new ResolutionContext(ed.ParseCache, new ConditionalCompilationFlags(ed));
			}

			return new ResolutionContext(DResolverWrapper.CreateCacheList(),
				new ConditionalCompilationFlags(VersionIdEvaluation.GetOSAndCPUVersions(), 1, true));
		}

		public static ResolutionContext CreateCurrentContext()
		{
			Document doc = null;
			DispatchService.GuiSyncDispatch(() => doc = IdeApp.Workbench.ActiveDocument);
			return CreateContext(doc);
		}
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

		public void AddIconItem(string iconName, string text, string description)
		{
			CompletionDataList.Add (new CompletionData (text, new IconId (iconName), description));
		}

		public void AddTextItem(string Text, string Description)
		{
			CompletionDataList.Add(Text, IconId.Null, Description);
		}

		public void AddModule(DModule module, string nameOverride)
		{
			CompletionDataList.Add(new NamespaceCompletionData(module, nameOverride));
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

		public void NotifyTimeout()
		{
			CompletionDataList.Add("<Completion timeout>", new IconId("md-error"), "The completion request took a too long time and therefore was canceled.", string.Empty);
		}

		public void SetSuggestedItem(string i)
		{
			CompletionDataList.DefaultCompletionString = i;
		}
	}

	class TokenCompletionData : CompletionData
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

	class PackageCompletionData : CompletionData
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

	class NamespaceCompletionData : CompletionData
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

		public NamespaceCompletionData(DModule mod, string modName = null)
		{
			this.Module = mod;
			DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup | DisplayFlags.IsImportCompletion;
			this.modName = modName ?? ModuleNameHelper.ExtractModuleName(mod.ModuleName);
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
	}

	class DCompletionData : CompletionData, IComparable<ICompletionData>
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
				Icon = DIcons.GetNodeIcon(n).Name;
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
			this.DisplayFlags = ICSharpCode.NRefactory.Completion.DisplayFlags.DescriptionHasMarkup;
		}

		public override IconId Icon
		{
			get	{return DIcons.GetNodeIcon(Node);	}
			set {}
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
				if(Node != null && !string.IsNullOrEmpty(Node.Name))
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
