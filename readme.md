
Mono-D is a language binding for MonoDevelop for the [D Programming language](http://dlang.org).

[Project site](http://mono-d.alexanderbothe.com).


# Setting up MonoDevelop on Linux to work on the addin

How I have done it:

## Install simendsjo's MonoDevelop build for Linux x64

## Build Mono

- Open terminal in your project directory
- `git clone https://github.com/mono/mono.git`
- `cd mono`
- `./configure --prefix=/opt/mono`
- `make` (Will take ~10 Minutes to build!)
- `sudo make install` installs the files to /opt/mono

## Get Gtk-Sharp

- Visit 
	https://www.archlinux.org/packages/extra/x86_64/gtk-sharp-2 for x64,
	https://www.archlinux.org/packages/extra/i686/gtk-sharp-2 for x86 Linux Systems
	and download the .tar.xz via 'Download from Mirror'
- Extract the archive to /opt/mono (so that the bin,lib,share folder from /opt/mono and the archive match!)

 ## Setup MonoDevelop

- Open the MonoDevelop located in /opt/xs-4.0.2-mono-d/monodevelop
- Go to Edit -> Options -> Projects tab -> .NET Runtimes
- Enter /opt/mono as new runtime
- Select it as standard runtime
- Now you can build Mono-D with its gtk-sharp dependencies and no Mono.Cairo conflicts

## Clone Mono-D
- Open a terminal in your projects folder
- `git clone https://github.com/aBothe/Mono-D.git`
- `git submodule init`
- `git submodule update`

## Build Mono-D
- Build the solution
- Add symlinks from projectdir/Mono-D/bin/Debug/* to your MonoDevelop AddIns folder 
	- `cd /opt/xs-4.0.2-mono-d/xs-4.0.2/lib/monodevelop/AddIns` 
	- `ln -s -d %YourProjectDirectory%/Mono-D/bin/Debug D`
- In the solution view, open the MonoDevelop.D options -> Properties -> Run -> Custom Commands
- Choose Execute
- Set `/opt/xs-4.0.2-mono-d/monodevelop` as executable
- Working directory can be left empty
- Confirm via OK
- Press F5 to debug

You can develop Mono-D and debug/test/use Mono-D in the target MonoDevelop installation now!
