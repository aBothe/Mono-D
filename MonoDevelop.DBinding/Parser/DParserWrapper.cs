using System.IO;
using D_Parser.Dom;
using D_Parser.Parser;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.Ide.Tasks;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;
using System.Collections.Generic;
using D_Parser.Misc;
using MonoDevelop.D.Building;
using MonoDevelop.D.Projects;
using D_Parser.Resolver;

namespace MonoDevelop.D.Parser
{
	/// <summary>
	/// Parses D code.
	/// 
	/// Note: For natively parsing the code, the D_Parser engine will be used. 
	/// To make it compatible to the MonoDevelop.Dom, its output will be wrapped afterwards!
	/// </summary>
	public class DParserWrapper : TypeSystemParser
	{
		public static DParserWrapper Instance=new DParserWrapper();

		// Used for not having to parse a module two times, for 1) The fold region parser and 2) this parser wrapper
		internal static ParsedDocument LastParsedMod;

		public override ParsedDocument Parse(bool storeAst, string file, TextReader content, Project prj = null)
		{
			if (!storeAst)
				return null;

			ProjectFile pf = null;
			
			if (prj == null)
			{
				var sln = Ide.IdeApp.ProjectOperations.CurrentSelectedSolution;
				if (sln != null)
					foreach (var proj in sln.GetAllProjects())
						if (proj.IsFileInProject(file))
						{
							prj = proj;
							pf = proj.GetProjectFile(file);
							break;
						}
			}
			else if(prj.IsFileInProject(file))
			{
				pf = prj.GetProjectFile(file);
			}

			// HACK(?) The folds are parsed before the document gets loaded 
			// - so reuse the last parsed document to save time
			// -- What if multiple docs are opened?
			if (LastParsedMod is ParsedDModule && LastParsedMod.FileName == file)
			{
				var d = LastParsedMod as ParsedDModule;
				LastParsedMod = null;
				return d;
			}
			else
				LastParsedMod = null;

			var dprj = prj as AbstractDProject;

			// Remove obsolete ast from cache
			if(file != null)
				GlobalParseCache.RemoveModule (file);

			DModule ast;
			var doc = new ParsedDModule(file);

			var parser = DParser.Create(content);

			// Also put attention on non-ddoc comments; These will be used to generate foldable comment regions then
			parser.Lexer.OnlyEnlistDDocComments = false;

			// Parse the code
			try
			{
				ast = parser.Parse();
			}
			catch (TooManyErrorsException)
			{
				ast = parser.Document;
			}

			// Update project owner information / Build appropriate module name
			if(string.IsNullOrEmpty(ast.ModuleName))
			{
				if(pf == null)
					ast.ModuleName = file != null ? Path.GetFileNameWithoutExtension(file) : string.Empty;
				else
					ast.ModuleName = BuildModuleName(pf);
			}
			ast.FileName = file;

			// Assign new ast to the ParsedDDocument object
			doc.DDom = ast;

			// Add parser errors to the parser output
			foreach (var parserError in parser.ParseErrors)
				doc.ErrorList.Add(new Error(
					ErrorType.Error, 
					parserError.Message, 
					parserError.Location.Line, 
					parserError.Location.Column));

			#region Provide comment fold support by addin them to the IDE document object
			foreach (var cm in parser.TrackerVariables.Comments)
			{
				var c = new MonoDevelop.Ide.TypeSystem.Comment(cm.CommentText){
					CommentStartsLine = cm.CommentStartsLine,
					CommentType = (cm.CommentType & D_Parser.Parser.Comment.Type.Block) != 0 ? CommentType.Block : CommentType.SingleLine,
					IsDocumentation = cm.CommentType.HasFlag(D_Parser.Parser.Comment.Type.Documentation),
				};
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
					else
					{
						c.OpenTag = "/*";
						c.ClosingTag = "*/";
					}
				}

				c.Region = new DomRegion(cm.StartPosition.Line, cm.StartPosition.Column, cm.EndPosition.Line, cm.EndPosition.Column);

				doc.Comments.Add(c);

				// Enlist TODO/FIXME/HACK etc. stuff in the IDE's project task list
				for (int i = CommentTag.SpecialCommentTags.Count-1; i >= 0 ; i--)
					if (c.Text.StartsWith(CommentTag.SpecialCommentTags[i].Tag))
					{
						doc.Add(new Tag(CommentTag.SpecialCommentTags[i].Tag, c.Text, c.Region));
						break;
					}
			}
			#endregion

