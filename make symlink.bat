@echo off

rem Creates a symlink between the Mono-D output directory and the addins dictionary of MonoDevelop
rem to save copying the lib files over and over again

rem This batch file must be ran as admin!

rem TODO: Fit to target path
mklink /D /J "C:\Program Files (x86)\Xamarin Studio\AddIns\mono-d" MonoDevelop.DBinding\bin\Debug

pause