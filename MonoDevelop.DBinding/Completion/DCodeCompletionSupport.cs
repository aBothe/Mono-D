using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using D_Parser;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.D.Building;
using D_Parser.Misc;

namespace MonoDevelop.D.Completion
{
	public class DCodeCompletionSupport
	{
		public static void BuildCompletionData(Document EditorDocument, 
			IAbstractSyntaxTree SyntaxTree, 
			CodeCompletionContext ctx, 
			CompletionDataList l, 
			char triggerChar)
		{
			var deltaOffset = (char.IsLetter(triggerChar) || triggerChar == '_' || triggerChar == '@') ? 1 : 0;

			var caretOffset = ctx.TriggerOffset-deltaOffset;
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset-deltaOffset, ctx.TriggerLine);
			var codeCache = EnumAvailableModules(EditorDocument);

			var edData=new EditorData {
					CaretLocation=caretLocation,
					CaretOffset=caretOffset,
					ModuleCode=EditorDocument.Editor.Text,
					SyntaxTree=SyntaxTree as DModule,
					ParseCache=codeCache
				};

			AbstractCompletionProvider.BuildCompletionData(
				new CompletionDataGenerator { CompletionDataList = l },
				edData, 
				triggerChar=='\0'?null:triggerChar.ToString());
		}

		#region Module enumeration helper
		public static ParseCacheList EnumAvailableModules(Document Editor)
		{
			return EnumAvailableModules(Editor.HasProject ? Editor.Project as DProject : null);
		}

		public static ParseCacheList EnumAvailableModules(DProject Project=null)
		{
			if (Project != null)
				return ParseCacheList.Create(Project.LocalFileCache, Project.LocalIncludeCache, Project.Compiler.ParseCache);
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
				images["class"] = new IconId("md-class");
				images["class_internal"] = new IconId("md-internal-class");
				images["class_private"] = new IconId("md-private-class");
				images["class_protected"] = new IconId("md-protected-class");

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

				images["method"] = new IconId("md-method");
				images["method_internal"] = new IconId("md-internal-method");
				images["method_private"] = new IconId("md-private-method");
				images["method_protected"] = new IconId("md-protected-method");

				images["parameter"] = new IconId("md-field");
				images["local"] = new IconId("md-field");

				images["field"] = new IconId("md-field");
				images["field_internal"] = new IconId("md-internal-field");
				images["field_private"] = new IconId("md-private-field");
				images["field_protected"] = new IconId("md-protected-field");

				images["property"] = new IconId("md-property");
				images["property_internal"] = new IconId("md-internal-property");
				images["property_private"] = new IconId("m-privated-property");
				images["property_protected"] = new IconId("md-protected-property");

				images["delegate"] = new IconId("md-delegate");
				images["delegate_internal"] = new IconId("md-internal-delegate");
				images["delegate_private"] = new IconId("md-private-delegate");
				images["delegate_protected"] = new IconId("md-protected-delegate");

				images["literal"] = new IconId("md-literal");
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

		public void Add(string ModuleName, IAbstractSyntaxTree Module = null, string PathOverride = null)
		{
			CompletionDataList.Add(new NamespaceCompletionData(ModuleName, Module) { ExplicitModulePath = PathOverride });
		}

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

		public void Add(int Token)
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
	}

	public class TokenCompletionData : CompletionData
	{
		public int Token { get; set; }

		public TokenCompletionData(int Token)
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

