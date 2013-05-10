
Mono-D is a language binding for MonoDevelop for the [D Programming language](http://dlang.org).

[Project site](http://mono-d.alexanderbothe.com).


# How to build Mono and MonoDevelop under Linux

* Install any mono version >2.4 that is available for your linux distro. It will be needed for building mono (yes, read twice, mono *is* needed to build mono!)

* Build Mono
	- Open terminal in your project directory
	- `git clone https://github.com/mono/mono.git`
	- `cd mono`
	- `./autogen.sh --prefix=/opt/mono`
	- `make` (Will take ~10 Minutes to build!)
	- `sudo make install` installs the files to /opt/mono
* Get Gtk-Sharp
	- Visit 
		https://www.archlinux.org/packages/extra/x86_64/gtk-sharp-2 for x64,
		https://www.archlinux.org/packages/extra/i686/gtk-sharp-2 for x86 Linux Systems
		and download the .tar.xz via 'Download from Mirror'
	- Extract the archive to /opt/mono (so that the bin,lib,share folder from /opt/mono and the archive match!)
* Copy missing gnome-sharp, gnome-vfs-sharp etc. libraries to /opt/mono/lib/mono/gac
* Build MonoDevelop
	- - Open terminal in your project directory
	- `git clone https://github.com/mono/monodevelop.git`
	- `cd monodevelop`
	- `export PATH="/opt/mono:$PATH"`
	- `./configure --prefix=/opt/mono`
	- `make` -- In the case make will fail, build MonoDevelop in an older installation of MonoDevelop using the Debug configuration!
	- `make install`
	- MonoDevelop should be built right now.
	- `sudo gedit /opt/mono/bin/monodevelop` (Or your favorite editor)
	- add `export LD_LIBRARY_PATH="/opt/mono/lib:$LD_LIBRARY_PATH"` to the other exports at the upper part of the file
	- modify the MONO_EXEC line to `MONO_EXEC="exec -a monodevelop /opt/mono/bin/mono"` so it'll take the mono installation in /opt/mono by default!
	- Now you can open MonoDevelop via /opt/mono/bin/monodevelop

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
* You can develop Mono-D and debug/test/use Mono-D in the target MonoDevelop installation now!
