using System;
using MonoDevelop.Ide.Gui.Content;

namespace MonoDevelop.D.Formatting.Indentation
{
	public class DIndentEngine : ICloneable, IDocumentStateEngine
	{
		#region Properties
		DFormattingPolicy policy;
		TextStylePolicy textStyle;
		#endregion
		
		#region Constructor/Init
		public DIndentEngine(DFormattingPolicy policy, TextStylePolicy textStyle)
		{
			this.policy = policy;
			this.textStyle = textStyle;
		}
		#endregion
		
		public object Clone()
		{
			throw new NotImplementedException();
		}
		
		public int Position {
			get {
				throw new NotImplementedException();
			}
		}
		
		public void Push(char c)
		{
			throw new NotImplementedException();
		}
		
		public void Reset()
		{
			throw new NotImplementedException();
		}
	}
}
