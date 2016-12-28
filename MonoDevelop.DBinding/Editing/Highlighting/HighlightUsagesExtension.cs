using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.Ide.Editor.Highlighting;
using MonoDevelop.Ide.FindInFiles;

namespace MonoDevelop.D.Editing.Highlighting
{
	class UsageInfo {

	}

	class HighlightUsagesExtension : AbstractUsagesExtension<UsageInfo>
	{
		DSyntaxMode syntaxMode;

		protected override void Initialize ()
		{
			base.Initialize ();
			syntaxMode = new DSyntaxMode (Editor, DocumentContext);
			Editor.SemanticHighlighting = syntaxMode;
		}

		public override void Dispose ()
		{
			if (syntaxMode != null) {
				syntaxMode.Dispose ();
				syntaxMode = null;
			}

			base.Dispose ();
		}

		protected override Task<IEnumerable<MemberReference>> GetReferencesAsync (UsageInfo resolveResult, CancellationToken token)
		{
			var l = new List<MemberReference> ();

			return Task.FromResult<IEnumerable<MemberReference>>(l);
		}

		protected override Task<UsageInfo> ResolveAsync (CancellationToken token)
		{
			return Task.FromResult(new UsageInfo ());
		}
	}
}
