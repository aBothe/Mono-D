//
// AddLibraryDialog.cs: A simple open file dialog to add libraries to the project (using vapi files)
//
// Authors:
//  Levi Bard <taktaktaktaktaktaktaktaktaktak@gmail.com> 
//
// Copyright (C) 2008 Levi Bard
// Based on CBinding by Marcos David Marin Amador <MarcosMarin@gmail.com>
//
// This source code is licenced under The MIT License:
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
namespace MonoDevelop.D
{
	public partial class AddLibraryDialog : Gtk.Dialog
	{
		public enum FileFilterType
		{
			DFiles,
			LibraryFiles
		}
			
		public AddLibraryDialog (FileFilterType filterType)
		{
			this.Build ();
			Init (filterType);
		}
		
		private void Init (FileFilterType filterType)
		{
			Gtk.FileFilter libs = new Gtk.FileFilter ();
			Gtk.FileFilter all = new Gtk.FileFilter ();

			switch (filterType) {
			case FileFilterType.DFiles:
				libs.AddPattern ("*.d");
				libs.AddPattern ("*.di");			
				libs.Name = "D Files";				
				break;
			case FileFilterType.LibraryFiles:
				libs.AddPattern ("*.a");
				libs.AddPattern ("*.lib");
				libs.AddPattern ("*.so");
				libs.AddPattern ("*.dylib");							
				libs.Name = "Libraries";
				break;
			}

			all.AddPattern ("*.*");
			all.Name = "All Files";
			
			file_chooser_widget.AddFilter (libs);
			file_chooser_widget.AddFilter (all);
			
			if (Environment.OSVersion.Platform == PlatformID.Unix)
				file_chooser_widget.SetCurrentFolder ("/usr/share/d/di");			
		}
		
		private void OnOkButtonClick (object sender, EventArgs e)
		{
			Destroy ();
		}
		
		private void OnCancelButtonClick (object sender, EventArgs e)
		{
			Destroy ();
		}
		
		public string SelectedFileName {
			get {
				return file_chooser_widget.Filename;
			}
			set {
				file_chooser_widget.SetFilename (value);
			}
		}
	}
}