			#region Serialize to NRefactory Dom structure
			/*
			var cu = new CompilationUnit(file);
			doc.CompilationUnit = cu;
			
			var global = new DomType(cu, ClassType.Class,
				Modifiers.Public | Modifiers.Partial,
				"(global)",
				new DomLocation(),
				ast.ModuleName,
				new DomRegion());
			cu.Add(global);

			foreach (var n in ast)
			{
				var ch = ConvertDParserToDomNode(n, doc);
				
				if (ch is DomField || ch is DomMethod)
					global.Add(ch as IMember);
				else
					cu.Add(ch as IType);
			}
			*/
			#endregion


			if (prj != null)
			{
				// Workaround for tags not being displayed
				var ctnt = TypeSystemService.GetProjectContentWrapper(prj);
				if (ctnt != null)
				{
					var tags = ctnt.GetExtensionObject<ProjectCommentTags>();
					if (tags != null)
						tags.UpdateTags(prj, file, doc.TagComments);
				}
			}

			// Update UFCS
			ModulePackage pack;
			if((pack=GlobalParseCache.GetPackage(ast, false)) != null && (pack = pack.Root) != null)
			{
				// If the file is not associated with any project,
				// check if the file is located in an imported/included directory
				// and update the respective cache.
				// Note: ParseCache.Remove() also affects the Ufcs cache,
				// but when adding it again, the UfcsCache has to be updated manually
				ParseCacheView pcw;
				bool containsPack = false;
				if (prj != null) {
					pcw = dprj.ParseCache;
					containsPack = true;
				} else {
					// Find out which compiler environment fits most
					pcw = null;
					foreach (var cmp in DCompilerService.Instance.Compilers) {
						pcw = cmp.GenParseCacheView ();
						foreach (var r in pack as IEnumerable<ModulePackage>)
							if (r == pack) {
								containsPack = true;
								break;
							}
						if (containsPack)
							break;
					}
				}

				if(containsPack)
					(pack as RootPackage).UfcsCache.CacheModuleMethods(ast, new ResolutionContext(pcw, null, ast));
			}

			return doc;
		}

		public static string BuildModuleName(ProjectFile pf)
		{
			if(pf==null)
				return string.Empty;
			
			// When handling an external link, keep it rooted though it might occur in a project's subfolder
			if (pf.IsLink || pf.IsExternalToProject)
				return pf.FilePath.FileNameWithoutExtension;

			var dprj = pf.Project as AbstractDProject;

			var sourcePaths = dprj.GetSourcePaths(Ide.IdeApp.Workspace.ActiveConfiguration);
			foreach(var path in sourcePaths)
				if(pf.FilePath.IsChildPathOf(path))
					return pf.FilePath.ToRelative(path).ChangeExtension(null).ToString().Replace(Path.DirectorySeparatorChar, '.');
			return "";
		}

		/// <summary>
		/// Ensures the absolute paths when invoking parse procedures in the GlobalParseCache
		/// </summary>
		/// <returns>The absolute paths.</returns>
		/// <param name="rawPaths">Raw paths.</param>
		/// <param name="fallbackPath">Fallback path that will be used as absolute base in case of one of the raw paths isn't rooted.</param>
		/// <param name="solutionPath">Solution path that replaces any occurrency of '$solution' in a raw path.</param>
		public static List<string> EnsureAbsolutePaths(IEnumerable<string> rawPaths, string fallbackPath, string solutionPath)
		{
			var l = new List<string> ();

			foreach (var raw in rawPaths) {
				var path = raw.Replace("$solution", solutionPath);

				if (string.IsNullOrWhiteSpace(path))
					continue;

				try
				{
					if (fallbackPath != null && !Path.IsPathRooted(path))
						path = Path.Combine(fallbackPath, path);
				}
				catch (System.ArgumentException) { }

				l.Add(path);
			}

			return l;
		}

		#region Converter methods
		/*
		public static INode ConvertDParserToDomNode(D_Parser.Dom.INode n, ParsedDocument doc)
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
		*/
		public static string BuildTypeNamespace(D_Parser.Dom.INode n)
		{
			return (n.NodeRoot as DModule).ModuleName;
		}

		/// <summary>
		/// Converts D template parameters to Dom type parameters
		/// </summary>
		/*public static IEnumerable<ITypeParameter> GetTypeParameters(DNode n)
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
		}*/

		/*public static DomLocation FromCodeLocation(CodeLocation loc)
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
		}*/

		#endregion
		
		public ParsedDocument Parse(string fileName, Project prj)
		{
			return Parse(true, fileName, prj);
		}
		
		public override ParsedDocument Parse(bool storeAst, string fileName, Project project = null)
		{
			using (var sr = new StreamReader(fileName))
				return Parse(storeAst, fileName, sr, project);
		}
	}
}
