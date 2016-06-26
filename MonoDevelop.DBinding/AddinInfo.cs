using System.Reflection;
using Mono.Addins;

[assembly: Addin ("DLanguageBinding", "3.0.0.0", Namespace = "MonoDevelop", EnabledByDefault = true)]
[assembly: AddinName("D Language Binding")]
[assembly: AddinUrl("http://wiki.dlang.org/Mono-D")]
[assembly: AddinAuthor("Alexander Bothe et alia")]
[assembly: AddinCategory("Language bindings")]
[assembly: AddinDescription("Language binding for the D programming language.")]

[assembly: ImportAddinAssembly("D_Parser.dll")]

[assembly: AddinDependency("Core", MonoDevelop.BuildInfo.Version)]
[assembly: AddinDependency("Ide", MonoDevelop.BuildInfo.Version)]

[assembly: AssemblyTitle("MonoDevelop.D")]
[assembly: AssemblyDescription("D Support for the MonoDevelop/XamarinStudio IDE")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("Alexander Bothe")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion ("2.0.*")]
