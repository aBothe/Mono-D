using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects.Dom.Parser;
using D_Parser;
using MonoDevelop.Projects.Dom;
using System.IO;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using D_Parser.Core;

namespace MonoDevelop.D.Parser
{
	/// <summary>
	/// Parses D code.
	/// 
	/// Note: For natively parsing the code, the D_Parser engine will be used. To make it compatible to the MonoDevelop.Dom, its output will be wrapped afterwards!
	/// </summary>
	public class DParserWrapper : IParser
	{
		public bool CanParse(string fileName)
		{
			return fileName.EndsWith(".d", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".di", StringComparison.OrdinalIgnoreCase);
		}

		public IExpressionFinder CreateExpressionFinder(ProjectDom dom)
		{
			return null;
		}

		public IResolver CreateResolver(ProjectDom dom, object editor, string fileName)
		{
			return null;
		}

		public ParsedDocument Parse(ProjectDom dom, string fileName, TextReader content)
		{
			var doc = new ParsedDocument(fileName);
			doc.Flags |= ParsedDocumentFlags.NonSerializable;

			var p = (null == dom || null == dom.Project) ?
				IdeApp.Workspace.GetProjectContainingFile(fileName) :
				dom.Project;

			var parser = D_Parser.DParser.Create(content);
			var ast = parser.Parse();

			var cu = new CompilationUnit(fileName);
			doc.CompilationUnit = cu;

			//TODO: Usings

			foreach (var n in ast)
			{
				cu.AddChild(ConvertDParserToDomNode(cu,n, p, doc));
			}

			return doc;
		}

		public Projects.Dom.ParsedDocument Parse(ProjectDom dom, string fileName, string content)
		{
			return Parse(dom, fileName, new StringReader(content));
		}

		#region Converter methods

		public static MonoDevelop.Projects.Dom.INode ConvertDParserToDomNode(MonoDevelop.Projects.Dom.INode parentDomNode, D_Parser.Core.INode n, Project prj, ParsedDocument doc)
		{
			//TODO: DDoc comments!

			MonoDevelop.Projects.Dom.INode ret=null;

			if (n is DMethod)
			{
				var dm = n as DMethod;

				var domMethod=new DomMethod(
					n.Name,
					GetNodeModifiers(dm),
					dm.SpecialType==DMethod.MethodType.Constructor? MethodModifier.IsConstructor:MethodModifier.None,
					FromCodeLocation(n.StartLocation),
					GetBlockBodyRegion(dm),
					GetReturnType(n));

				foreach (var pn in dm.Parameters)
					domMethod.Add(new DomParameter(domMethod, pn.Name, GetReturnType(pn)));

				
				domMethod.AddTypeParameter(GetTypeParameters(dm));
				ret = domMethod;
			}
			else if (n is DEnum)
			{
				var de = n as DEnum;

				var domType = new DomType(
					doc.CompilationUnit,
					ClassType.Enum,
					GetNodeModifiers(de),
					n.Name,
					FromCodeLocation(n.StartLocation),
					BuildTypeNamespace(n), GetBlockBodyRegion(de));
				ret = domType;
			}
			else if (n is DClassLike)
			{
				var dc = n as DClassLike;

				ClassType ct = ClassType.Unknown;

				switch (dc.ClassType)
				{
					case DTokens.Template:
					case DTokens.Class:
						ct = ClassType.Class;
						break;
					case DTokens.Interface:
						ct = ClassType.Interface;
						break;
					case DTokens.Union:
					case DTokens.Struct:
						ct = ClassType.Struct;
						break;

				}

				var domType = new DomType(
					doc.CompilationUnit,
					ct,
					GetNodeModifiers(dc),
					n.Name,
					FromCodeLocation(n.StartLocation),
					BuildTypeNamespace(n),
					GetBlockBodyRegion(dc));
				
				domType.AddTypeParameter(GetTypeParameters(dc));
				ret = domType;
			}
			else if (n is DVariable)
			{
				var dv = n as DVariable;
				ret = new DomField(n.Name, GetNodeModifiers(dv), FromCodeLocation(n.StartLocation), GetReturnType(n));
			}
			else if (n is DStatementBlock)
			{
				var ds = n as DStatementBlock;
				//doc.ConditionalRegions.Add(new ConditionalRegion("") { Region=GetBlockBodyRegion(ds)});
			}

			if (n is IBlockNode && ret is MonoDevelop.Projects.Dom.AbstractNode) // Usually, ret should always be a DomType
			{
				var bn = n as IBlockNode;

					foreach(var subNode in bn)
						(ret as MonoDevelop.Projects.Dom.AbstractNode).AddChild(ConvertDParserToDomNode(ret,subNode,prj,doc));
			}

			return ret;
		}

		public static string BuildTypeNamespace(D_Parser.Core.INode n)
		{
			var path = "";

			var curNode = n;
			while (curNode!=n.Parent&& (curNode = n.Parent) != null)
			{
				path = curNode.Name + "." + path;
			}

			return path.TrimEnd('.');
		}

		/// <summary>
		/// Converts D template parameters to Dom type parameters
		/// </summary>
		public static IEnumerable<ITypeParameter> GetTypeParameters(DNode n)
		{
			if (n.TemplateParameters != null)
				foreach (var tpar in n.TemplateParameters)
					yield return new TypeParameter(tpar.Name); //TODO: Constraints'n'Stuff
		}

		public static IReturnType GetReturnType(D_Parser.Core.INode n)
		{
			return ToDomType(n.Type);
		}

		public static IReturnType ToDomType(ITypeDeclaration td)
		{
			return new DomReturnType(td.ToString());
		}

		public static IEnumerable<IAttribute> TransferAttributes(DNode n)
		{
			foreach (var attr in n.Attributes)
				yield return new DomAttribute() { Role=DomAttribute.Roles.Attribute, Name=DTokens.GetTokenString( attr.Token)};
		}

		public static DomRegion GetBlockBodyRegion(IBlockNode n)
		{
			return new DomRegion(n.BlockStartLocation.Line, n.BlockStartLocation.Column, n.EndLocation.Line, n.EndLocation.Column);
		}

		public static DomLocation FromCodeLocation(CodeLocation loc)
		{
			return new DomLocation(loc.Line, loc.Column);
		}

		public static Modifiers GetNodeModifiers(DNode dn)
		{
			Modifiers m = Modifiers.None;

			if (dn.ContainsAttribute(DTokens.Abstract)) m |= Modifiers.Abstract;
			if (dn.ContainsAttribute(DTokens.Const)) m |= Modifiers.Const;
			if (dn.ContainsAttribute(DTokens.Extern)) m |= Modifiers.Extern;
			if (dn.ContainsAttribute(DTokens.Package)) m |= Modifiers.Internal;
			if (dn.ContainsAttribute(DTokens.Override)) m |= Modifiers.Override;
			if (dn.ContainsAttribute(DTokens.Private)) m |= Modifiers.Private;
			if (dn.ContainsAttribute(DTokens.Protected)) m |= Modifiers.Protected;
			if (dn.ContainsAttribute(DTokens.Public)) m |= Modifiers.Public;
			if (dn.ContainsAttribute(DTokens.Final)) m |= Modifiers.Sealed;
			if (dn.ContainsAttribute(DTokens.Static)) m |= Modifiers.Static;
			if (dn.ContainsAttribute(DTokens.Volatile)) m |= Modifiers.Volatile;

			return m;
		}

		#endregion
	}
}
