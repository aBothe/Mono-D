using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser;
using D_IDE.D.CodeCompletion;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;
using System.IO;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Dom.Statements;

namespace MonoDevelop.D.Completion
{
	public class DCodeCompletionSupport
	{
		public static DCodeCompletionSupport Instance = new DCodeCompletionSupport();

		public DCodeCompletionSupport()
		{
			InitImages();
		}

		public static bool IsIdentifierChar(char key)
		{
			return char.IsLetterOrDigit(key) || key == '_';
		}

		public void BuildCompletionData(Document EditorDocument,IAbstractSyntaxTree SyntaxTree,CodeCompletionContext ctx, CompletionDataList l, string EnteredText)
		{
			var caretOffset = ctx.TriggerOffset;
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset,ctx.TriggerLine);

			IStatement stmt = null;
			var curBlock = DResolver.SearchBlockAt(SyntaxTree,caretLocation,out stmt);

			if (curBlock == null)
				return;

			IEnumerable<INode> listedItems = null;
			var codeCache = EnumAvailableModules(EditorDocument);

			// Usually shows variable members
			if (EnteredText == ".")
			{
				ITypeDeclaration id = null;
				DToken tk = null;
				var accessedItems=DResolver.ResolveTypeDeclarations(SyntaxTree,
					EditorDocument.Editor.Text.Substring(0,caretOffset-1),
					caretOffset-2,
					caretLocation,
					false,
					codeCache,
					out id,
					true,
					out tk);

				bool isThisOrSuper=tk!=null && (tk.Kind==DTokens.This || tk.Kind==DTokens.Super);
				bool isThis = isThisOrSuper && tk.Kind == DTokens.This;

				var addedModuleNames = new List<string>();

				if (accessedItems == null) //TODO: Add after-space list creation when an unbound . (Dot) was entered which means to access the global scope
					return;

				/*
				 * So, after getting the accessed variable or class or namespace it's needed either 
				 * - to resolve its type and show all its public items
				 * - or to show all public|static members of a class
				 * - or to show all public members of a namespace
				 * 
				 * Note: When having entered a module name stub only (e.g. "std." or "core.") it's needed to show all packages that belong to that root namespace
				 */
				foreach (var n in accessedItems)
				{
					if (n is DVariable || n is DMethod)
					{
						var type = DCodeResolver.GetDNodeType(n);

						if (type == null)
							continue;

						var declarationNodes = DCodeResolver.ResolveTypeDeclarations(SyntaxTree, type, codeCache,true);

						foreach (var declNode in declarationNodes)
							if (declNode is IBlockNode)
							{
								var declClass = declNode as DClassLike;

								if(declClass!=null) // If declaration type is a class-like type, also scan through all base classes
									while (declClass != null)
									{
										foreach (var n2 in declClass)
										{
											var dn = n2 as DNode;
											if (dn != null ? (dn.IsPublic || dn.IsStatic) && (dn is DVariable || dn is DMethod) : true)
												l.Add(new DCompletionData(n2));
										}
										declClass = DCodeResolver.ResolveBaseClass(declClass, codeCache);
									}
								else // 
									foreach (var n2 in declNode as IBlockNode)
									{
										var dn = n2 as DNode;
										if (dn != null ? (dn.IsPublic || dn.IsStatic) && (dn is DVariable || dn is DMethod) : true)
											l.Add(new DCompletionData(n2));
									}
							}
					}
					else if (n is DClassLike) // Add public static members of the class and including all base classes
					{
						var curClass = n as DClassLike;
						while (curClass != null)
						{
							foreach (var i in curClass)
							{
								var dn = i as DNode;

								if (dn == null)
									l.Add(new DCompletionData(i));

								// If "this." and if watching the current inheritance level only , add all items
								// if "super." , add public items
								// if neither nor, add public static items
								if( (isThis&&n==curClass) ? true : 
										(isThisOrSuper ? dn.IsPublic : 
											(dn.IsStatic && dn.IsPublic)))
									l.Add(new DCompletionData(dn));
							}
							curClass = DCodeResolver.ResolveBaseClass(curClass, codeCache);
						}
					}
					else if (n is DEnum)
					{
						var de = n as DEnum;

						foreach (var i in de)
						{
							var dn = i as DEnumValue;
							if (dn != null)
								l.Add(new DCompletionData(i));
						}
					}
					else if (n is IAbstractSyntaxTree)
					{
						var idParts = (n as IAbstractSyntaxTree).ModuleName.Split('.');
						int skippableParts = 0;

						if (id is NormalDeclaration)
							skippableParts = 1;
						else if (id is IdentifierList)
							skippableParts = (id as IdentifierList).Parts.Count;

						if (skippableParts >= idParts.Length)
						{
							// Add public items of a module
							foreach (var i in n as IBlockNode)
							{
								var dn = i as DNode;
								if (dn != null)
								{
									if (dn.IsPublic && !dn.ContainsAttribute(DTokens.Package))
										l.Add(new DCompletionData(dn));
								}
							}
						}
						else if (!addedModuleNames.Contains(idParts[skippableParts])) // Add next part of the module name path only if it wasn't added before
						{
							addedModuleNames.Add(idParts[skippableParts]); // e.g.  std.c.  ... in this virtual package, there are several sub-packages that contain the .c-part
							l.Add(new NamespaceCompletionData(idParts[skippableParts],GetModulePath((n as IAbstractSyntaxTree).FileName,idParts.Length,skippableParts+1)));
						}
					}
				}
			}

