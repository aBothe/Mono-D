using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using System.IO;
using D_Parser.Parser;
using D_Parser.Dom.Statements;

namespace MonoDevelop.D.Parser
{
	public class ParsedDModule : ParsedDocument
	{
		public ParsedDModule(string fileName) : base(fileName) { }

		IAbstractSyntaxTree _ddom;
		public IAbstractSyntaxTree DDom {
			get { 
				var sln=Ide.IdeApp.ProjectOperations.CurrentSelectedSolution;
				if(sln!=null)
				{
					var prj = sln.GetProjectContainingFile(this.FileName);

					if (prj != null && prj.IsFileInProject(FileName))
					{
						var pf = prj.GetProjectFile(FileName);

						return pf.ExtendedProperties[DProject.DParserPropertyKey] as IAbstractSyntaxTree;
					}
				}
				return _ddom;
			}
			set
			{
				var sln = Ide.IdeApp.ProjectOperations.CurrentSelectedSolution;
				if (sln != null)
				{
					var prj = sln.GetProjectContainingFile(this.FileName);

					if (prj != null && prj.IsFileInProject(FileName))
					{
						var pf = prj.GetProjectFile(FileName);

						pf.ExtendedProperties[DProject.DParserPropertyKey] = value;
					}
				}
				_ddom=value;
			}
		}

		public static ParsedDModule CreateFromDFile(ProjectDom prjDom, string file, TextReader content)
		{
			var doc = new ParsedDModule(file);
			doc.Flags |= ParsedDocumentFlags.NonSerializable;

			var parser = DParser.Create(content);

			// Also put attention on non-ddoc comments; These will be used to generate foldable comment regions then
			parser.Lexer.OnlyEnlistDDocComments = false;

			// Parse the code
			var ast  =  parser.Parse();

			ast.FileName = file;

			// Update project owner information
			if (prjDom != null && prjDom.Project is DProject)
			{
				var prj = prjDom.Project as DProject;
				var pf = prj.GetProjectFile(file);

				// Build appropriate module name
				var modName = pf.ProjectVirtualPath.ChangeExtension(null).ToString().Replace(Path.DirectorySeparatorChar, '.');

				ast.ModuleName = modName;

				if (pf != null)
					pf.ExtendedProperties[DProject.DParserPropertyKey] = ast;
			}
			else
				doc._ddom = ast;

			/*
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
			}*/
			
			foreach(ParserError parserError in parser.ParseErrors)
			{
				doc.Errors.Add(new Error(ErrorType.Error, parserError.Location.Line, parserError.Location.Column,parserError.Message));
			}

			foreach (var cm in parser.TrackerVariables.Comments)
			{
				var c = new Projects.Dom.Comment(cm.CommentText);

				c.CommentType = cm.CommentType.HasFlag(D_Parser.Parser.Comment.Type.Block) ? CommentType.MultiLine : CommentType.SingleLine;
				c.IsDocumentation = cm.CommentType.HasFlag(D_Parser.Parser.Comment.Type.Documentation);

				if (c.CommentType == CommentType.SingleLine)
				{
					if (c.IsDocumentation)
						c.OpenTag = "///";
					else
						c.OpenTag = "//";
				}
				else
				{
					if (c.IsDocumentation)
					{
						c.OpenTag = "/**";
						c.ClosingTag = "*/";
					}
					else {
						c.OpenTag = "/*";
						c.ClosingTag = "*/";
					}
				}

				c.Region = new DomRegion(cm.StartPosition.Line, cm.StartPosition.Column-2, cm.EndPosition.Line, cm.EndPosition.Column);

				doc.Comments.Add(c);
			}

			return doc;
		}

		#region Converter methods

