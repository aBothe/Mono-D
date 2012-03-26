@echo off

xcopy /Q "MonoDevelop.DBinding\bin\Debug\*" "..\tutorial.lib\MonoDevelop\main\build\AddIns\BackendBindings" /Y

pause