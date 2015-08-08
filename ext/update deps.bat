@echo off

set xsbin=C:\Program Files (x86)\Xamarin Studio\bin
set xsaddin=C:\Program Files (x86)\Xamarin Studio\Addins

echo \bin:
copy "%xsbin%\Mono.Addins.dll" .
copy "%xsbin%\Mono.Addins.Setup.dll" .
copy "%xsbin%\Mono.Debugging.dll" .
copy "%xsbin%\Mono.TextEditor.dll" .
copy "%xsbin%\MonoDevelop.Core.dll" .
copy "%xsbin%\MonoDevelop.Ide.dll" .
copy "%xsbin%\Newtonsoft.Json.dll" .
copy "%xsbin%\ICSharpCode.NRefactory.dll" .
copy "%xsbin%\ICSharpCode.NRefactory.CSharp.dll" .
copy "%xsbin%\ICSharpCode.SharpZipLib.dll" .
copy "%xsbin%\Xwt.dll" .

echo \Addins:
copy "%xsaddin%\MonoDevelop.Debugger\MonoDevelop.Debugger.dll" .
copy "%xsaddin%\MonoDevelop.DesignerSupport\MonoDevelop.DesignerSupport.dll" .
copy "%xsaddin%\MonoDevelop.GtkCore\MonoDevelop.GtkCore.dll" .
copy "%xsaddin%\MonoDevelop.GtkCore\libsteticui.dll" .
copy "%xsaddin%\MonoDevelop.GtkCore\libstetic.dll" .
copy "%xsaddin%\MonoDevelop.Refactoring\MonoDevelop.Refactoring.dll" .
copy "%xsaddin%\DisplayBindings\SourceEditor\MonoDevelop.SourceEditor2.dll" .

