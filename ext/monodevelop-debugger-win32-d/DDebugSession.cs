
using System;
using System.Globalization;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Mono.Debugging.Client;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
//using Mono.Unix.Native;
using DebugEngineWrapper;

namespace MonoDevelop.Debugger.DDebugger
{
	class BreakPointWrapper
	{
		public BreakEventInfo EventInfo { get; set; }
		public DebugEngineWrapper.BreakPoint Breakpoint {get; set;}
		public BreakPointWrapper(BreakEventInfo eventInfo, DebugEngineWrapper.BreakPoint breakpoint)
		{
			EventInfo = eventInfo;
			Breakpoint = breakpoint;
		}
	}
	
	class DDebugSession: DebuggerSession
	{
        DBGEngine Engine;
        bool IsDebugging;
        bool EngineStarting;
		bool StopWaitingForEvents = false;        

		IProcessAsyncOperation console;

		long currentThread = -1;
		long activeThread = -1;

		long targetProcessId = 0;
		List<string> tempVariableObjects = new List<string> ();
		Dictionary<ulong,BreakPointWrapper> breakpoints = new Dictionary<ulong,BreakPointWrapper> ();
		List<BreakEventInfo> breakpointsWithHitCount = new List<BreakEventInfo> ();
		
		DateTime lastBreakEventUpdate = DateTime.Now;
		Dictionary<int, WaitCallback> breakUpdates = new Dictionary<int,WaitCallback> ();
		bool breakUpdateEventsQueued;
		const int BreakEventUpdateNotifyDelay = 500;

		bool logGdb;
			
		object syncLock = new object ();
		object eventLock = new object ();

		
		ulong debugeeOffSet;
		
		public DDebugSession ()
		{
			logGdb = !string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("MONODEVELOP_GDB_LOG"));
            Engine = new DBGEngine();

				Engine.Output += delegate(OutputFlags type, string msg)
				{
				/*if (!GlobalProperties.Instance.VerboseDebugOutput && (type == OutputFlags.Verbose || type == OutputFlags.Normal)) return;

					var ErrType=ErrorType.Message;
					if (type == OutputFlags.Warning)
						return;
					if (type == OutputFlags.Error)
						ErrType = ErrorType.Error;
					Log(msg.Replace("\n",string.Empty),ErrType);*/
				};

				Engine.OnLoadModule += delegate(ulong BaseOffset, uint ModuleSize, string ModuleName, uint Checksum, uint Timestamp)
				{
					if (EngineStarting)
						return DebugStatus.Break;
					return DebugStatus.NoChange;
				};

				Engine.OnCreateProcess += delegate(ulong BaseOffset, uint ModuleSize, string ModuleName, uint Checksum, uint TimeStamp)
				{
					targetProcessId = Engine.GetTargetProcessId();
					debugeeOffSet = BaseOffset;
				
					return DebugStatus.NoChange;				
				};
									
				Engine.OnBreakPoint += delegate(uint Id, string cmd, ulong off, string exp)
				{
                    FireBreakPoint(off);
                    StopWaitingForEvents = true;
                    return DebugStatus.Break;

                };

				Engine.OnException += delegate(CodeException ex)
				{
					StopWaitingForEvents = true;

					return DebugStatus.Break;
				};

				Engine.OnExitProcess += delegate(uint code)
				{
					Exit();
					return DebugStatus.NoChange;
				};
            
            
			Engine.Execute("n 10"); // Set decimal numbers
			Engine.Execute(".lines -e"); // Enable source code locating            
		}
		
		protected override void OnRun (DebuggerStartInfo startInfo)
		{
            targetProcessId = 0;
            RunCv2Pdb(startInfo.Command);
            
            StartDebuggerSession(startInfo, 0);

			OnStarted ();
				
            WaitForDebugEvent(); // Wait for the first breakpoint/exception/program exit to occur

		}
		
		protected override void OnAttachToProcess (long processId)
		{
            //ToDo implement:
            targetProcessId = processId;
		}
		
        private void RunCv2Pdb(string target)
        {
            try
            {
                Process.Start("cv2pdb.exe", target).WaitForExit(30000);
            }
            catch (Exception ex)
            {
                throw new Exception("Error running cv2pdb.exe on target: " + target + 
                                    "\r\nPlease ensure that path to cv2pdb is registered in the 'PATH' environment variable" + 
                                    "\r\nDetails:\r\n" + ex.Message);
            }
        }

