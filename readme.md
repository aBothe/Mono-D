
Mono-D is a language binding for MonoDevelop for the [D Programming language](http://dlang.org).

[Project site](http://mono-d.alexanderbothe.com).

Few (none?) GNU/Linux distros package a new enough MonoDevelop required by Mono-D.
You can download pre-built binaries built with mono3 below. Unpack to `/opt/mono`.
* [MonoDevelop 5 GNU/Linux **x86**](http://simendsjo.me/files/abothe/MonoDevelop.x86.Master.tar.xz)
* [MonoDevelop 5 GNU/Linux **x64**](http://simendsjo.me/files/abothe/MonoDevelop.x64.Master.tar.xz)

Mono-D is a plugin for *Xamarin Studio 5* when the platform is *Windows or Mac OS X*. 
* Download [Xamarin Studio for Windows](http://xamarin.com/)
* Subscribe to the Alpha Channel in "Help > Check for Updates..."
* Upgrade to Xamarin Studio 5+
* You can now install Mono-D through the [Add-in manager](http://mono-d.alexanderbothe.com/download/#mono-d)

# How to initialize Mono-D development under Linux

* Setup MonoDevelop to use /opt/mono instead of other runtimes
	- Open MonoDevelop
	- Go to Edit -> Options -> Projects tab -> .NET Runtimes
	- Enter /opt/mono as new runtime if it's not there already!
	- Select it as standard runtime
	- Now you can build Mono-D with its gtk-sharp dependencies and no Mono.Cairo conflicts
* Clone Mono-D
	- Open a terminal in your projects folder
	- `git clone https://github.com/aBothe/Mono-D.git`
	- `cd Mono-D`
	- `git submodule init`
	- `git submodule update`
* Build Mono-D
	- Open & build the main solution
	- Add symlinks from projectdir/Mono-D/bin/Debug/* to your MonoDevelop AddIns folder 
		- `cd /opt/mono/lib/monodevelop/AddIns` 
		- `ln -s -d %YourProjectDirectory%/Mono-D/bin/Debug D`
	- In the solution view, open the MonoDevelop.D options -> Properties -> Run -> Custom Commands
	- Choose Execute
	- Set `/opt/mono/bin/monodevelop` as executable
	- Working directory can be left empty
	- Confirm via OK
	- Press F5 to debug

# How to initialize Mono-D development under Windows

* Remove Mono-D from Xamarin (if you already have it installed)
	- In the top menu, navigate to: Tools > Add-in Manager > Language Bindings
	- Select "D Language Binding" and press Uninstall...
	- The make symlink.bat will reinstall it from the development Debug folder
* Clone Mono-D
	- Open a git bash in your projects folder
	- `git clone https://github.com/aBothe/Mono-D.git`
	- `cd Mono-D`
	- `git submodule init`
	- `git submodule update`
* Build Mono-D
	- Open & build the main solution inside Visual Studio or Xamarin Studio (the latter is required to do changes on Gtk#-based Option Panels etc.)
	- Add symlinks from projectdir/Mono-D/bin/Debug/* to your MonoDevelop AddIns folder 
		- Run the `make symlink.bat` that is located inside the folder root
	- You might want to set a new default executable path to XamarinStudio.exe, but normally, this is not necessary if you've installed XS via the normal installer
	- Run & Debug Mono-D
