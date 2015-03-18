//
// DRefactoringContext.cs
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
using System;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.D.Parser;
using D_Parser.Resolver;
using MonoDevelop.D.Resolver;
using D_Parser.Completion;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom;

namespace MonoDevelop.D.Refactoring
{
	public class DRefactoringContext : IRefactoringContext
	{
		public readonly Document Doc;
		public readonly ParsedDModule ParsedDoc;
		AbstractType[] lastResults;

		public IEditorData ed;
		public LooseResolution.NodeResolutionAttempt resultResolutionAttempt;
		public ISyntaxRegion syntaxObject;

		public AbstractType[] CurrentResults
		{
			get{
				if (lastResults == null)
					lastResults = DResolverWrapper.ResolveHoveredCodeLoosely (out ed, out resultResolutionAttempt, out syntaxObject, Doc);

				return lastResults;
			}
		}

		public DRefactoringContext (Document doc, ParsedDModule mod)
		{
			this.ParsedDoc = mod;
			this.Doc = doc;
		}

		public IDisposable CreateScript ()
		{
			return null;
		}
	}
}

