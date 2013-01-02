//
// ProfilerModeHandler.cs
//
// Author:
//       foerdi <>
//
// Copyright (c) 2013 foerdi
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
using MonoDevelop.Components.Commands;
using MonoDevelop.D.Profiler.Gui;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Profiler.Commands
{
	public class ProfilerModeHandler : CommandHandler
	{
		private static bool isProfilerMode;
		public static bool IsProfilerMode 
		{
			get { return isProfilerMode; } 
			set 
			{
				isProfilerMode = value;
				
				Pad pad =Ide.IdeApp.Workbench.GetPad<DProfilerPad>();
				if(pad == null || !(pad.Content is DProfilerPad))
					return;
				
				((DProfilerPad)pad.Content).Widget.RefreshSwitchProfilingIcon();
			}
		}
		
		protected override void Update (CommandInfo info)
		{
			base.Update (info);
			info.Checked = IsProfilerMode;
		}
		
		protected override void Run ()
		{
			IsProfilerMode = !IsProfilerMode;
			
			if(IsProfilerMode == false)
				DProfilerPad.ShowPad(false);
		}
	}
}