			// Enum all nodes that can be accessed in the current scope
			else if(string.IsNullOrEmpty(EnteredText) || IsIdentifierChar(EnteredText[0]))
			{
				listedItems = DCodeResolver.EnumAllAvailableMembers(curBlock, codeCache);

				foreach (var kv in DTokens.Keywords)
					l.Add(new TokenCompletionData(kv.Key));

				// Add module name stubs of importable modules
				var nameStubs=new Dictionary<string,string>();
				foreach (var mod in codeCache)
				{
					if (string.IsNullOrEmpty(mod.ModuleName))
						continue;

					var parts = mod.ModuleName.Split('.');

					if (!nameStubs.ContainsKey(parts[0]))
						nameStubs.Add(parts[0], GetModulePath(mod.FileName, parts.Length, 1));
				}

				foreach (var kv in nameStubs)
					l.Add(new NamespaceCompletionData(kv.Key,kv.Value));
			}

			// Add all found items to the referenced list
			if(listedItems!=null)
				foreach (var i in listedItems)
				{
					// Skip on unit tests or static c(d)tors
					if (i is DMethod)
					{
						var dm = i as DMethod;

						if (dm.SpecialType == DMethod.MethodType.Unittest || ((dm.SpecialType == DMethod.MethodType.Destructor || dm.SpecialType == DMethod.MethodType.Constructor) && dm.IsStatic))
							continue;
						}
					l.Add(new DCompletionData(i));
				}
		}

		/// <summary>
		/// Returns C:\fx\a\b when PhysicalFileName was "C:\fx\a\b\c\Module.d" , ModuleName= "a.b.c.Module" and WantedDirectory= "a.b"
		/// 
		/// Used when formatting package names in BuildCompletionData();
		/// </summary>
		public static string GetModulePath(string PhysicalFileName, string ModuleName, string WantedDirectory)
		{
			return GetModulePath(PhysicalFileName,ModuleName.Split('.').Length,WantedDirectory.Split('.').Length);
		}

		public static string GetModulePath(string PhysicalFileName, int ModuleNamePartAmount, int WantedDirectoryNamePartAmount)
		{
			var ret = "";

			var physFileNameParts = PhysicalFileName.Split(Path.DirectorySeparatorChar);
			for (int i = 0; i < physFileNameParts.Length - ModuleNamePartAmount + WantedDirectoryNamePartAmount; i++)
				ret += physFileNameParts[i] + Path.DirectorySeparatorChar;

			return ret.TrimEnd(Path.DirectorySeparatorChar);
		}

		/*public void BuildToolTip(DEditorDocument EditorDocument, ToolTipRequestArgs ToolTipRequest)
		{
			int offset = EditorDocument.Editor.Document.GetOffset(ToolTipRequest.Line, ToolTipRequest.Column);

			if (!ToolTipRequest.InDocument||
					DCodeResolver.Commenting.IsInCommentAreaOrString(EditorDocument.Editor.Text,offset)) 
				return;

			try
			{
				var types = DCodeResolver.ResolveTypeDeclarations(
					EditorDocument.SyntaxTree, 
					EditorDocument.Editor.Text,
					offset, 
					new CodeLocation(ToolTipRequest.Column, ToolTipRequest.Line),
					true,
					EnumAvailableModules(EditorDocument) // std.cstream.din.getc(); <<-- It's resolvable but not imported explictily! So also scan the global cache!
					//DCodeResolver.ResolveImports(EditorDocument.SyntaxTree,EnumAvailableModules(EditorDocument))
					,true
					);

				string tt = "";

				//TODO: Build well-formatted tool tip string/ Do a better tool tip layout
				if (types != null)
					foreach (var n in types)
						tt += n.ToString() + "\r\n";

				tt = tt.Trim();
				if(!string.IsNullOrEmpty(tt))
					ToolTipRequest.ToolTipContent = tt;
			}catch{}
		}*/