		public static MonoDevelop.Projects.Dom.INode ConvertDParserToDomNode(D_Parser.Dom.INode n, ParsedDocument doc)
		{
			//TODO: DDoc comments!

			if (n is DMethod)
			{
				var dm = n as DMethod;

				var domMethod = new DomMethod(
					n.Name,
					GetNodeModifiers(dm),
					dm.SpecialType == DMethod.MethodType.Constructor ? MethodModifier.IsConstructor : MethodModifier.None,
					FromCodeLocation(n.StartLocation),
					GetBlockBodyRegion(dm),
					GetReturnType(n));

				foreach (var pn in dm.Parameters)
					domMethod.Add(new DomParameter(domMethod, pn.Name, GetReturnType(pn)));


				domMethod.AddTypeParameter(GetTypeParameters(dm));

				foreach (var subNode in dm) domMethod.AddChild(ConvertDParserToDomNode(subNode, doc));

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

				foreach (var subNode in de)
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
			return null;
		}

		public static string BuildTypeNamespace(D_Parser.Dom.INode n)
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

		public static IReturnType GetReturnType(D_Parser.Dom.INode n)
		{
			return ToDomType(n.Type);
		}

		public static IReturnType ToDomType(ITypeDeclaration td)
		{
			return td == null ? null : new DomReturnType(td.ToString());
		}

		public static IEnumerable<IAttribute> TransferAttributes(DNode n)
		{
			foreach (var attr in n.Attributes)
				yield return new DomAttribute() { Role = DomAttribute.Roles.Attribute, Name = DTokens.GetTokenString(attr.Token) };
		}

		public static DomRegion GetBlockBodyRegion(IBlockNode n)
		{
			return new DomRegion(n.BlockStartLocation.Line, n.BlockStartLocation.Column, n.EndLocation.Line, n.EndLocation.Column+1);
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


		public override IEnumerable<FoldingRegion> GenerateFolds()
		{
			var l = new List<FoldingRegion>();

			// Add primary node folds
			GenerateFoldsInternal(l, DDom);

			// Get member block regions
			var memberRegions = new List<DomRegion>();
			foreach (var i in l)
				if (i.Type == FoldType.Member)
					memberRegions.Add(i.Region);

			// Add multiline comment folds
			foreach (var c in Comments)
			{
				if (c.CommentType == CommentType.SingleLine)
					continue;

				bool IsMemberComment = false;

				foreach (var i in memberRegions)
					if (i.Contains(c.Region.Start))
					{
						IsMemberComment = true;
						break;
					}

				l.Add(new FoldingRegion(c.Region, IsMemberComment ? FoldType.CommentInsideMember : FoldType.Comment));
			}

			return l;
		}

		void GenerateFoldsInternal(List<FoldingRegion> l,IBlockNode block)
		{
			if (block == null)
				return;

			if (!(block is IAbstractSyntaxTree) && !block.StartLocation.IsEmpty && block.EndLocation > block.StartLocation)
			{
				if (block is DMethod)
				{
					var dm = block as DMethod;

					if (dm.In != null)
						GenerateFoldsInternal(l, dm.In);
					if (dm.Out != null)
						GenerateFoldsInternal(l, dm.Out);
					if (dm.Body != null)
						GenerateFoldsInternal(l, dm.Body);
				}
				else
					l.Add(new FoldingRegion(GetBlockBodyRegion(block),FoldType.Type));
			}

			if (block.Count > 0)
				foreach (var n in block)
					GenerateFoldsInternal(l,n as IBlockNode);
		}

		void GenerateFoldsInternal(List<FoldingRegion> l, StatementContainingStatement statement)
		{
			// Only let block statements (like { SomeStatement(); SomeOtherStatement++; }) be foldable
			if(statement is BlockStatement)
				l.Add(new FoldingRegion(
					new DomRegion(
						statement.StartLocation.Line,
						statement.StartLocation.Column,
						statement.EndLocation.Line,
						statement.EndLocation.Column+1),
					FoldType.Undefined));

			// Do a deep-scan
			foreach (var s in statement.SubStatements)
				if (s is StatementContainingStatement)
					GenerateFoldsInternal(l, s as StatementContainingStatement);
		}
	}

}
