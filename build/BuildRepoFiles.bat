@echo off

mdtool setup pack ..\MonoDevelop.DBinding\bin\Debug\MonoDevelop.D.dll
mdtool setup rep-build .

pause