//
// GuiBuilderWindow.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
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
using MonoDevelop.GtkCore.GuiBuilder;
using MonoDevelop.Core;

namespace MonoDevelop.D.GuiBuilder
{
	public class GuiBuilderWindow : IDisposable
	{
		Stetic.WidgetInfo rootWidget;
		GuiBuilderProject fproject;
		Stetic.Project gproject;
		string name;

		public event WindowEventHandler Changed;

		internal GuiBuilderWindow (GuiBuilderProject fproject, Stetic.Project gproject, Stetic.WidgetInfo rootWidget)
		{
			this.fproject = fproject;
			this.rootWidget = rootWidget;
			this.gproject = gproject;
			name = rootWidget.Name;
			gproject.ProjectReloaded += OnProjectReloaded;
			rootWidget.Changed += OnChanged;
		}

		public Stetic.WidgetInfo RootWidget {
			get { return rootWidget; }
		}

		public GuiBuilderProject Project {
			get { return fproject; }
		}

		public string Name {
			get { return rootWidget.Name; }
		}

		public FilePath SourceCodeFile {
			get { return fproject.GetSourceCodeFile (rootWidget); }
		}

		public void Dispose ()
		{
			gproject.ProjectReloaded -= OnProjectReloaded;
			rootWidget.Changed -= OnChanged;
		}

		void OnProjectReloaded (object s, EventArgs args)
		{
			rootWidget.Changed -= OnChanged;
			rootWidget = gproject.GetWidget (name);
			if (rootWidget != null)
				rootWidget.Changed += OnChanged;
		}

		void OnChanged (object o, EventArgs args)
		{
			// Update the name, it may have changed
			name = rootWidget.Name;

			if (Changed != null)
				Changed (this, new WindowEventArgs (this));
		}
		/*
		void AddSignalsRec (CodeTypeDeclaration type, Stetic.Component comp)
		{
			foreach (Stetic.Signal signal in comp.GetSignals ()) {
				CodeMemberMethod met = new CodeMemberMethod ();
				met.Name = signal.Handler;
				met.Attributes = MemberAttributes.Family;
				met.ReturnType = new CodeTypeReference (signal.SignalDescriptor.HandlerReturnTypeName);

				foreach (Stetic.ParameterDescriptor pinfo in signal.SignalDescriptor.HandlerParameters)
					met.Parameters.Add (new CodeParameterDeclarationExpression (pinfo.TypeName, pinfo.Name));

				type.Members.Add (met);
			}
			foreach (Stetic.Component cc in comp.GetChildren ()) {
				AddSignalsRec (type, cc);
			}
		}*/
	}
}

