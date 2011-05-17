using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Projects.Dom.Parser;
using System.IO;
using MonoDevelop.Projects.Dom;
using MonoDevelop.D.Parser.Lexer;
using MonoDevelop.Core;

namespace MonoDevelop.D.Parser
{
	public class DModule : ICompilationUnit
	{
		public IEnumerable<IAttribute> Attributes
		{
			get { return null; }
		}

		public FilePath FileName
		{
			get { throw new NotImplementedException(); }
		}

		public IMember GetMemberAt(DomLocation location)
		{
			throw new NotImplementedException();
		}

		public IMember GetMemberAt(int line, int column)
		{
			throw new NotImplementedException();
		}

		public void GetNamespaceContents(List<IMember> list, string subNameSpace, bool caseSensitive)
		{
			throw new NotImplementedException();
		}

		public IType GetType(string fullName, int genericParameterCount)
		{
			throw new NotImplementedException();
		}

		public IType GetTypeAt(DomLocation location)
		{
			throw new NotImplementedException();
		}

		public IType GetTypeAt(int line, int column)
		{
			throw new NotImplementedException();
		}

		public bool IsNamespaceUsedAt(string name, DomLocation location)
		{
			throw new NotImplementedException();
		}

		public bool IsNamespaceUsedAt(string name, int line, int column)
		{
			throw new NotImplementedException();
		}

		public IReturnType ShortenTypeName(IReturnType fullyQualfiedType, int line, int column)
		{
			throw new NotImplementedException();
		}

		public IReturnType ShortenTypeName(IReturnType fullyQualfiedType, DomLocation location)
		{
			throw new NotImplementedException();
		}

		public System.Collections.ObjectModel.ReadOnlyCollection<IType> Types
		{
			get { throw new NotImplementedException(); }
		}

		public System.Collections.ObjectModel.ReadOnlyCollection<IUsing> Usings
		{
			get { throw new NotImplementedException(); }
		}

		public S AcceptVisitor<T, S>(IDomVisitor<T, S> visitor, T data)
		{
			throw new NotImplementedException();
		}

		public INode FirstChild
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public INode LastChild
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public INode NextSibling
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public INode Parent
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public INode PrevSibling
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public int Role
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}
	}

	public class DNode : AbstractNode
	{
	}
}
