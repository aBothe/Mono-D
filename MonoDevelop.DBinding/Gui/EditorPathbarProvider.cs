using System;
using System.Collections.Generic;
using D_Parser.Dom;
using Gtk;
using MonoDevelop.Components;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using D_Parser.Resolver.ASTScanner;


namespace MonoDevelop.D.Gui
{
	class EditorPathbarProvider : DropDownBoxListWindow.IListDataProvider
	{
		object tag;
		DModule syntaxTree;
		List<INode> memberList = new List<INode> ();
		
		Document document { get; set; }
		
		public EditorPathbarProvider (Document doc, object tag)
		{
			this.document = doc;
			this.tag = ((INode)tag).Parent;
			
			var ast = document.ParsedDocument as ParsedDModule;
			if (ast != null)			
			syntaxTree = ast.DDom;				
			
			Reset ();
		}
		
		#region IListDataProvider implementation
	
		public int IconCount {
			get {
				return memberList.Count;
			}
		}
		
		public void Reset ()
		{
			memberList.Clear ();
			if (!(tag is IBlockNode))
				return;
			var blockNode = (tag as IBlockNode);
			foreach(var nd in blockNode.Children)
				if (AbstractVisitor.CanAddMemberOfType(MemberFilter.All, nd))
					memberList.Add(nd);
			
			memberList.Sort ((x, y) => x.Name.CompareTo(y.Name));
		}		
				
		public string GetMarkup (int n)
		{
			return memberList[n].Name +  DParameterDataProvider.GetNodeParamString(memberList[n]);
		}

		Xwt.Drawing.Image DropDownBoxListWindow.IListDataProvider.GetIcon(int n)
		{
			var icon = DIcons.GetNodeIcon(memberList[n] as DNode);
			return ImageService.GetIcon(icon.Name, IconSize.Menu);
		}

		public object GetTag (int n)
		{
			return memberList[n];
		}

		public void ActivateItem (int n)
		{
			var member = memberList[n];
			MonoDevelop.Ide.Gui.Content.IExtensibleTextEditor extEditor = document.GetContent<MonoDevelop.Ide.Gui.Content.IExtensibleTextEditor> ();
			if (extEditor != null)
				extEditor.SetCaretTo (Math.Max (1, member.NameLocation.Line), member.NameLocation.Column);
		}
		#endregion
	}
}

