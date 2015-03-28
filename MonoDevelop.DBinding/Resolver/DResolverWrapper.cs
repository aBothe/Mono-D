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
using MonoDevelop.Projects;

namespace MonoDevelop.D.Resolver
{
	public static class DResolverWrapper
	{
		#region EditorData creation
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
			var codeCache = CreateParseCacheView(EditorDocument);

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
				var versions = new List<string>();
				var debugConstants = new List<string> ();

				var cfg = EditorDocument.Project.GetConfiguration(IdeApp.Workspace.ActiveConfiguration);

				if (cfg is DProjectConfiguration)
				{
					var dcfg = cfg as DProjectConfiguration;
					ed.IsDebug = dcfg.DebugMode;
					ed.DebugLevel = dcfg.DebugLevel;
					double d;
					ulong v;
					if (Double.TryParse(EditorDocument.Project.Version, out d))
						ed.VersionNumber = (ulong)d;
					else if (UInt64.TryParse(EditorDocument.Project.Version, out v))
						ed.VersionNumber = v;
				}
				else if (cfg is DubProjectConfiguration)
				{
					versions.AddRange(VersionIdEvaluation.GetOSAndCPUVersions());
					
					var dcfg = cfg as DubProjectConfiguration;
					ed.IsDebug = dcfg.DebugMode;
				}

				foreach (var prj in GetProjectDependencyHierarchyToCurrentStartupProject(EditorDocument.Project, cfg.Selector))
					if(prj is AbstractDProject)
						ExtractVersionDebugConstantsFromProject (prj as AbstractDProject, versions, debugConstants);
				
				ed.GlobalDebugIds = debugConstants.ToArray ();
				ed.GlobalVersionIds = versions.ToArray ();
			}

			if (ed.GlobalVersionIds == null || ed.GlobalVersionIds.Length == 0)
			{
				ed.GlobalVersionIds = VersionIdEvaluation.GetOSAndCPUVersions();
			}

			return ed;
		}

		static List<SolutionItem> GetProjectDependencyHierarchyToCurrentStartupProject(SolutionItem prj, ConfigurationSelector cfg)
		{
			var currentlySearchedProjects = new List<SolutionItem> ();
			var nextProjectsToSearchIn = new List<SolutionItem> ();
			var topologicalParents = new Dictionary<SolutionItem, SolutionItem> ();

			var topologicalChain = new List<SolutionItem> ();
			topologicalChain.Add (prj);

			if (prj.ParentSolution.SingleStartup)
				currentlySearchedProjects.Add (prj.ParentSolution.StartupItem);
			else
				currentlySearchedProjects.AddRange (prj.ParentSolution.MultiStartupItems);

			while (currentlySearchedProjects.Count != 0) {

				foreach (var currentlySearchedProject in currentlySearchedProjects) {
					if (currentlySearchedProject == prj) {
						
						// Build topological chain by walking from leaf to root dependency.
						while (topologicalParents.TryGetValue (prj, out prj))
							topologicalChain.Add (prj);

						return topologicalChain;
					}

					IEnumerable<SolutionItem> deps;
					if (currentlySearchedProject is AbstractDProject)
						deps = (currentlySearchedProject as AbstractDProject).GetReferencedDProjects (cfg);
					else
						deps = currentlySearchedProject.GetReferencedItems (cfg);

					foreach (var dep in deps) {
						if (topologicalParents.ContainsKey (dep)) // Discard any second or dependency relationship, also prevents cycles.
							continue;
						topologicalParents [dep] = currentlySearchedProject;
						nextProjectsToSearchIn.Add (dep);
					}
				}

				currentlySearchedProjects.Clear ();
				currentlySearchedProjects.AddRange (nextProjectsToSearchIn);
				nextProjectsToSearchIn.Clear ();
			}

			return topologicalChain;
		}

		static void ExtractVersionDebugConstantsFromProject(AbstractDProject prj, List<string> versions, List<string> debugConstants)
		{
			var cfg = prj.GetConfiguration(IdeApp.Workspace.ActiveConfiguration);

			if (cfg is DProjectConfiguration)
			{
				var dcfg = cfg as DProjectConfiguration;
				if (dcfg.CustomDebugIdentifiers != null)
					debugConstants.AddRange (dcfg.CustomDebugIdentifiers);
				if (dcfg.GlobalVersionIdentifiers != null)
					versions.AddRange (dcfg.GlobalVersionIdentifiers);
			}
			else if (cfg is DubProjectConfiguration)
			{
				var dcfg = cfg as DubProjectConfiguration;

				HandleDubSettingsConditionExtraction(versions, (dcfg.ParentItem as DubProject).CommonBuildSettings);
				HandleDubSettingsConditionExtraction(versions, dcfg.BuildSettings);
			}
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
		#endregion

		public static ParseCacheView CreateParseCacheView(Document Editor)
		{
			return CreateParseCacheView(Editor.HasProject ? Editor.Project as AbstractDProject : null);
		}

		public static ParseCacheView CreateParseCacheView(AbstractDProject Project = null)
		{
			if (Project != null)
				return new MonoDParseCacheView();
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

		public static AbstractType[] ResolveHoveredCodeLoosely(out IEditorData ed, out LooseResolution.NodeResolutionAttempt resolutionAttempt, out ISyntaxRegion sr, Document doc = null)
		{
			ed = CreateEditorData(doc);
			if (ed == null)
			{
				sr = null;
				resolutionAttempt = LooseResolution.NodeResolutionAttempt.Normal;
				return null;
			}

			//return DResolver.ResolveTypeLoosely(ed, out resolutionAttempt, ctxt);
			return AmbiguousType.TryDissolve(LooseResolution.ResolveTypeLoosely(ed, out resolutionAttempt, out sr)).ToArray();
		}
	}
}
