using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.IO;
using MonoDevelop.Core.ProgressMonitoring;
using System.CodeDom.Compiler;


namespace MonoDevelop.D
{
	public class DMDCompiler
	{
		private DCompilerCommandBuilder compilerCommands;
		
		public BuildResult Compile(
			DProject prj, 
			ProjectFileCollection files, 
			DProjectConfiguration cfg, 
			IProgressMonitor monitor)
		{
			// Determine default object extension
			var objExt = ".obj";
			if (Environment.OSVersion.Platform == PlatformID.MacOSX ||
				Environment.OSVersion.Platform == PlatformID.Unix)
				objExt = ".o";

			var relObjDir = "objs";
			var objDir = Path.Combine(prj.BaseDirectory, relObjDir);

			if(!Directory.Exists(objDir))
				Directory.CreateDirectory(objDir);

			/*
			 * 1) Compile all D sources
			 *	a. Check if modified
			 *	b. If so, compile
			 * 2) Link them
			 *	a. Check if there were modifications made
			 *	b. If so, link
			 */

			// List of created object files
			var objs = new List<string>();
			var compilerResults = new CompilerResults(new TempFileCollection());
			var buildResult = new BuildResult(compilerResults, "");
			bool succesfullyBuilt = true;
			bool modificationsDone = false;
			
			switch(cfg.Compiler)
			{
				case DCompiler.GDC:
					compilerCommands = new GDCCompilerCommandBuilder(prj, cfg);
					break;
				case DCompiler.LDC:	
					compilerCommands = new LDCCompilerCommandBuilder(prj, cfg);
					break;
				default:
					compilerCommands = new DMDCompilerCommandBuilder(prj, cfg);
					break;
			}
				
			monitor.BeginTask("Build Project", files.Count + 1);

			// 1)
			foreach (var f in files)
			{
				if (monitor.IsCancelRequested)
					return buildResult;

				// If not compilable, skip it
				if (f.BuildAction != BuildAction.Compile || !File.Exists(f.FilePath))
					continue;

				// Create object file path
				var obj = Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath)) + objExt;
				
				// a.Check if source file was modified and if object file still exists
				if (prj.LastModificationTimes.ContainsKey(f) &&
					prj.LastModificationTimes[f] == File.GetLastWriteTime(f.FilePath) &&
					File.Exists(f.LastGenOutput))
				{
					// File wasn't edited since last build
					// but add the built object to the objs array
					objs.Add(f.LastGenOutput);
					monitor.Step(1);
					continue;
				}
				// If source was modified and if obj file is existing, delete it
				else
				{
					modificationsDone = true;
					if (File.Exists(obj))
						File.Delete(obj);
				}

				// Prevent duplicates e.g. when having the samely-named source files in different sub-packages
				int i=2;
				while(File.Exists(obj))
				{
					// Simply add a number between the obj name and its extension
					obj= Path.Combine(objDir, Path.GetFileNameWithoutExtension(f.FilePath))+i + objExt;
					i++;
				}
				
			
				var dmdArgs = compilerCommands.BuildCompilerArguments(f.FilePath, obj);			
				
				// b.Execute compiler
				string dmdOutput;
				int exitCode = ExecuteCommand(compilerCommands.CompilerCommand, dmdArgs, prj.BaseDirectory, monitor, out dmdOutput);

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
					prj.LastModificationTimes[f] = File.GetLastWriteTime(f.FilePath);
					objs.Add(obj);
				}
			}

			// 2)
			if (succesfullyBuilt)
			{
				// a.
				if (!modificationsDone)
				{
					// Only return if build target is still existing
					if (File.Exists(cfg.CompiledOutputName))
					{
						monitor.Step(1);
						return new BuildResult(compilerResults, "");
					}
				}

				// b.Build linker argument string
				var linkArgs = compilerCommands.BuildLinkerArguments(objs);
				var linkerOutput = "";
				int exitCode = ExecuteCommand(compilerCommands.LinkerCommand,linkArgs,prj.BaseDirectory,monitor,out linkerOutput);

				compilerResults.NativeCompilerReturnValue = exitCode;

				CheckReturnCode(exitCode, compilerResults);

				if (exitCode == 0)
				{
					monitor.ReportSuccess("Build successful!");
					monitor.Step(1);
				}
			}

			return new BuildResult(compilerResults,"");
		}
	
		
		CompilerError CreateErrorFromErrorString(string errorString, TextReader reader)
		{
			return compilerCommands.FindError(errorString, reader);
		}

		protected void ParseCompilerOutput(string errorString, CompilerResults cr)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				CompilerError error = CreateErrorFromErrorString(next, reader);
				if (error != null)
					cr.Errors.Add(error);
			}

			reader.Close();
		}

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


		int ExecuteCommand(string command, string args, string baseDirectory, IProgressMonitor monitor, out string errorOutput)
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
	}
}
