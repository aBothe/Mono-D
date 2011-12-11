using System;
using System.Collections.Generic;
using Gtk;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Components;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Output;
using MonoDevelop.D.Completion;
using MonoDevelop.D.Parser;
using D_Parser;
using D_Parser.Dom;


namespace MonoDevelop.D.Gui
{
	public class EditorPathbarProvider : DropDownBoxListWindow.IListDataProvider
	{
		object tag;
		IAbstractSyntaxTree syntaxTree;
		List<D_Parser.Dom.INode> memberList = new List<D_Parser.Dom.INode> ();
		
		Document document { get; set; }
		
		public EditorPathbarProvider (Document doc, object tag)
		{
			this.document = doc;
			this.tag = ((D_Parser.Dom.INode)tag).Parent;
			
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
			foreach(D_Parser.Dom.INode nd in blockNode.Children)
			{					
				if ((nd is IBlockNode) || (nd is DEnumValue)) {
					memberList.Add(nd);
				}
			}
			
			memberList.Sort ((x, y) => String.Compare (x.Name + DParameterDataProvider.GetNodeParamString(x), y.Name + DParameterDataProvider.GetNodeParamString(y), StringComparison.OrdinalIgnoreCase));
		}		
				
		public string GetMarkup (int n)
		{
			return memberList[n].Name +  DParameterDataProvider.GetNodeParamString(memberList[n]);
		}

		public Gdk.Pixbuf GetIcon (int n)
		{			
			var icon=DCompletionData.GetNodeIcon(memberList[n] as DNode);
			return ImageService.GetPixbuf(icon.Name, IconSize.Menu);
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
				extEditor.SetCaretTo (Math.Max (1, member.StartLocation.Line), member.StartLocation.Column);
		}


		#endregion
	}
}