        void StartDebuggerSession(DebuggerStartInfo startInfo, long attachToProcessId)
		{
			IsDebugging = true;
			EngineStarting = true;

            bool showConsole = false;

			DebugCreateProcessOptions opt = new DebugCreateProcessOptions();
            if (attachToProcessId == 0)
            {
                opt.CreateFlags = CreateFlags.DebugOnlyThisProcess | (showConsole ? CreateFlags.CreateNewConsole : 0);
                opt.EngCreateFlags = EngCreateFlags.Default;
            }

			
			if (attachToProcessId != 0)
				Engine.CreateProcessAndAttach(0, "", opt, Path.GetDirectoryName(startInfo.Command), "", (uint)attachToProcessId, 0);
			else			
				Engine.CreateProcessAndAttach(0, startInfo.Command + (string.IsNullOrWhiteSpace(startInfo.Arguments) ? "" : (" " + startInfo.Arguments)), opt, Path.GetDirectoryName(startInfo.Command), "", 0, 0);           

            //ToDo: figure out how to pass the symbol path
            Engine.Symbols.SourcePath = (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)) ? Path.GetDirectoryName(startInfo.Command) : startInfo.WorkingDirectory;
			Engine.IsSourceCodeOrientedStepping = true;
			
			Engine.WaitForEvent();					
			Engine.Execute("bc"); // Clear breakpoint list
			Engine.WaitForEvent();

			
			foreach (Breakpoint bp in Breakpoints)
			{			
				ulong off = 0;
				if (!Engine.Symbols.GetOffsetByLine(bp.FileName, (uint)bp.Line, out off))
					continue;

				Engine.AddBreakPoint(BreakPointOptions.Enabled).Offset = off;
				
				//bp.Breakpoint = DebugManagement.Engine.AddBreakPoint(BreakPointOptions.Enabled);
				//bp.Breakpoint.Offset = off;								
			}			

