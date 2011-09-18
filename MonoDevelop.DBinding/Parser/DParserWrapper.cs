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
using MonoDevelop.Core;

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

		public class ParsedDSource : ParsedDocument
		{
			public ParsedDSource(string fileName) : base(fileName) {}

			public IAbstractSyntaxTree DDom { get; protected set; }

			public static ParsedDSource CreateFromDFile(ProjectDom prjDom,string file,TextReader content)
			{
				var doc = new ParsedDSource(file);
				doc.Flags |= ParsedDocumentFlags.NonSerializable;

				var parser = D_Parser.DParser.Create(content);
				
				// Parse the code
				var ast=doc.DDom = parser.Parse();
				
				ast.FileName = file;

				// Update project owner information
				if (prjDom!=null && prjDom.Project is DProject)
				{
					var prj = prjDom.Project as DProject;
					var pf=prj.GetProjectFile(file);

					// Build appropriate module name
					var modName = pf.ProjectVirtualPath.ChangeExtension(null).ToString().Replace(Path.DirectorySeparatorChar,'.');

					ast.ModuleName = modName;

					if (pf != null)
						pf.ExtendedProperties[DProject.DParserPropertyKey] = ast;
				}

				var cu = new CompilationUnit(file);
				doc.CompilationUnit = cu;

				//TODO: Usings

				var globalScope = new DomType(cu, ClassType.Unknown, Modifiers.None, "(Global Scope)", new DomLocation(1, 1), string.Empty, new DomRegion(1, int.MaxValue - 2));

				cu.Add(globalScope);

				foreach (var n in ast)
				{
					var ch = ConvertDParserToDomNode(n, doc);

					if (ch is DomField || ch is DomMethod)
						globalScope.Add(ch as IMember);
					else
						cu.Add(ch as IType);
				}

				return doc;
			}
		}

		public ParsedDocument Parse(ProjectDom dom, string fileName, TextReader content)
		{
			return ParsedDSource.CreateFromDFile(dom,fileName, content);
		}

		public Projects.Dom.ParsedDocument Parse(ProjectDom dom, string fileName, string content)
		{
			return Parse(dom, fileName, new StringReader(content));
		}

		#region Converter methods

		public static MonoDevelop.Projects.Dom.INode ConvertDParserToDomNode(D_Parser.Core.INode n, ParsedDocument doc)
		{
			//TODO: DDoc comments!

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

				foreach(var subNode in dm)	domMethod.AddChild(ConvertDParserToDomNode(subNode,doc));

				return domMethod;
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
				
				foreach(var subNode in de)
					domType.Add(ConvertDParserToDomNode(subNode, doc) as IMember);
				return domType;
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
					foreach (var subNode in dc)
						domType.Add(ConvertDParserToDomNode(subNode, doc) as IMember);
				return domType;
			}
			else if (n is DVariable)
			{
				var dv = n as DVariable;
				return new DomField(n.Name, GetNodeModifiers(dv), FromCodeLocation(n.StartLocation), GetReturnType(n));
			}
			else if (n is DStatementBlock)
			{
				var ds = n as DStatementBlock;
				//TODO
			}
			return null;
		}

		public static string BuildTypeNamespace(D_Parser.Core.INode n)
		{
			var path = "";

			var curNode = n.Parent;
			while (curNode != null)
			{
				path = curNode.Name + "." + path;

				if (curNode == curNode.Parent)
					break;

				curNode = curNode.Parent;
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
			return td==null?null: new DomReturnType(td.ToString());
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
