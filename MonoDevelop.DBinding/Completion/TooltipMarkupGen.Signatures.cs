//
// TooltipMarkupHeaderGEn.cs
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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Mono.TextEditor.Highlighting;
using D_Parser.Parser;
using Mono.TextEditor;
using MonoDevelop.D.Highlighting;
using D_Parser.Resolver;
using D_Parser.Dom;

namespace MonoDevelop.D.Completion
{
	public partial class TooltipMarkupGen
	{
		public static string GenTooltipSignature(AbstractType t, ColorScheme st, bool templateParamCompletion = false, int currentMethodParam = -1)
		{
			return DCodeToMarkup (st, t.ToCode ());
		}

		public static string GenTooltipSignature(DNode dn, ColorScheme st, bool templateParamCompletion = false, int currentMethodParam = -1)
		{
			var sb = new StringBuilder();
			sb.Append("<i>(");

			if (dn is DClassLike)
			{
				switch ((dn as DClassLike).ClassType)
				{
					case DTokens.Class:
						sb.Append("Class");
						break;
					case DTokens.Template:
						if (dn.ContainsAttribute(DTokens.Mixin))
							sb.Append("Mixin ");
						sb.Append("Template");
						break;
					case DTokens.Struct:
						sb.Append("Struct");
						break;
					case DTokens.Union:
						sb.Append("Union");
						break;
				}
			}
			else if (dn is DEnum)
			{
				sb.Append("Enum");
			}
			else if (dn is DEnumValue)
			{
				sb.Append("Enum Value");
			}
			else if (dn is DVariable)
			{
				if (dn.Parent is DMethod)
				{
					var dm = dn.Parent as DMethod;
					if (dm.Parameters.Contains(dn))
						sb.Append("Parameters");
					else
						sb.Append("Local");
				}
				else if (dn.Parent is DClassLike)
					sb.Append("Field");
				else
					sb.Append("Variable");
			}
			else if (dn is DMethod)
			{
				sb.Append("Method");
			}
			else if (dn is TemplateParameter.Node)
			{
				sb.Append("Template Parameter");
			}

			sb.Append(")</i> ");

			return sb.Append (dn.ToString(false,false)).ToString ();
		}
	}
}

