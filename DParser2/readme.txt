
D_Parser - A library for D code analysis and completion mechanics
===========================================================================
by Alexander Bothe


Released under the GPL.
http://www.gnu.org/licenses/gpl-3.0


==========================
Completion and stuff
==========================

The completion flow:

1) Code parsing / Syntactical analysis
2) Type resolution & intelligent linkage / Semantic analysis 
3) Completion (Interpreting the resolved type/evaluated value's type) / Intelligent usage of the semantics 

In actual code, you would
1) parse some code or an expression via DParser.ParseExpression("1 + 2") or DParser.ParseFile("myFile.d");
2) evaluate the value/type of the expression via Evaluation.EvaluateValue(expression); or Evaluation.EvaluateType(expression);
	(Optionally passing a resolution context that provides e.g. variables, classes or methods)
	TypeDeclarationResolver.Resolve(myTypeDeclaration); can be used for handling things like int[] or MyType**
	-- to build up a sophisticated syntax hierarchy that points e.g. to MyType inside.
3) Let the completion providers generate all items that should be shown in the completion window
	(Calling the AbstractCompletionProvider.BuildCompletioData method is already sufficient!)
3) ???
4) Profit!




=============================
Module caching
=============================

For more efficient and cross-module completion, it's possible to cache entire libraries (like druntime, phobos and/or tango).

There are a) ParseCaches and b) ParseCacheLists.

ParseCacheLists contain at least one ParseCache.
ParseCaches store a package hierarchy beginning with exactly one Root package.
	The reason that I let CacheLists manage Cache objects is that in a commonly known situation, 
	there will be several independent caches 
	- like the Global cache (that contains the phobos/druntime/tango modules), 
	and a Local project cache (that contains all modules from a project - whereas these are physically totally independent from the global cache)
	Furthermore, a project might have project-wide includes, so the resulting cache stands between the global and local cache.
	All in all, you ended up in having 3 caches that are independent from each other.

Root packages and ModulePackages contain other ModulePackages and DModules.

To analyse several directories, just type
	var cache = new ParseCache();

	cache.BeginParse(new[] { // Begin to analyse directories multi-threaded
		dmdBase+@"\druntime\import",
		dmdBase+@"\phobos"
	},dmdBase+@"\phobos"); // If a directory item in the list isn't absolute, this will be put in front of the relative path

	cache.WaitForParserFinish();

	var stdio = pc.GetModule("std.stdio"); // Now the stdio object should contain the syntax tree of std.stdio





=============================
Formatting
=============================


How to get the correct indent of a D code line:

int correctTabIndent = new DFormatter().CalculateIndentation(myRawCode, lineToFormat).GetLineIndentation(lineToFormat);


