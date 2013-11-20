using MonoDevelop.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDevelop.D.Building;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core.ProgressMonitoring;

namespace MonoDevelop.D.Projects.Dub
{
	public class DubBuilder
	{
		public static readonly DubBuilder Instance = new DubBuilder();

		public void BuildCommonArgAppendix(StringBuilder sr,DubProject prj, ConfigurationSelector sel)
		{
			if (prj.Configurations.Count > 1 && sel.GetConfiguration(prj).Id != "Default")
				sr.Append(" --config=").Append(sel.GetConfiguration(prj).Id);
		}

		public void BuildProgramArgAppendix(StringBuilder sr, DubProject prj, DubProjectConfiguration cfg)
		{

		}

		public static BuildResult BuildProject(DubProject prj, IProgressMonitor mon, ConfigurationSelector sel)
		{
			var br = new BuildResult();

			var args = new StringBuilder("build");

			Instance.BuildCommonArgAppendix(args, prj, sel);

			string output;
			string errDump;

			int status = ProjectBuilder.ExecuteCommand(DubSettings.Instance.DubCommand, args.ToString(), prj.BaseDirectory, 
				mon, out errDump, out output);
			br.CompilerOutput = output;

			ErrorExtracting.HandleReturnCode (mon, br, status);
			ErrorExtracting.HandleCompilerOutput(prj, br, output);
			ErrorExtracting.HandleCompilerOutput(prj, br, errDump);

			return br;
		}

		internal static void ExecuteProject(DubProject prj,IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			bool isDebug = context.ExecutionHandler.GetType ().Name.StartsWith ("Debug");

			var conf = prj.GetConfiguration(configuration) as DubProjectConfiguration;
			IConsole console;
			if (conf.ExternalConsole)
				console = context.ExternalConsoleFactory.CreateConsole(!conf.PauseConsoleOutput);
			else
				console = context.ConsoleFactory.CreateConsole(true);
			
			var operationMonitor = new AggregatedOperationMonitor(monitor);

			var sr = new StringBuilder();
			if (!isDebug) {
				sr.Append ("run");
				Instance.BuildCommonArgAppendix (sr, prj, configuration);
			}

			try
			{
				var cmd = isDebug ? prj.CreateExecutionCommand(configuration) : new NativeExecutionCommand(DubSettings.Instance.DubCommand, sr.ToString(), prj.BaseDirectory.ToString());
				if (!context.ExecutionHandler.CanExecute(cmd))
				{
					monitor.ReportError("Cannot execute \"" + cmd.Command + " " + cmd.Arguments + "\". The selected execution mode is not supported for Dub projects.", null);
					return;
				}

				var op = context.ExecutionHandler.Execute(cmd, console);

				operationMonitor.AddOperation(op);
				op.WaitForCompleted();

				if(op.ExitCode != 0)
					monitor.ReportError(cmd.Command+" exited with code: "+op.ExitCode.ToString(), null);
				else
					monitor.Log.WriteLine(cmd.Command+" exited with code: {0}", op.ExitCode);
			}
			catch (Exception ex)
			{
				monitor.ReportError("Cannot execute \"" + sr.ToString() + "\"", ex);
			}
			finally
			{
				operationMonitor.Dispose();
				console.Dispose();
			}
		}
	}
}
