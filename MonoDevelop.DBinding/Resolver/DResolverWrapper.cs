using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using MonoDevelop.D.Building;
using MonoDevelop.D.Parser;
using MonoDevelop.D.Projects;
using MonoDevelop.D.Projects.Dub;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.D.Resolver
{
	public static class DResolverWrapper
	{
		public static EditorData CreateEditorData(Document EditorDocument)
		{
			if (EditorDocument == null)
				EditorDocument = IdeApp.Workbench.ActiveDocument;

			if (EditorDocument == null)
				return new EditorData();

			var ast = EditorDocument.GetDAst();
			if (ast == null)
				return null;

			var ctx = new CodeCompletionContext();

			ctx.TriggerLine = EditorDocument.Editor.Caret.Line;
			ctx.TriggerLineOffset = EditorDocument.Editor.Caret.Column;
			ctx.TriggerOffset = EditorDocument.Editor.Caret.Offset;

			return CreateEditorData (EditorDocument, ast, ctx);
		}

		public static EditorData CreateEditorData(Document EditorDocument, DModule Ast, CodeCompletionContext ctx, char triggerChar = '\0')
		{
			bool removeChar = char.IsLetter(triggerChar) || triggerChar == '_';

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
				var cfg = EditorDocument.Project.GetConfiguration(IdeApp.Workspace.ActiveConfiguration);

				if (cfg is DProjectConfiguration)
				{
					var dcfg = cfg as DProjectConfiguration;
					ed.GlobalDebugIds = dcfg.CustomDebugIdentifiers;
					ed.IsDebug = dcfg.DebugMode;
					ed.DebugLevel = dcfg.DebugLevel;
					ed.GlobalVersionIds = dcfg.GlobalVersionIdentifiers;
					double d;
					ulong v;
					if (Double.TryParse(EditorDocument.Project.Version, out d))
						ed.VersionNumber = (ulong)d;
					else if (UInt64.TryParse(EditorDocument.Project.Version, out v))
						ed.VersionNumber = v;
				}
				else if (cfg is DubProjectConfiguration)
				{
					var versions = new List<string>(VersionIdEvaluation.GetOSAndCPUVersions());
					
					var dcfg = cfg as DubProjectConfiguration;
					ed.IsDebug = dcfg.DebugMode;

					HandleDubSettingsConditionExtraction(versions, (dcfg.ParentItem as DubProject).CommonBuildSettings);
					HandleDubSettingsConditionExtraction(versions, dcfg.BuildSettings);
					
					ed.GlobalVersionIds = versions.ToArray();
				}
			}

			if (ed.GlobalVersionIds == null)
			{
				ed.GlobalVersionIds = VersionIdEvaluation.GetOSAndCPUVersions();
			}

			return ed;
		}

		private static void HandleDubSettingsConditionExtraction(List<string> versions, DubBuildSettings buildSets)
		{
			List<DubBuildSetting> sets;
			if(buildSets == null || !buildSets.TryGetValue(DubBuildSettings.VersionsProperty, out sets))
				return;

			foreach (var set in sets)
				if (set.Values != null)
					foreach (var ver in set.Values)
						if (!string.IsNullOrWhiteSpace(ver) && !versions.Contains(ver))
							versions.Add(ver);
		}

		public static ParseCacheView CreateCacheList(Document Editor)
		{
			return CreateCacheList(Editor.HasProject ? Editor.Project as AbstractDProject : null);
		}

		public static ParseCacheView CreateCacheList(AbstractDProject Project = null)
		{
			if (Project != null)
				return Project.ParseCache;
			else
				return DCompilerService.Instance.GetDefaultCompiler().GenParseCacheView();
		}

		public static AbstractType[] ResolveHoveredCode(
			out ResolutionContext ResolverContext, out IEditorData edData, 
			Document doc=null)
		{
			edData = CreateEditorData(doc);
			if (edData == null)
			{
				ResolverContext = null;
				return null;
			}
			ResolverContext = ResolutionContext.Create(edData, false);

			// Resolve the hovered piece of code
			return AmbiguousType.TryDissolve(DResolver.ResolveType(edData, ctxt:ResolverContext)).ToArray();
		}

		public static AbstractType[] ResolveHoveredCodeLoosely(out IEditorData ed, out LooseResolution.NodeResolutionAttempt resolutionAttempt, Document doc = null)
		{
			ed = CreateEditorData(doc);
			if (ed == null)
			{
				resolutionAttempt = LooseResolution.NodeResolutionAttempt.Normal;
				return null;
			}

			//return DResolver.ResolveTypeLoosely(ed, out resolutionAttempt, ctxt);
			ISyntaxRegion sr;
			return AmbiguousType.TryDissolve(LooseResolution.ResolveTypeLoosely(ed, out resolutionAttempt, out sr)).ToArray();
		}
	}
}
