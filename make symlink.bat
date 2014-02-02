@echo off

rem Creates a symlink between the Mono-D output directory and the addins dictionary of MonoDevelop
rem to save copying the lib files over and over again

rem TODO: Fit to target path
mklink /D /J "%APPDATA%\..\Local\XamarinStudio-4.0\LocalInstall\Addins\mono-d" MonoDevelop.DBinding\bin\Debug

pause