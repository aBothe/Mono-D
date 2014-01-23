//
// TooltipInfoGen.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using MonoDevelop.Ide.CodeCompletion;
using D_Parser.Resolver;
using Mono.TextEditor.Highlighting;
using D_Parser.Dom;
using System.Collections.Generic;

namespace MonoDevelop.D.Completion
{
	public static class TooltipInfoGen
	{
		//TODO: Für semantisches Highlighting den TypeRefFinder benutzen und einfach pauschal alle Ids entsprechend highlighten
		public static TooltipInformation Create(AbstractType t, ColorScheme st, bool templateParamCompletion = false, int currentMethodParam = -1)
		{
			var markupGen = new TooltipMarkupGen (st);

			var tti = new TooltipInformation { 
				SignatureMarkup = markupGen.GenTooltipSignature (t, templateParamCompletion, currentMethodParam)
			};

			var ds = t as DSymbol;
			if (ds != null)
				CreateTooltipBody (markupGen, ds.Definition, tti);

			return tti;
		}

		public static TooltipInformation Create(DNode dn, ColorScheme st, bool templateParamCompletion = false, int currentMethodParam = -1)
		{
			var markupGen = new TooltipMarkupGen (st);

			var tti = new TooltipInformation { 
				SignatureMarkup = markupGen.GenTooltipSignature(dn, templateParamCompletion, currentMethodParam)
			};

			CreateTooltipBody (markupGen, dn, tti);

			return tti;
		}

		static void CreateTooltipBody(TooltipMarkupGen markupGen, DNode dn, TooltipInformation tti)
		{
			string summary;
			Dictionary<string,string> categories;

			markupGen.GenToolTipBody (dn, out summary, out categories);

			tti.SummaryMarkup = summary;
			if (categories != null)
				foreach (var kv in categories)
					tti.AddCategory (kv.Key, kv.Value);
		}
	}
}