	public class DCompletionData : CompletionData, IComparable<CompletionData>
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
						case DTokens.Class:
							if (n.ContainsAttribute(DTokens.Package))
								return DCodeCompletionSupport.GetNodeImage("class_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.GetNodeImage("class_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.GetNodeImage("class_private");
							return DCodeCompletionSupport.GetNodeImage("class");

						case DTokens.Union:
						case DTokens.Struct:
							if (n.ContainsAttribute(DTokens.Package))
								return DCodeCompletionSupport.GetNodeImage("struct_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.GetNodeImage("struct_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.GetNodeImage("struct_private");
							return DCodeCompletionSupport.GetNodeImage("struct");

						case DTokens.Interface:
							if (n.ContainsAttribute(DTokens.Package))
								return DCodeCompletionSupport.GetNodeImage("interface_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.GetNodeImage("interface_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.GetNodeImage("interface_private");
							return DCodeCompletionSupport.GetNodeImage("interface");
					}
				}
				else if (n is DEnum)
				{
					if (n.ContainsAttribute(DTokens.Package))
						return DCodeCompletionSupport.GetNodeImage("enum_internal");
					else if (n.ContainsAttribute(DTokens.Protected))
						return DCodeCompletionSupport.GetNodeImage("enum_protected");
					else if (n.ContainsAttribute(DTokens.Private))
						return DCodeCompletionSupport.GetNodeImage("enum_private");
					return DCodeCompletionSupport.GetNodeImage("enum");
				}
				else if (n is DMethod)
				{
					//TODO: Getter or setter functions should be declared as a >single< property only
					if (n.ContainsPropertyAttribute())
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.GetNodeImage("property_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.GetNodeImage("property_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.GetNodeImage("property_private");
						return DCodeCompletionSupport.GetNodeImage("property");
					}

					if (n.ContainsAttribute(DTokens.Package))
						return DCodeCompletionSupport.GetNodeImage("method_internal");
					else if (n.ContainsAttribute(DTokens.Protected))
						return DCodeCompletionSupport.GetNodeImage("method_protected");
					else if (n.ContainsAttribute(DTokens.Private))
						return DCodeCompletionSupport.GetNodeImage("method_private");
					return DCodeCompletionSupport.GetNodeImage("method");
				}
				else if (n is DEnumValue)
					return DCodeCompletionSupport.GetNodeImage("literal");
				else if (n is DVariable)
				{
					if (n.ContainsPropertyAttribute())
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.GetNodeImage("property_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.GetNodeImage("property_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.GetNodeImage("property_private");
						return DCodeCompletionSupport.GetNodeImage("property");
					}

					if (n.Type is DelegateDeclaration)
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.GetNodeImage("delegate_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.GetNodeImage("delegate_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.GetNodeImage("delegate_private");
						return DCodeCompletionSupport.GetNodeImage("delegate");
					}

					if (n.ContainsAttribute(DTokens.Const))
						return DCodeCompletionSupport.GetNodeImage("literal");

					var realParent = n.Parent as DNode;

					if (n.Parent is IAbstractSyntaxTree && !(n as DVariable).IsAlias)
						return DCodeCompletionSupport.GetNodeImage("field");

					if (realParent == null)
						return DCodeCompletionSupport.GetNodeImage("local");

					if (realParent is DClassLike)
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.GetNodeImage("field_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.GetNodeImage("field_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.GetNodeImage("field_private");
						return DCodeCompletionSupport.GetNodeImage("field");
					}

					if (realParent is DMethod)
					{
						if ((realParent as DMethod).Parameters.Contains(n))
							return DCodeCompletionSupport.GetNodeImage("parameter");
						return DCodeCompletionSupport.GetNodeImage("local");
					}

					if (realParent.ContainsTemplateParameter(n.Name))
						return DCodeCompletionSupport.GetNodeImage("parameter");
				}
			}
			catch (Exception ex) { LoggingService.LogError("Error while getting node icon", ex); }
			return null;
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
					return !string.IsNullOrWhiteSpace( Node.Description)?Node.Description:PureNodeString;
				}
				catch (Exception ex) { LoggingService.LogError("Error while building node string", ex); }
				return null;
			}
			//TODO: Make a more smarter tool tip
			set { }
		}

		public int CompareTo(CompletionData other)
		{
			return Node.Name != null ? Node.Name.CompareTo(other.DisplayText) : -1;
		}

		public void AddOverload(INode n)
		{
			AddOverload(new DCompletionData(n));
		}

		public void AddOverload(CompletionData n)
		{
			if (Overloads == null)
			{
				Overloads = new List<CompletionData>();
				Overloads.Add(this);
			}

			Overloads.Add(n);
		}

		List<CompletionData> Overloads = null;

		public override bool IsOverloaded
		{
			get
			{
				return Overloads!=null && Overloads.Count>1;
			}
		}

		public override IEnumerable<CompletionData> OverloadedData
		{
			get
			{
				return Overloads;
			}
		}
	}
}
