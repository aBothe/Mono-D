using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Xml;
using System.Threading;

namespace MonoDevelop.D.Building
{
	public enum DCompileTarget
	{
		/// <summary>
		/// A normal console application.
		/// </summary>
		Executable,

		/// <summary>
		/// Applications which explicitly draw themselves a custom GUI and do not need a console.
		/// Usually 'Desktop' applications.
		/// </summary>
		ConsolelessExecutable,

		SharedLibrary,
		StaticLibrary
	}

	public enum DCompilerVendor
	{
		DMD,
		GDC,
		LDC
	}

	/// <summary>
	/// Central class which enables build support for D projects in MonoDevelop.
	/// </summary>
	public class DCompiler : ICustomXmlSerializer
	{
		static DCompiler _instance = null;
		public static DCompiler Instance
		{
			get
			{
				// If not initialized yet, load configuration
				if (!IsInitialized)
					Load();

				return _instance;
			}
		}

		#region Init/Loading & Saving
		public static void Load()
		{
			// Deserialize config data
			_instance=PropertyService.Get<DCompiler>(GlobalPropertyName);

			//LoggingService.AddLogger(new MonoDevelop.Core.Logging.FileLogger("A:\\monoDev.log", true));

			if (_instance == null)
				_instance = new DCompiler
				{
					Dmd = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.DMD),
					Gdc = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.GDC),
					Ldc = DCompilerConfiguration.CreateWithDefaults(DCompilerVendor.LDC)
				};
		}

		public void Save()
		{
			PropertyService.Set(GlobalPropertyName, this);
			PropertyService.SaveProperties();
		}

		const string GlobalPropertyName = "DBinding.DCompiler";
		#endregion
		
		public static string ExecutableExtension
		{
			get{ return OS.IsWindows?".exe":(OS.IsMac?".app":null);}	
		}
		public static string StaticLibraryExtension
		{
			get{ return OS.IsWindows?".lib":".a"; }
		}
		public static string SharedLibraryExtension
		{
			get{return OS.IsWindows?".dll":(OS.IsMac?".dylib":".so");}	
		}
		public static string ObjectExtension
		{
			get{return OS.IsWindows?".obj":".o";}	
		}

		public DCompilerVendor DefaultCompiler = DCompilerVendor.DMD;

		public static bool IsInitialized { get { return _instance != null; } }

		/// <summary>
		/// Static object which stores all global information about the dmd installation which probably exists on the programmer's machine.
		/// </summary>
		public DCompilerConfiguration Dmd = new DCompilerConfiguration { Vendor = DCompilerVendor.DMD };
		public DCompilerConfiguration Gdc = new DCompilerConfiguration { Vendor = DCompilerVendor.GDC };
		public DCompilerConfiguration Ldc = new DCompilerConfiguration { Vendor = DCompilerVendor.LDC };

		public IEnumerable<DCompilerConfiguration> Compilers
		{
			get { return new[] { Dmd,Gdc,Ldc }; }
		}

		public void UpdateParseCachesAsync()
		{
			foreach (var cmp in Compilers)
				cmp.UpdateParseCacheAsync();
		}

		/// <summary>
		/// Returns the default compiler configuration
		/// </summary>
		public DCompilerConfiguration GetDefaultCompiler()
		{
			return GetCompiler(DefaultCompiler);
		}

		public DCompilerConfiguration GetCompiler(DCompilerVendor type)
		{
			switch (type)
			{
				case DCompilerVendor.GDC:
					return Gdc;
				case DCompilerVendor.LDC:
					return Ldc;
			}

			return Dmd;
		}
		
		/// <summary>
		/// Compiles a D project.
		/// </summary>
		public static BuildResult Compile(
			DProject Project, 
			ProjectFileCollection FilesToCompile, 
			DProjectConfiguration BuildConfig, 
			IProgressMonitor monitor)
		{
			var relObjDir = "objs";
			var objDir = Path.Combine(Project.BaseDirectory, relObjDir);

			if(!Directory.Exists(objDir))
				Directory.CreateDirectory(objDir);

			// List of created object files
			var BuiltObjects = new List<string>();
			var compilerResults = new CompilerResults(new TempFileCollection());
			var buildResult = new BuildResult(compilerResults, "");
			bool succesfullyBuilt = true;
			bool modificationsDone = false;

			var Compiler = Project.Compiler;
			var Commands = Compiler.GetTargetConfiguration(Project.CompileTarget);
			var Arguments= Commands.GetArguments(BuildConfig.DebugMode);
			
			/// The target file to which all objects will be linked to
			var LinkTarget = BuildConfig.OutputDirectory.Combine(BuildConfig.CompiledOutputName);
			
			monitor.BeginTask("Build Project", FilesToCompile.Count + 1);

			var SourceIncludePaths=new List<string>(Compiler.GlobalParseCache.DirectoryPaths);
				SourceIncludePaths.AddRange(Project.LocalIncludeCache.DirectoryPaths);
			
			#region Compile sources to objects
			foreach (var f in FilesToCompile)
			{
				if (monitor.IsCancelRequested)
					return buildResult;

				// If not compilable, skip it
				if (f.BuildAction != BuildAction.Compile || !File.Exists(f.FilePath))
					continue;
				
				// a.Check if source file was modified and if object file still exists
				if (Project.LastModificationTimes.ContainsKey(f) &&
					Project.LastModificationTimes[f] == File.GetLastWriteTime(f.FilePath) &&
					File.Exists(f.LastGenOutput))
				{
					// File wasn't edited since last build
					// but add the built object to the objs array
					BuiltObjects.Add(f.LastGenOutput);
					monitor.Step(1);
					continue;
				}
				else
					modificationsDone=true;
				
				#region Resource file
				if(f.Name.EndsWith(".rc",StringComparison.OrdinalIgnoreCase))
				{
					var res = Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath))+ ".res";
					
					if(File.Exists(res))
						File.Delete(res);
					
					// Build argument string
					var resCmpArgs = FillInMacros(Win32ResourceCompiler.Instance.Arguments,
						new Win32ResourceCompiler.ArgProvider{
							RcFile=f.FilePath.ToString(), 
							ResFile=res 
						});
					
					// Execute compiler
					string output;
					int _exitCode = ExecuteCommand(Win32ResourceCompiler.Instance.Executable, 
						resCmpArgs, 
						Project.BaseDirectory, 
						monitor, 
						out output);
	
					// Error analysis
					if(!string.IsNullOrEmpty(output))
						compilerResults.Errors.Add(new CompilerError{ FileName=f.FilePath, ErrorText=output});
					CheckReturnCode(_exitCode, compilerResults);
	
					monitor.Step(1);
	
					if (_exitCode != 0)
					{
						buildResult.FailedBuildCount++;
						succesfullyBuilt = false;
						break;
					}
					else
					{
						f.LastGenOutput = res;
						buildResult.BuildCount++;
						Project.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);
	
						// Especially when compiling large projects, do only add the relative part of the r file due to command shortness
						if (res.StartsWith(Project.BaseDirectory))
							BuiltObjects.Add(res.Substring(Project.BaseDirectory.ToString().Length).TrimStart(Path.DirectorySeparatorChar));
						else
							BuiltObjects.Add(res);
					}
					
					continue;
				}
				#endregion

				// Create object file path
				var obj = Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath)) + ObjectExtension;
				
				if (File.Exists(obj))
					File.Delete(obj);

				// Prevent duplicates e.g. when having the samely-named source files in different sub-packages
				int i=2;
				while(File.Exists(obj))
				{
					// Simply add a number between the obj name and its extension
					obj= Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath))+i + ObjectExtension;
					i++;
				}
				
				// Create argument string for source file compilation.
				var dmdArgs = FillInMacros(Arguments.CompilerArguments + " " + BuildConfig.ExtraCompilerArguments,  new DCompilerMacroProvider 
				{ 
					IncludePathConcatPattern=Commands.IncludePathPattern,
					SourceFile = f.FilePath, 
					ObjectFile = obj,
					Includes=SourceIncludePaths,
				});			
				
				// b.Execute compiler
				string dmdOutput;
				int exitCode = ExecuteCommand(Commands.Compiler, dmdArgs, Project.BaseDirectory, monitor, out dmdOutput);

				ParseCompilerOutput(dmdOutput, compilerResults);
				CheckReturnCode(exitCode, compilerResults);

				monitor.Step(1);

				if (exitCode != 0)
				{
					buildResult.FailedBuildCount++;
					succesfullyBuilt = false;
					break;
				}
				else
				{
					f.LastGenOutput = obj;
					buildResult.BuildCount++;
					Project.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);

					// Especially when compiling large projects, do only add the relative part of the obj file due to command shortness
					if (obj.StartsWith(Project.BaseDirectory))
						BuiltObjects.Add(obj.Substring(Project.BaseDirectory.ToString().Length).TrimStart(Path.DirectorySeparatorChar));
					else
						BuiltObjects.Add(obj);
				}
			}
			#endregion

			#region Link files
			if (succesfullyBuilt)
			{
				// a.
				if (!modificationsDone)
				{
					// Only return if build target is still existing
					if (File.Exists(LinkTarget))
					{
						monitor.Step(1);
						return new BuildResult(compilerResults, "");
					}
				}

				// b.Build linker argument string
				
				// Build argument preparation
				/*				
				var libPaths=new List<string>(Compiler.DefaultLibPaths);
				libPaths.AddRange(Project.LibraryPaths);
				*/
				var libs=new List<string>(Compiler.DefaultLibraries);
				libs.AddRange(Project.ExtraLibraries);
				
				var linkArgs = FillInMacros(Arguments.LinkerArguments + " "+BuildConfig.ExtraLinkerArguments,
				    new DLinkerMacroProvider {
						ObjectsStringPattern=Commands.ObjectFileLinkPattern,
						Objects=BuiltObjects.ToArray(),
						TargetFile=LinkTarget,
						RelativeTargetDirectory=BuildConfig.OutputDirectory.ToRelative(Project.BaseDirectory),
						
						//LibraryPaths=libPaths,
						Libraries=libs
				});
				var linkerOutput = "";
				int exitCode = ExecuteCommand(Commands.Linker,linkArgs,Project.BaseDirectory,monitor,out linkerOutput);

				compilerResults.NativeCompilerReturnValue = exitCode;

				CheckReturnCode(exitCode, compilerResults);

				if (exitCode == 0)
				{
					monitor.ReportSuccess("Build successful!");
					monitor.Step(1);
				}
			}
			#endregion

			return new BuildResult(compilerResults,"");
		}

		#region Build argument creation

		/// <summary>
		/// Scans through RawArgumentString for macro uses (e.g. -of"$varname") and replace found variable matches with values provided by MacroProvider
		/// </summary>
		public static string FillInMacros(string RawArgumentString, IArgumentMacroProvider MacroProvider)
		{
			var returnArgString = RawArgumentString;

			string tempId = "";
			char c='\0';
			for (int i = RawArgumentString.Length - 1; i >= 0; i--)
			{
				c = RawArgumentString[i];

				if (char.IsLetterOrDigit(c) || c == '_')
					tempId = c+tempId;
				else if (c == '$' && tempId.Length>0)
				{
					var replacement = MacroProvider.Replace(tempId);

					//ISSUE: Replace undefined variables with nothing?
					if (replacement == tempId || replacement == null)
						replacement = "";

					returnArgString = returnArgString.Substring(0, i) + replacement + returnArgString.Substring(i + tempId.Length +1); // "+1" because of the initially skipped '$'

					tempId = "";
				}
				else 
					tempId = "";
			}

			return returnArgString;
		}

		#endregion

		/// <summary>
		/// Scans errorString line-wise for filename-line-message patterns (e.g. "myModule(1): Something's wrong here") and add these error locations to the CompilerResults cr.
		/// </summary>
		protected static void ParseCompilerOutput(string errorString, CompilerResults cr)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				var error = FindError(next, reader);
				if (error != null)
					cr.Errors.Add(error);
			}

			reader.Close();
		}

		#region Compiler Error Parsing
		private static Regex withColRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):(?<column>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		private static Regex noColRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);
		private static Regex linkerRegex = new Regex(
			@"^\s*(?<file>[^:]*):(?<line>\d*):\s*(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		//additional regex parsers
		private static Regex noColRegex_2 = new Regex(
			@"^\s*((?<file>.*)(\()(?<line>\d*)(\)):\s*(?<message>.*))|(Error:)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		private static Regex gcclinkerRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*):((?<column>\d*):)?\s*(?<level>.*)\s*:\s(?<message>.*)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		public static CompilerError FindError(string errorString, TextReader reader)
		{
			var error = new CompilerError();
			string warning = GettextCatalog.GetString("warning");
			string note = GettextCatalog.GetString("note");

			var match = withColRegex.Match(errorString);

			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				error.Column = int.Parse(match.Groups["column"].Value);
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				return error;
			}

			match = noColRegex.Match(errorString);

			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);
				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				// Skip messages that begin with ( and end with ), since they're generic.
				//Attempt to capture multi-line versions too.
				if (error.ErrorText.StartsWith("("))
				{
					string error_continued = error.ErrorText;
					do
					{
						if (error_continued.EndsWith(")"))
							return null;
					} while ((error_continued = reader.ReadLine()) != null);
				}

				return error;
			}

			match = noColRegex_2.Match(errorString);
			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);

				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;

				return error;
			}

			match = gcclinkerRegex.Match(errorString);
			if (match.Success)
			{
				error.FileName = match.Groups["file"].Value;
				error.Line = int.Parse(match.Groups["line"].Value);

				error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
								   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
				error.ErrorText = match.Groups["message"].Value;


				return error;
			}


			return null;
		}
		#endregion	

		/// <summary>
		/// Checks a compilation return code, 
		/// and adds an error result if the compiler results
		/// show no errors.
		/// </summary>
		/// <param name="returnCode">
		/// A <see cref="System.Int32"/>: A process return code
		/// </param>
		/// <param name="cr">
		/// A <see cref="CompilerResults"/>: The return code from a compilation run
		/// </param>
		static void CheckReturnCode(int returnCode, CompilerResults cr)
		{
			cr.NativeCompilerReturnValue = returnCode;
			if (0 != returnCode && 0 == cr.Errors.Count)
			{
				cr.Errors.Add(new CompilerError(string.Empty, 0, 0, string.Empty,
												  GettextCatalog.GetString("Build failed - check build output for details")));
			}
		}

		/// <summary>
		/// Executes a file and reports events related to the execution to the 'monitor' passed in the parameters.
		/// </summary>
		static int ExecuteCommand(string command, string args, string baseDirectory, IProgressMonitor monitor, out string errorOutput)
		{
			errorOutput = string.Empty;
			int exitCode = -1;

			var swError = new StringWriter();
			var chainedError = new LogTextWriter();
			chainedError.ChainWriter(monitor.Log);
			chainedError.ChainWriter(swError);

			monitor.Log.WriteLine("{0} {1}", command, args);

			var operationMonitor = new AggregatedOperationMonitor(monitor);

			try
			{
				var p = Runtime.ProcessService.StartProcess(command, args, baseDirectory, monitor.Log, chainedError, null);
				operationMonitor.AddOperation(p); //handles cancellation

				p.WaitForOutput();
				errorOutput = swError.ToString();
				exitCode = p.ExitCode;
				p.Dispose();

				if (monitor.IsCancelRequested)
				{
					monitor.Log.WriteLine(GettextCatalog.GetString("Build cancelled"));
					monitor.ReportError(GettextCatalog.GetString("Build cancelled"), null);
					if (exitCode == 0)
						exitCode = -1;
				}
			}
			finally
			{
				chainedError.Close();
				swError.Close();
				operationMonitor.Dispose();
			}

			return exitCode;
		}

		#region Loading & Saving
		public ICustomXmlSerializer ReadFrom(XmlReader x)
		{
			if (!x.Read())
				return this;

			while (x.Read())
			{
				switch (x.LocalName)
				{
					case "DefaultCompiler":
						if (x.MoveToAttribute("Name"))
							DefaultCompiler = (DCompilerVendor)Enum.Parse(typeof(DCompilerVendor), x.ReadContentAsString());
						break;

					case "Compiler":
						var vendor = DCompilerVendor.DMD;

						if (x.MoveToAttribute("Name"))
						{
							vendor = (DCompilerVendor)Enum.Parse(typeof(DCompilerVendor), x.ReadContentAsString());

							x.MoveToElement();
						}

						var cmp=GetCompiler(vendor);
						cmp.Vendor = vendor;

						cmp.ReadFrom(x.ReadSubtree());
						break;
					
					
				case "ResCmp":
					Win32ResourceCompiler.Instance.Load( x.ReadSubtree());
					break;
				}
			}

			return this;
		}

		public void WriteTo(XmlWriter x)
		{
			x.WriteStartElement("DefaultCompiler");
			x.WriteAttributeString("Name", DefaultCompiler.ToString());
			x.WriteEndElement();

			foreach (var cmp in Compilers)
			{
				x.WriteStartElement("Compiler");
				x.WriteAttributeString("Name", cmp.Vendor.ToString());

				cmp.SaveTo(x);

				x.WriteEndElement();
			}
			
			x.WriteStartElement("ResCmp");
			Win32ResourceCompiler.Instance.Save(x);
			x.WriteEndElement();
		}
		#endregion
	}
	
	public class Win32ResourceCompiler
	{
		public static Win32ResourceCompiler Instance = new Win32ResourceCompiler();

		public string Executable="rc.exe";
		public string Arguments=ResourceCompilerDefaultArguments;

		public const string ResourceCompilerDefaultArguments = "/fo \"$res\" \"$rc\"";
		
		public void Load(XmlReader x)
		{
			while(x.Read())
			{
				switch(x.LocalName)
				{
				case "exe":
					Executable=x.ReadString();
					break;
				
				case "args":
					Arguments=x.ReadString();
					break;
				}
			}
		}
		
		public void Save(XmlWriter x)
		{
			x.WriteStartElement("exe");
			x.WriteCData(Executable);
			x.WriteEndElement();
			
			x.WriteStartElement("args");
			x.WriteCData(Arguments);
			x.WriteEndElement();
		}
		
		public class ArgProvider:IArgumentMacroProvider
		{
			public string RcFile;
			public string ResFile;
			
			public string Replace (string Input)
			{
				switch(Input)
				{
				case "rc":
					return RcFile;
					
				case "res":
					return ResFile;
				}
				return null;
			}
		}
	}
	
	public class OS
	{
		public static bool IsWindows
		{
			get{return !IsMac && !IsLinux;}	
		}
		
		public static bool IsMac{
			get{ return Environment.OSVersion.Platform==PlatformID.MacOSX;}	
		}
		
		public static bool IsLinux{
			get{return Environment.OSVersion.Platform==PlatformID.Unix;}	
		}
	}
}
