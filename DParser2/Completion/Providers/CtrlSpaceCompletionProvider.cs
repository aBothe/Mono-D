using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using System.IO;

namespace D_Parser.Completion
{
	public class CtrlSpaceCompletionProvider : AbstractCompletionProvider
	{
		public object parsedBlock;
		public IBlockNode curBlock;
		public ParserTrackerVariables trackVars;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			IEnumerable<INode> listedItems = null;
			var visibleMembers = MemberFilter.All;

			IStatement curStmt = null;
			if(curBlock==null)
				curBlock = D_Parser.Resolver.TypeResolution.DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt);

			if (curBlock == null)
				return;

			// 1) Get current context the caret is at
			if(parsedBlock==null)
				parsedBlock = FindCurrentCaretContext(
					Editor.ModuleCode,
					curBlock,
					Editor.CaretOffset,
					Editor.CaretLocation,
					out trackVars);

			// 2) If in declaration and if node identifier is expected, do not show any data
			if (trackVars == null)
			{
				// --> Happens if no actual declaration syntax given --> Show types/imports/keywords anyway
				visibleMembers = MemberFilter.Imports | MemberFilter.Types | MemberFilter.Keywords;

				listedItems = ItemEnumeration.EnumAllAvailableMembers(curBlock, null, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
			}
			else
			{
				var n = trackVars.LastParsedObject as INode;
				var dv=n as DVariable;
				if (dv != null && dv.IsAlias && dv.Type == null && trackVars.ExpectingIdentifier)
				{ 
					// Show completion because no aliased type has been entered yet
				}
				else if (n != null && string.IsNullOrEmpty(n.Name) && trackVars.ExpectingIdentifier)
					return;

				else if (trackVars.LastParsedObject is TokenExpression &&
					DTokens.BasicTypes[(trackVars.LastParsedObject as TokenExpression).Token] &&
					!string.IsNullOrEmpty(EnteredText) &&
					IsIdentifierChar(EnteredText[0]))
					return;

				if (trackVars.LastParsedObject is DAttribute)
				{
					var attr = trackVars.LastParsedObject as DAttribute;

					if (attr.IsStorageClass && attr.Token != DTokens.Abstract)
						return;
				}

				if (trackVars.LastParsedObject is ImportStatement)
					visibleMembers = MemberFilter.Imports;
				else if ((trackVars.LastParsedObject is NewExpression && trackVars.IsParsingInitializer) ||
					trackVars.LastParsedObject is TemplateInstanceExpression && ((TemplateInstanceExpression)trackVars.LastParsedObject).Arguments==null)
					visibleMembers = MemberFilter.Imports | MemberFilter.Types;
				else if (EnteredText == " ")
					return;
				// In class bodies, do not show variables
				else if (!(parsedBlock is BlockStatement || trackVars.IsParsingInitializer))
					visibleMembers = MemberFilter.Imports | MemberFilter.Types | MemberFilter.Keywords;
				
				/*
				 * Handle module-scoped things:
				 * When typing a dot without anything following, trigger completion and show types, methods and vars that are located in the module & import scope
				 */
				else if (trackVars.LastParsedObject is TokenExpression && 
					((TokenExpression)trackVars.LastParsedObject).Token == DTokens.Dot)
				{
					visibleMembers = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables;
					curBlock = Editor.SyntaxTree;
					curStmt = null;
				}

				// In a method, parse from the method's start until the actual caret position to get an updated insight
				if (visibleMembers.HasFlag(MemberFilter.Variables) &&
					curBlock is DMethod &&
					parsedBlock is BlockStatement)
				{
					var bs = parsedBlock as BlockStatement;

					// Insert the updated locals insight.
					// Do not take the caret location anymore because of the limited parsing of our code.
					curStmt = bs.SearchStatementDeeply(bs.EndLocation);
				}
				else
					curStmt = null;

				

				if (visibleMembers != MemberFilter.Imports) // Do not pass the curStmt because we already inserted all updated locals a few lines before!
					listedItems = ItemEnumeration.EnumAllAvailableMembers(curBlock, curStmt, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
			}

			// Add all found items to the referenced list
			if (listedItems != null)
				foreach (var i in listedItems)
				{
					if (i is IAbstractSyntaxTree) // Modules and stuff will be added later on
						continue;

					if (CanItemBeShownGenerally(i))
						CompletionDataGenerator.Add(i);
				}

			//TODO: Split the keywords into such that are allowed within block statements and non-block statements
			// Insert typable keywords
			if (visibleMembers.HasFlag(MemberFilter.Keywords))
				foreach (var kv in DTokens.Keywords)
					CompletionDataGenerator.Add(kv.Key);

			else if (visibleMembers.HasFlag(MemberFilter.Types))
				foreach (var kv in DTokens.BasicTypes_Array)
					CompletionDataGenerator.Add(kv);

			#region Add module name stubs of importable modules
			if (visibleMembers.HasFlag(MemberFilter.Imports))
			{
				var nameStubs = new Dictionary<string, string>();
				var availModules = new List<IAbstractSyntaxTree>();

				foreach(var sstmt in Editor.SyntaxTree.StaticStatements)
					if (sstmt is ImportStatement)
					{
						var impStmt = (ImportStatement)sstmt;

						foreach(var imp in impStmt.Imports)
							if (string.IsNullOrEmpty(imp.ModuleAlias))
							{
								var id=imp.ModuleIdentifier.ToString();
								
								IAbstractSyntaxTree mod = null;
								foreach(var m in Editor.ParseCache.LookupModuleName(id))
								{
									mod = m;
									break;
								}

								if (mod == null || string.IsNullOrEmpty(mod.ModuleName))
									continue;

								var stub = imp.ModuleIdentifier.InnerMost.ToString();

								if (!nameStubs.ContainsKey(stub) && !availModules.Contains(mod))
								{
									if (stub == mod.ModuleName)
										availModules.Add(mod);
									else
										nameStubs.Add(stub, GetModulePath(mod.FileName, id.Split('.').Length, 1));
								}
							}
					}

				foreach (var kv in nameStubs)
					CompletionDataGenerator.Add(kv.Key, null, kv.Value);

				foreach (var mod in availModules)
					CompletionDataGenerator.Add(mod.ModuleName, mod);
			}
			#endregion
		}

		/// <returns>Either CurrentScope, a BlockStatement object that is associated with the parent method or a complete new DModule object</returns>
		public static object FindCurrentCaretContext(string code,
			IBlockNode CurrentScope,
			int caretOffset, CodeLocation caretLocation,
			out ParserTrackerVariables TrackerVariables)
		{
			bool ParseDecl = false;

			int blockStart = 0;
			var blockStartLocation = CurrentScope.BlockStartLocation;

			if (CurrentScope is DMethod)
			{
				var block = (CurrentScope as DMethod).GetSubBlockAt(caretLocation);

				if (block != null)
					blockStart = DocumentHelper.LocationToOffset(code, blockStartLocation = block.StartLocation);
				else
					return FindCurrentCaretContext(code, CurrentScope.Parent as IBlockNode, caretOffset, caretLocation, out TrackerVariables);
			}
			else if (CurrentScope != null)
			{
				if (CurrentScope.BlockStartLocation.IsEmpty || caretLocation < CurrentScope.BlockStartLocation && caretLocation > CurrentScope.StartLocation)
				{
					ParseDecl = true;
					blockStart = DocumentHelper.LocationToOffset(code, blockStartLocation = CurrentScope.StartLocation);
				}
				else
					blockStart = DocumentHelper.LocationToOffset(code, CurrentScope.BlockStartLocation);
			}

			if (blockStart >= 0 && caretOffset - blockStart > 0)
			{
				var codeToParse = code.Substring(blockStart, caretOffset - blockStart);

                var sr = new StringReader(codeToParse);
				var psr = DParser.Create(sr);

				/* Deadly important! For correct resolution behaviour, 
				 * it is required to set the parser virtually to the blockStart position, 
				 * so that everything using the returned object is always related to 
				 * the original code file, not our code extraction!
				 */
				psr.Lexer.SetInitialLocation(blockStartLocation);

				object ret = null;

				if (CurrentScope == null || CurrentScope is IAbstractSyntaxTree)
					ret = psr.Parse();
				else if (CurrentScope is DMethod)
				{
					psr.Step();
					ret = psr.BlockStatement(CurrentScope);
				}
				else if (CurrentScope is DModule)
					ret = psr.Root();
				else
				{
					psr.Step();
					if (ParseDecl)
					{
						var ret2 = psr.Declaration(CurrentScope);

						if (ret2 != null && ret2.Length > 0)
							ret = ret2[0];
					}
					else
					{
						DBlockNode bn = null;
						if (CurrentScope is DClassLike)
						{
							var t = new DClassLike((CurrentScope as DClassLike).ClassType);
							t.AssignFrom(CurrentScope);
							bn = t;
						}
						else if (CurrentScope is DEnum)
						{
							var t = new DEnum();
							t.AssignFrom(CurrentScope);
							bn = t;
						}

						bn.Clear();

						psr.ClassBody(bn);
						ret = bn;
					}
				}

				TrackerVariables = psr.TrackerVariables;
                sr.Close();

				return ret;
			}

			TrackerVariables = null;

			return null;
		}
	}
}
