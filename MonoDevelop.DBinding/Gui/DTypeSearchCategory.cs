using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using MonoDevelop.Components.MainToolbar;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Projects;
using MonoDevelop.Ide;
using Xwt;

namespace MonoDevelop.D.Gui
{
	public class DTypeSearchCategory : SearchCategory
	{
		public DTypeSearchCategory () : base("DTypes")
		{
			
		}

		public override bool IsValidTag (string tag)
		{
			switch (tag) {
				case "type":
				case "member":
				case "d":
					return true;
				default:
					return false;
			}
		}

		public override Task<ISearchDataSource> GetResults (SearchPopupSearchPattern s, int resultsCount, CancellationToken token)
		{
			return Task.Factory.StartNew (delegate {
				
				var l = new List<INode>();

				foreach(var project in IdeApp.Workspace.GetAllProjects())
				{
					var dprj = project as AbstractDProject;
					if(dprj == null)
						continue;

					ModulePackage pack;
					foreach (var p in dprj.GetSourcePaths())
						if ((pack = GlobalParseCache.GetRootPackage (p)) != null)
							foreach (DModule m in pack)
								SearchResultsIn(m, s.Pattern, l, resultsCount);

					foreach (var p in dprj.IncludePaths)
						if ((pack = GlobalParseCache.GetRootPackage (p)) != null)
							foreach (DModule m in pack)
								SearchResultsIn(m, s.Pattern, l, resultsCount);
				}

				return (ISearchDataSource)new DSearchDataSource(l) { SearchPattern = s.Pattern };
			}, token);
		}

		void SearchResultsIn(IBlockNode block, string pattern, List<INode> results, int maxResults)
		{
			// Don't search in modules themselves!

			if (block.Children.Count == 0 || results.Count > maxResults)
				return;

			foreach (var n in block.Children) {
				if(!results.Contains(n) && n.Name.Contains(pattern))
					if(!results.Contains(n))
						results.Add(n);
				
				if(n is IBlockNode)
					SearchResultsIn(n as IBlockNode, pattern, results, maxResults);

				if (results.Count > maxResults)
					return;
			}
		}

		class DSearchDataSource : ISearchDataSource
		{
			public readonly List<INode> Symbols;
			public string SearchPattern;

			public DSearchDataSource(IEnumerable<INode> nodes)
			{
				Symbols = new List<INode>(nodes);
			}

			public Xwt.Drawing.Image GetIcon (int n)
			{
				return ImageService.GetIcon(DIcons.GetNodeIcon(Symbols[n] as DNode).Name, Gtk.IconSize.Menu);
			}

			public string GetMarkup (int item, bool isSelected)
			{
				var name = Symbols [item].Name;

				var i = name.IndexOf (SearchPattern);

				if (i < 0)
					return name;

				return name.Insert (i + SearchPattern.Length, "</b>").Insert(i,"<b>");
			}

			public string GetDescriptionMarkup (int item, bool isSelected)
			{
				var n = Symbols [item];

				var type = "Symbol";

				if (n is DMethod)
					type = "Method";
				else if (n is DVariable)
					type = "Member";
				else if (n is DClassLike)
					switch ((n as DClassLike).ClassType) {
						case DTokens.Class:
							type = "Class";
							break;
						case DTokens.Interface:
							type = "Interface";
							break;
					}
				else if (n is DEnum)
					type = "Enum";

				return MonoDevelop.Core.GettextCatalog.GetString (type) + " (" + DNode.GetNodePath(n, false) + ")";
			}

			public MonoDevelop.Ide.CodeCompletion.TooltipInformation GetTooltip (int item)
			{
				return TooltipInfoGen.Create(Symbols[item] as DNode, Mono.TextEditor.Highlighting.SyntaxModeService.GetColorStyle (MonoDevelop.Ide.IdeApp.Preferences.ColorScheme));
			}

			public double GetWeight (int item)
			{
				return 1;
			}

			public ICSharpCode.NRefactory.TypeSystem.DomRegion GetRegion (int item)
			{
				var n = Symbols [item];
				return new ICSharpCode.NRefactory.TypeSystem.DomRegion ((n.NodeRoot as DModule).FileName, n.Location.Line, n.Location.Column, n.EndLocation.Line, n.EndLocation.Column);
			}

			// Activate == Clicking on a search result. If false, the GetRegion-method will be called for a code lookup
			public bool CanActivate (int item)	{	return false;	}
			public void Activate (int item)	{}

			public int ItemCount {
				get {
					return Symbols.Count;
				}
			}
		}
	}
}

