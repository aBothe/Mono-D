using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Mono.TextEditor;
using Mono.TextEditor.Highlighting;
using System.IO;

namespace MonoDevelop.D.Highlighting
{
	public class DSyntaxMode : SyntaxMode
	{
		public DSyntaxMode()
		{
			var matches = new List<Mono.TextEditor.Highlighting.Match>();

			var provider = new ResourceXmlProvider(
				typeof(DSyntaxMode).Assembly, 
				typeof(DSyntaxMode).Assembly.GetManifestResourceNames().First(s => s.Contains("DSyntaxHighlightingMode")));
			using (XmlReader reader = provider.Open())
			{
				SyntaxMode baseMode = SyntaxMode.Read(reader);
				this.rules = new List<Rule>(baseMode.Rules);
				this.keywords = new List<Keywords>(baseMode.Keywords);
				this.spans = new List<Span>(baseMode.Spans.Where(span => span.Begin.Pattern != "#")).ToArray();
				matches.AddRange(baseMode.Matches);
				this.prevMarker = baseMode.PrevMarker;
				this.SemanticRules = new List<SemanticRule>(baseMode.SemanticRules);
				this.keywordTable = baseMode.keywordTable;
				this.keywordTableIgnoreCase = baseMode.keywordTableIgnoreCase;
				this.properties = baseMode.Properties;
			}

			var st = new StringReader("<Match color = \"constant.digit\"><![CDATA[(?<!\\w)(0((x|X)[0-9a-fA-F_]+|(b|B)[0-1_]+)|([0-9]+[_0-9]*)[L|U|u|f|i]*)]]></Match>");

			var x = new XmlTextReader(st);
			x.Read();
			matches.Add(Match.Read(x));
			st.Close();
			//this.AddSemanticRule(new DNumberSemanticRule());
			this.matches = matches.ToArray();
		}
	}
}
