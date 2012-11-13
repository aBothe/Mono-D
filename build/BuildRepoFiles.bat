@echo off

set P=..\..\tutorial.lib\monodevelop\main\build\bin\mdtool.exe

%P% setup pack ..\MonoDevelop.DBinding\bin\Debug\MonoDevelop.D.dll
%P% setup rep-build .