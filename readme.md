
Mono-D is a language binding for MonoDevelop for the [D Programming language](http://dlang.org).

[Project site](http://wiki.dlang.org/Mono-D).

# Dev agenda
## General
* [ ] Prepare D addin to be used & extended in MonoDevelop
* [ ] Bind in text/d mimetype
* [ ] Project & File templates

## Project/building support
* [ ] Dub
* [ ] Native/Legacy Mono-D projects
* [ ] Visual-D
* [ ] Project build option panels
* [ ] Global build option panels
* [ ] D Option Panels

### Further building-related features
* [ ] DMD profiling evaluation support
* [ ] Unittests build & report evaluation support
* [ ] Makefile/dub.sdl generation support?
* [ ] Dub registry browser?

## Editing support
* [ ] Static Syntax highlighting
* [ ] Re-Establish completion infrastructure with D_Parser
* [ ] Semantic/AST-affine syntax highlighting
* [ ] Global/Local option panel for syntax settings

### Code indentation/formatting
* [ ] Provide indentation binding codebase
* [ ] Provide formatting binding codebase
* [ ] Re-Use existing indentation API from D_Parser
* [ ] Re-Use existing formatting API from D_Parser
* [ ] Extend D_Parser's formatting abilities

## Debug support
* [ ] Find a way to p/invoke native WinDbg-Engine infrastructure without using C++/CLI anymore
* [ ] Hook on gdb
* [ ] Have a generalized infrastructure for using parser's AST cache as variable evaluation assistance
* [ ] Hack in toString()-Evaluation, especially on WinDBG which requires heavy tinkering
* [ ] Dynamic expression evaluation -- why not CTFE-ish pure code execution using run-time local values?

## Obligatory editing-candy
* [ ] Find symbol references
* [ ] Symbol renaming
* [ ] Symbol import-stmt generation

### Stuff I've experienced while using Eclipse in real-world use cases:
* [ ] Automatic import-stmt generation/deletion/sort/"organization"
* [ ] Method extraction
* [ ] Switch-case completion && write down all possible cases
* [ ] Expression2LocalVariable extraction
* [ ] Display type inheritance hierachy
* [ ] Check local-scoped symbols for whether not being used anymore
* [ ] Have diff-based highlighting
* [ ] Mixin evaluation panel for real-time execution-alike code insight magic

# Get MonoDevelop on Linux

Few (none?) GNU/Linux distros package a new enough MonoDevelop required by Mono-D.
You can download pre-built binaries built with mono below. Unpack to `/opt/mono`.
* [MonoDevelop GNU/Linux **x64**](http://simendsjo.me/files/abothe/MonoDevelop.x64.Master.tar.xz)
* [Setup/Compilation/packaging helpers for MonoDevelop etc.](http://simendsjo.me/files/abothe)

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