			EngineStarting = false;
		}

		public void GotoCurrentLocation()
		{
            if (!IsDebugging || StopWaitingForEvents) return;

           ulong off = Engine.CurrentFrame.InstructionOffset;
           FireBreakPoint(off);
           StopWaitingForEvents = true;

		}
        
        
        void WaitForDebugEvent()
        {
            if (!IsDebugging) return;

            //Log(IDEManager.Instance.MainWindow.LeftStatusText = "Waiting for the program to interrupt...", ErrorType.Information);
            var wr = WaitResult.OK;
            while (IsDebugging && (wr = Engine.WaitForEvent(10)) == WaitResult.TimeOut)
            {
                if (wr == WaitResult.Unexpected)
                    break;
                System.Windows.Forms.Application.DoEvents();
            }
            if (wr != WaitResult.Unexpected)
            {
                //Log(IDEManager.Instance.MainWindow.LeftStatusText = "Program execution halted...", ErrorType.Information);
            }
            /*
             * After a program paused its execution, we'll be able to access its breakpoints and symbol data.
             * When resuming the program, WaitForDebugEvent() will be called again.
             * Note that it's not possible to 'wait' on a different thread.
             */


            //UpdateDebuggingPanels();
            //RefreshAllDebugMarkers();
        }

		public override void Dispose ()
		{
			if (console != null && !console.IsCompleted) {
				console.Cancel ();
				console = null;
			}
				
		}
		
		protected override void OnSetActiveThread (long processId, long threadId)
		{
			activeThread = threadId;
		}
		
		protected override void OnStop ()
		{
			
			if (IsDebugging)
			{
				//IDEManager.Instance.MainWindow.LeftStatusText = "Terminate debugger";

				IsDebugging = false;
				Engine.EndPendingWaits();
				Engine.Terminate();
				//Engine.MainProcess.Kill();
				
				/*Instance.MainWindow.RefreshMenu();
				UpdateDebuggingPanels();
				RefreshAllDebugMarkers();*/
			}

			//EditingManagement.AllDocumentsReadOnly = false;						
		}
		
		protected override void OnDetach ()
		{
            TargetEventArgs args = new TargetEventArgs(TargetEventType.TargetExited);
            OnTargetEvent(args);           
		}
		
		protected override void OnExit ()
		{
            OnStop();

            TargetEventArgs args = new TargetEventArgs(TargetEventType.TargetExited);
            OnTargetEvent(args);
			
		}

		protected override void OnStepLine ()//step into
		{
			if (!IsDebugging) return;
			Engine.Execute("t"); // Trace
			WaitForDebugEvent();
			StopWaitingForEvents = false;
			GotoCurrentLocation();
		}

		protected override void OnNextLine ()//step over
		{
			if (!IsDebugging) return;
			Engine.Execute("p"); // Step
			WaitForDebugEvent();
			StopWaitingForEvents = false;
			GotoCurrentLocation();
		}

        protected override void OnFinish() //step out
        {
            if (!IsDebugging) return;
            Engine.Execute("pt"); // Halt on next return
            WaitForDebugEvent();
            StopWaitingForEvents = false;
            GotoCurrentLocation();
            
        }
      

		protected override void OnStepInstruction () //what is this for?
		{
			if (!IsDebugging) return;
			Engine.Execute("p"); // Step
			WaitForDebugEvent();
			StopWaitingForEvents = false;
			GotoCurrentLocation();


		}

        protected override void OnNextInstruction()  //what is this for?
		{
			if (!IsDebugging) return;
			Engine.Execute("pt"); // Halt on next return
			WaitForDebugEvent();
			StopWaitingForEvents = false;
			GotoCurrentLocation();
		}
			
        void FireBreakPoint(ulong offset)
        {
                TargetEventArgs args = new TargetEventArgs(TargetEventType.TargetHitBreakpoint);

                ulong tempoff = (ulong)offset;
                if (breakpoints.ContainsKey(tempoff))
                {
                    breakpoints[(ulong)tempoff].EventInfo.UpdateHitCount((int)breakpoints[(ulong)tempoff].Breakpoint.HitCount);
                    args.BreakEvent = breakpoints[(ulong)tempoff].EventInfo.BreakEvent;
                }
                else
                {
                    args = new TargetEventArgs(TargetEventType.TargetStopped);
                    BreakEventInfo breakInfo = new BreakEventInfo();
                    breakInfo.Handle = tempoff;
					breakInfo.SetStatus (BreakEventStatus.Bound, null);
                    string fn;
                    uint ln;
                    if (Engine.Symbols.GetLineByOffset(offset, out fn, out ln))
                    {
                        //breakInfo.BreakEvent = new Breakpoint(fn, (int)ln);
                        args.BreakEvent = breakInfo.BreakEvent;
                    }
                }

                ProcessInfo process = OnGetProcesses()[0];
                args.Process = new ProcessInfo(process.Id, process.Name);

                args.Backtrace = new Backtrace(new DDebugBacktrace(this, activeThread, Engine));

                ThreadPool.QueueUserWorkItem(delegate(object data)
                {
                    try
                    {
                	    OnTargetEvent((TargetEventArgs)data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, args);
        }
		
		void NotifyBreakEventUpdate (BreakEventInfo binfo, int hitCount, string lastTrace)
		{
			bool notify = false;
			
			WaitCallback nc = delegate {
				if (hitCount != -1)
					binfo.UpdateHitCount (hitCount);
				if (lastTrace != null)
					binfo.UpdateLastTraceValue (lastTrace);
			};
			
			lock (breakUpdates)
			{
				int span = (int) (DateTime.Now - lastBreakEventUpdate).TotalMilliseconds;
				if (span >= BreakEventUpdateNotifyDelay && !breakUpdateEventsQueued) {
					// Last update was more than 0.5s ago. The update can be sent.
					lastBreakEventUpdate = DateTime.Now;
					notify = true;
				} else {
					// Queue the event notifications to avoid wasting too much time
					breakUpdates [(int)binfo.Handle] = nc;
					if (!breakUpdateEventsQueued) {
						breakUpdateEventsQueued = true;
						
						ThreadPool.QueueUserWorkItem (delegate {
							Thread.Sleep (BreakEventUpdateNotifyDelay - span);
							List<WaitCallback> copy;
							lock (breakUpdates) {
								copy = new List<WaitCallback> (breakUpdates.Values);
								breakUpdates.Clear ();
								breakUpdateEventsQueued = false;
								lastBreakEventUpdate = DateTime.Now;
							}
							foreach (WaitCallback wc in copy)
								wc (null);
						});
					}
				}
			}
			if (notify)
				nc (null);
		}
		
		protected override BreakEventInfo OnInsertBreakEvent (BreakEvent be)
		{
			Breakpoint bp = be as Breakpoint;
			if (bp == null)
				throw new NotSupportedException ();
			
			BreakEventInfo breakEventInfo = new BreakEventInfo ();
			

			//bool dres = InternalStop ();
			try {
				string extraCmd = string.Empty;
				if (bp.HitCount > 0) {
					extraCmd += "-i " + bp.HitCount;
					breakpointsWithHitCount.Add (breakEventInfo);
				}
				if (!string.IsNullOrEmpty (bp.ConditionExpression)) {
					if (!bp.BreakIfConditionChanges)
						extraCmd += " -c " + bp.ConditionExpression;
				}

                ulong bh = 0;
                DebugEngineWrapper.BreakPoint engineBreakPoint = null;
                ulong off = 0;
                if (Engine.Symbols.GetOffsetByLine(bp.FileName, (uint)bp.Line, out off))
                {
                    engineBreakPoint = Engine.AddBreakPoint(BreakPointOptions.Enabled);
                    engineBreakPoint.Offset = off;

                    bh = engineBreakPoint.Offset;
                    breakpoints[bh] = new BreakPointWrapper(breakEventInfo, engineBreakPoint);
                    breakEventInfo.Handle = bh;
                    breakEventInfo.SetStatus(BreakEventStatus.Bound, null);

                    //if (!be.Enabled)
                    //ToDo: tell debugger engine that breakpoint is disabled

                }
                else
                {
                    breakEventInfo.SetStatus(BreakEventStatus.BindError, null);
                }





				return breakEventInfo;
			} finally {
				//InternalResume (dres);
			}

		}
		
		
		protected override void OnRemoveBreakEvent (BreakEventInfo binfo)
		{
				
			if (binfo == null)
				return;

			breakpointsWithHitCount.Remove (binfo);
			
			DebugEngineWrapper.BreakPoint breakpoint = 	breakpoints[(ulong)binfo.Handle].Breakpoint;
			
			if (IsDebugging /*&& bpw.IsExisting*/)
				Engine.RemoveBreakPoint(breakpoint);
			
			breakpointsWithHitCount.Remove (binfo);
			breakpoints.Remove((ulong)binfo.Handle);

		}
		
		protected override void OnEnableBreakEvent (BreakEventInfo binfo, bool enable)
		{
			if (binfo.Handle == null)
				return;

			breakpoints[(ulong)binfo.Handle].Breakpoint.Flags =  enable? BreakPointOptions.Enabled : BreakPointOptions.Deferred;
			
            //ToDo: tell engine we enabled a break point
		}
		
		protected override void OnUpdateBreakEvent (BreakEventInfo binfo)
		{
			return;
			
		}

		protected override void OnContinue ()
		{
			if (!IsDebugging)	return;
			Engine.Execute("gh");
			WaitForDebugEvent();
		}
		
		protected override ThreadInfo[] OnGetThreads (long processId)
		{
            Process process = Process.GetProcessById((int)processId);            
			List<ThreadInfo> list = new List<ThreadInfo> ();

            foreach(ProcessThread thread in process.Threads){
                list.Add(new ThreadInfo(processId, thread.Id, "Thread #" + thread.Id.ToString(), ""));
            }
            return list.ToArray();            
		}
		
		protected override ProcessInfo[] OnGetProcesses ()
		{				
            Process process = Process.GetProcessById((int)targetProcessId);
            return new ProcessInfo[] {new ProcessInfo(process.Id, process.ProcessName)} ;
		}
		
		ThreadInfo GetThread (long id)
		{
			return new ThreadInfo (0, id, "Thread #" + id, null);
		}
		
		protected override Backtrace OnGetThreadBacktrace (long processId, long threadId)
		{
            return new Backtrace(new DDebugBacktrace(this, threadId, Engine));            
		}
		
		protected override AssemblyLine[] OnDisassembleFile (string file)
		{
            return null;
		}
		
		public void SelectThread (long id)
		{
            if (id == currentThread)
                return;
			currentThread = id;
            //ToDo: select thread on engine wrapper
		}
		
		string Escape (string str)
		{
			if (str == null)
				return null;
			else if (str.IndexOf (' ') != -1 || str.IndexOf ('"') != -1) {
				str = str.Replace ("\"", "\\\"");
				return "\"" + str + "\"";
			}
			else
				return str;
		}
		

		internal void RegisterTempVariableObject (string var)
		{
			tempVariableObjects.Add (var);
		}
		
		void CleanTempVariableObjects ()
		{
            //ToDo: remove temp variables
			tempVariableObjects.Clear ();
		}
	}
}
