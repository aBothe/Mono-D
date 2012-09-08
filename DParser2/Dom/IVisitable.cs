using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Dom
{
	public interface IVisitable<Visitor> where Visitor : IVisitor
	{
		void Accept(Visitor vis);
		//R Accept<R>(RetVisitor vis);
	}
	/*
	public interface IVisitiable_Return<Visitor, out R> where Visitor : IVisitor<R>
	{
		R Accept<R>(IVisitor<R> vis);
	}*/
}