		public static bool IsInsightWindowTrigger(char key)
		{
			return key == '(' || key==',';
		}

		#region Module enumeration helper
		public static IEnumerable<IAbstractSyntaxTree> EnumAvailableModules(Document Editor)
		{
			return EnumAvailableModules(Editor.HasProject ? Editor.Project as DProject : null);
		}

		public static IEnumerable<IAbstractSyntaxTree> EnumAvailableModules(DProject Project)
		{
			var ret =new List<IAbstractSyntaxTree>();
			
			if (Project != null)
			{
				// Add the project's parsed modules to the reachable-packages list
				ret.AddRange(Project.ParsedModules);
			}

			// Add all parsed global modules that belong to the project's compiler configuration
			foreach (var astColl in DLanguageBinding.GlobalParseCache)
				ret.AddRange(astColl);

			return ret;
		}
		#endregion

		#region Image helper
		Dictionary<string, Core.IconId> images = new Dictionary<string, IconId>();

		void InitImages()
		{
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

				images["property"] =new IconId("md-property");
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
				LoggingService.LogError("Error while filling icon array",ex);
			}
		}

		public Core.IconId GetNodeImage(string key)
		{
			if (images.ContainsKey(key))
				return images[key];
			return null;
		}
		#endregion
	}

	public class TokenCompletionData : CompletionData
	{
		public int Token { get; set; }

		public TokenCompletionData(int Token)
		{
			this.Token = Token;
			CompletionText=DisplayText = DTokens.GetTokenString(Token);
			Description = DTokens.GetDescription(Token);
		}

		public override IconId Icon
		{
			get
			{
				return new IconId("md-keyword");
			}
			set{}
		}
	}

	public class NamespaceCompletionData : CompletionData
	{
		public string ModuleName{get;set;}
		public IAbstractSyntaxTree AssociatedModule { get; set; }
		public string _desc;

		public NamespaceCompletionData(string ModuleName, IAbstractSyntaxTree AssocModule)
		{
			this.ModuleName=ModuleName;
			AssociatedModule = AssocModule;
		}

		public NamespaceCompletionData(string ModuleName, string Description)
		{
			this.ModuleName = ModuleName;
			_desc = Description;
		}

		public override Core.IconId Icon
		{
			get
			{
				return new IconId("md-name-space");
			}
			set{}
		}

		public override string  Description
		{
			set { }
			get { return !string.IsNullOrEmpty(_desc)?_desc: (AssociatedModule!=null?AssociatedModule.FileName:null); }
		}

		public override string DisplayText{
			get{	return CompletionText;	}
			set{}
		}

		public override string CompletionText{	
			get{	return ModuleName;	}
			set{}
		}
	}

	public class DCompletionData : CompletionData
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
								return DCodeCompletionSupport.Instance.GetNodeImage("class_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.Instance.GetNodeImage("class_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.Instance.GetNodeImage("class_private");
							return DCodeCompletionSupport.Instance.GetNodeImage("class");

						case DTokens.Union:
						case DTokens.Struct:
							if (n.ContainsAttribute(DTokens.Package))
								return DCodeCompletionSupport.Instance.GetNodeImage("struct_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.Instance.GetNodeImage("struct_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.Instance.GetNodeImage("struct_private");
							return DCodeCompletionSupport.Instance.GetNodeImage("struct");

						case DTokens.Interface:
							if (n.ContainsAttribute(DTokens.Package))
								return DCodeCompletionSupport.Instance.GetNodeImage("interface_internal");
							else if (n.ContainsAttribute(DTokens.Protected))
								return DCodeCompletionSupport.Instance.GetNodeImage("interface_protected");
							else if (n.ContainsAttribute(DTokens.Private))
								return DCodeCompletionSupport.Instance.GetNodeImage("interface_private");
							return DCodeCompletionSupport.Instance.GetNodeImage("interface");
					}
				}
				else if (n is DEnum)
				{
					if (n.ContainsAttribute(DTokens.Package))
						return DCodeCompletionSupport.Instance.GetNodeImage("enum_internal");
					else if (n.ContainsAttribute(DTokens.Protected))
						return DCodeCompletionSupport.Instance.GetNodeImage("enum_protected");
					else if (n.ContainsAttribute(DTokens.Private))
						return DCodeCompletionSupport.Instance.GetNodeImage("enum_private");
					return DCodeCompletionSupport.Instance.GetNodeImage("enum");
				}
				else if (n is DMethod)
				{
					//TODO: Getter or setter functions should be declared as a >single< property only
					if (n.ContainsAttribute(DTokens.PropertyAttribute))
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.Instance.GetNodeImage("property_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.Instance.GetNodeImage("property_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.Instance.GetNodeImage("property_private");
						return DCodeCompletionSupport.Instance.GetNodeImage("property");
					}

					if (n.ContainsAttribute(DTokens.Package))
						return DCodeCompletionSupport.Instance.GetNodeImage("method_internal");
					else if (n.ContainsAttribute(DTokens.Protected))
						return DCodeCompletionSupport.Instance.GetNodeImage("method_protected");
					else if (n.ContainsAttribute(DTokens.Private))
						return DCodeCompletionSupport.Instance.GetNodeImage("method_private");
					return DCodeCompletionSupport.Instance.GetNodeImage("method");
				}
				else if (n is DEnumValue)
					return DCodeCompletionSupport.Instance.GetNodeImage("literal");
				else if (n is DVariable)
				{
					if (n.Type is DelegateDeclaration)
					{
						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.Instance.GetNodeImage("delegate_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.Instance.GetNodeImage("delegate_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.Instance.GetNodeImage("delegate_private");
						return DCodeCompletionSupport.Instance.GetNodeImage("delegate");
					}

					if (n.ContainsAttribute(DTokens.Const))
						return DCodeCompletionSupport.Instance.GetNodeImage("literal");

					var realParent = n.Parent as DBlockStatement;
					while (realParent is DStatementBlock)
						realParent = realParent.Parent as DBlockStatement;

					if (realParent == null)
						return null;

					if (realParent is DClassLike)
					{


						if (n.ContainsAttribute(DTokens.Package))
							return DCodeCompletionSupport.Instance.GetNodeImage("field_internal");
						else if (n.ContainsAttribute(DTokens.Protected))
							return DCodeCompletionSupport.Instance.GetNodeImage("field_protected");
						else if (n.ContainsAttribute(DTokens.Private))
							return DCodeCompletionSupport.Instance.GetNodeImage("field_private");
						return DCodeCompletionSupport.Instance.GetNodeImage("field");
					}

					if (realParent is DMethod)
					{
						if ((realParent as DMethod).Parameters.Contains(n))
							return DCodeCompletionSupport.Instance.GetNodeImage("parameter");
						return DCodeCompletionSupport.Instance.GetNodeImage("local");
					}

					if (realParent.TemplateParameters != null && realParent.TemplateParameters.Contains(n))
						return DCodeCompletionSupport.Instance.GetNodeImage("parameter");
				}
			}
			catch (Exception ex) { LoggingService.LogError("Error while getting node icon",ex); }
			return null;
		}

		public string NodeString { get {
			if (Node is DNode)
				return (Node as DNode).ToString();
			return Node.ToString(); } }

		/// <summary>
		/// Returns node string without attributes and without node path
		/// </summary>
		public string PureNodeString
		{
			get
			{
				if (Node is DNode)
					return (Node as DNode).ToString(false,false);
				return Node.ToString();
			}
		}

		public INode Node { get; protected set; }

		public override string CompletionText		{			
			get			{				return Node.Name;			}
			set	{}
		}

		public override string DisplayText		{		
			get	{	return CompletionText;	}
			set	{}
		}

		public override string Description
		{
			// If an empty description was given, do not show an empty decription tool tip
			get {
				try
				{
					return NodeString;
				}
				catch (Exception ex) { LoggingService.LogError("Error while building node string", ex); }
				return null;
			}
			//TODO: Make a more smarter tool tip
			set { }
		}
	}
}
