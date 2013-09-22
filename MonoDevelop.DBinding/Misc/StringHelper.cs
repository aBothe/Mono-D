//
// StringHelper.cs
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

namespace MonoDevelop.D.Misc
{
	public static class StringHelper
	{
		public static List<string> SplitLines(string lines)
		{
			var l = new List<string> ();
			l.AddLineSplittedString (lines);
			return l;
		}

		public static void AddLineSplittedString(this List<string> list, string input)
		{
			if (string.IsNullOrEmpty (input))
				return;

			int last=0,i=0;
			var newL = input.Contains("\r") ? "\r\n" : "\n";
			var newLLen = newL.Length;
			while((i = input.IndexOf(newL, last))>-1)
			{
				if(i-last > 1)
					list.Add (input.Substring(last, i-last));
				last = i + newLLen;
			}

			if(last < input.Length)
				list.Add (input.Substring(last));
		}
	}
}

