using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.Core;
using MonoDevelop.D.Building;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using System;

namespace MonoDevelop.D.Resolver
{
	public class DResolverWrapper
	{
		public static EditorData CreateEditorData(Document EditorDocument)
		{
			var dpd = EditorDocument.ParsedDocument as ParsedDModule;
			var ctx = new CodeCompletionContext();

			ctx.TriggerLine = EditorDocument.Editor.Caret.Line;
			ctx.TriggerLineOffset = EditorDocument.Editor.Caret.Column;
			ctx.TriggerOffset = EditorDocument.Editor.Caret.Offset;

			return CreateEditorData(EditorDocument, dpd.DDom as DModule, ctx);
		}

		public static EditorData CreateEditorData(Document EditorDocument, DModule Ast, CodeCompletionContext ctx, char triggerChar = '\0')
		{
			bool removeChar = char.IsLetter(triggerChar) || triggerChar == '_' || triggerChar == '@';

			var deltaOffset = 0;//removeChar ? 1 : 0;

			var caretOffset = ctx.TriggerOffset - (removeChar ? 1 : 0);
			var caretLocation = new CodeLocation(ctx.TriggerLineOffset - deltaOffset, ctx.TriggerLine);
			var codeCache = CreateCacheList(EditorDocument);

			var ed = new EditorData
			{
				CaretLocation = caretLocation,
				CaretOffset = caretOffset,
				ModuleCode = removeChar ? EditorDocument.Editor.Text.Remove(ctx.TriggerOffset - 1, 1) : EditorDocument.Editor.Text,
				SyntaxTree = Ast,
				ParseCache = codeCache
			};

			if (EditorDocument.HasProject)
			{
				var cfg = EditorDocument.Project.GetConfiguration(Ide.IdeApp.Workspace.ActiveConfiguration) as DProjectConfiguration;

				if (cfg != null)
				{
					ed.GlobalDebugIds = cfg.CustomDebugIdentifiers;
					ed.IsDebug = cfg.DebugMode;
					ed.DebugLevel = cfg.DebugLevel;
					ed.GlobalVersionIds = cfg.GlobalVersionIdentifiers;
					double d;
					int v;
					if (Double.TryParse(EditorDocument.Project.Version, out d))
						ed.VersionNumber = (int)d;
					else if (Int32.TryParse(EditorDocument.Project.Version, out v))
						ed.VersionNumber = v;
				}
			}

			if (ed.GlobalVersionIds == null)
			{
				ed.GlobalVersionIds = VersionIdEvaluation.GetOSAndCPUVersions();
			}

			return ed;
		}

		public static ParseCacheList CreateCacheList(Document Editor)
		{
			return CreateCacheList(Editor.HasProject ? Editor.Project as DProject : null);
		}

		public static ParseCacheList CreateCacheList(DProject Project = null)
		{
			if (Project != null)
			{
				var pcl = ParseCacheList.Create(Project.LocalFileCache, Project.LocalIncludeCache, Project.Compiler.ParseCache);

				// Automatically include dep projects' caches
				foreach (var dep in Project.DependingProjects)
					if (dep != null)
						pcl.Add(dep.LocalFileCache);

				return pcl;
			}
			else
				return ParseCacheList.Create(DCompilerService.Instance.GetDefaultCompiler().ParseCache);
		}



		public static AbstractType[] ResolveHoveredCode(
			out ResolutionContext ResolverContext, 
			MonoDevelop.Ide.Gui.Document doc=null)
		{
			var edData = CreateEditorData(doc);

			ResolverContext = ResolutionContext.Create(edData);
			
			// Resolve the hovered piece of code
			return DResolver.ResolveType(edData, ResolverContext, DResolver.AstReparseOptions.AlsoParseBeyondCaret);
		}
	}
}
