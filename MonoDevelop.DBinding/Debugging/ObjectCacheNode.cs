//
// ObjectCacheNode.cs
//
// Author:
//       lx <>
//
// Copyright (c) 2013 lx
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
using D_Parser.Resolver;
using Mono.Debugging.Client;

namespace MonoDevelop.D.Debugging
{
	class ObjectRootCacheNode : ObjectCacheNode
	{
		public ObjectRootCacheNode() : base() {}
	}

	class SubArrayCacheNode : ObjectCacheNode
	{
		public readonly ulong firstItem;
		public readonly int length;

		public SubArrayCacheNode(string n, AbstractType t, ulong firstItemPointer, int len)
			: base(n, t, firstItemPointer)
		{
			firstItem = firstItemPointer;
			length = len;
		}
	}

	class ObjectCacheNode
	{
		public readonly string Name;
		Dictionary<string, ObjectCacheNode> children = new Dictionary<string, ObjectCacheNode>();
		public readonly AbstractType NodeType;
		public readonly ulong address;

		protected ObjectCacheNode() {}
		public ObjectCacheNode(string n, AbstractType t, ulong address) {
			if(string.IsNullOrEmpty(n))
				throw new ArgumentNullException("name","Child name must not be empty!");
			if(t == null)
				throw new ArgumentNullException("t","Abstract type of '"+n+"' must not be null!");

			Name = n;
			NodeType = t;
			this.address = address;
		}

		public void Clear()
		{
			children.Clear ();
		}

		public ObjectCacheNode this[string n]
		{
			get{
				if (string.IsNullOrEmpty (n))
					return null;

				ObjectCacheNode ch;
				children.TryGetValue (n, out ch);
				return ch;
			}
		}

		public ObjectCacheNode this[ObjectPath path]
		{
			get{
				ObjectCacheNode n= this;

				for(int i = 0; i < path.Length; i++)
					if ((n = n [path[i]]) == null)
						break;
				return n;
			}
		}

		public void Set(ObjectCacheNode ch)
		{
			if (ch == null || ch is ObjectRootCacheNode || ch == this)
				throw new ArgumentException("ch and the name must be set and must not be the root node");

			children [ch.Name] = ch;
		}
	}
}

