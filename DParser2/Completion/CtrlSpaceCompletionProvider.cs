using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Completion
{
	public class CtrlSpaceCompletionProvider : AbstractCompletionProvider
	{
		public object parsedBlock;
		public IBlockNode curBlock;
		public ParserTrackerVariables trackVars;

		public CtrlSpaceCompletionProvider(ICompletionDataGenerator cdg) : base(cdg) { }

		public static bool CompletesEnteredText(string EnteredText)
		{
			return string.IsNullOrWhiteSpace(EnteredText) ||
				IsIdentifierChar(EnteredText[0]) ||
				EnteredText[0] == '(';
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, string EnteredText)
		{
			IEnumerable<INode> listedItems = null;
			var visibleMembers = MemberTypes.All;

			IStatement curStmt = null;
			if(curBlock==null)
				curBlock = DResolver.SearchBlockAt(Editor.SyntaxTree, Editor.CaretLocation, out curStmt);

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
				visibleMembers = MemberTypes.Imports | MemberTypes.Types | MemberTypes.Keywords;

				listedItems = ItemEnumeration.EnumAllAvailableMembers(curBlock, null, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
			}
			else
			{
				if (trackVars.LastParsedObject is INode &&
					string.IsNullOrEmpty((trackVars.LastParsedObject as INode).Name) &&
					trackVars.ExpectingIdentifier)
					return;

				if (trackVars.LastParsedObject is TokenExpression &&
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
					visibleMembers = MemberTypes.Imports;
				else if (trackVars.LastParsedObject is NewExpression && trackVars.IsParsingInitializer)
					visibleMembers = MemberTypes.Imports | MemberTypes.Types;
				else if (EnteredText == " ")
					return;
				// In class bodies, do not show variables
				else if (!(parsedBlock is BlockStatement || trackVars.IsParsingInitializer))
					visibleMembers = MemberTypes.Imports | MemberTypes.Types | MemberTypes.Keywords;

				// In a method, parse from the method's start until the actual caret position to get an updated insight
				if (visibleMembers.HasFlag(MemberTypes.Variables) &&
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

				if (visibleMembers != MemberTypes.Imports) // Do not pass the curStmt because we already inserted all updated locals a few lines before!
					listedItems = ItemEnumeration.EnumAllAvailableMembers(curBlock, curStmt, Editor.CaretLocation, Editor.ParseCache, visibleMembers);
			}

			// Add all found items to the referenced list
			if (listedItems != null)
				foreach (var i in listedItems)
				{
					if (CanItemBeShownGenerally(i))
						CompletionDataGenerator.Add(i);
				}

			//TODO: Split the keywords into such that are allowed within block statements and non-block statements
			// Insert typable keywords
			if (visibleMembers.HasFlag(MemberTypes.Keywords))
				foreach (var kv in DTokens.Keywords)
					CompletionDataGenerator.Add(kv.Key);

			else if (visibleMembers.HasFlag(MemberTypes.Types))
				foreach (var kv in DTokens.BasicTypes_Array)
					CompletionDataGenerator.Add(kv);

			#region Add module name stubs of importable modules
			if (visibleMembers.HasFlag(MemberTypes.Imports))
			{
				var nameStubs = new Dictionary<string, string>();
				var availModules = new List<IAbstractSyntaxTree>();
				foreach (var mod in Editor.ParseCache)
				{
					if (string.IsNullOrEmpty(mod.ModuleName))
						continue;

					var parts = mod.ModuleName.Split('.');

					if (!nameStubs.ContainsKey(parts[0]) && !availModules.Contains(mod))
					{
						if (parts[0] == mod.ModuleName)
							availModules.Add(mod);
						else
							nameStubs.Add(parts[0], GetModulePath(mod.FileName, parts.Length, 1));
					}
				}

				foreach (var kv in nameStubs)
					CompletionDataGenerator.Add(kv.Key, PathOverride: kv.Value);

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

				var psr = DParser.Create(new System.IO.StringReader(codeToParse));

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
					ret = psr.BlockStatement();
				}
				else if (CurrentScope is DModule)
					ret = psr.Root();
				else
				{
					psr.Step();
					if (ParseDecl)
					{
						var ret2 = psr.Declaration();

						if (ret2 != null && ret2.Length > 0)
							ret = ret2[0];
					}
					else
					{
						IBlockNode bn = null;
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

				return ret;
			}

			TrackerVariables = null;

			return null;
		}
	}
}
