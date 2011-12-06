
using System;
using System.Collections.Generic;
using System.Globalization;
using Mono.Debugging.Client;
using Mono.Debugging.Backend;

using DEW = DebugEngineWrapper;

namespace MonoDevelop.Debugger.DDebugger
{
	
	
	class DDebugBacktrace: IBacktrace, IObjectValueSource
	{
		int fcount;
        StackFrame firstFrame;
		DDebugSession session;
		DissassemblyBuffer[] disBuffers;
		int currentFrame = -1;
		long threadId;
        DEW.DBGEngine Engine;

        public DDebugBacktrace(DDebugSession session, long threadId, DEW.DBGEngine engine)
		{
			this.session = session;
            this.Engine = engine;
			fcount = engine.CallStack.Length;
			this.threadId = threadId;
			if (firstFrame != null)
				this.firstFrame = CreateFrame(Engine.CallStack[0]);
		}
		
		public int FrameCount {
			get {
				return fcount;
			}
		}
		
		public StackFrame[] GetStackFrames (int firstIndex, int lastIndex)
		{
            //StackFrame frm = new StackFrame(
           //Engine.CallStack[0].
                        

			List<StackFrame> frames = new List<StackFrame> ();
			if (firstIndex == 0 && firstFrame != null) {
				frames.Add (firstFrame);
				firstIndex++;
			}
			
			if (lastIndex >= fcount)
				lastIndex = fcount - 1;
			
			if (firstIndex > lastIndex)
				return frames.ToArray ();
			
			session.SelectThread (threadId);
            //DDebugCommandResult res = session.RunCommand("-stack-list-frames", firstIndex.ToString(), lastIndex.ToString());
			//ResultData stack = res.GetObject ("stack");



			for (int n=0; n< Engine.CallStack.Length; n++) {
				//ResultData frd = stack.GetObject (n);
                frames.Add(CreateFrame(Engine.CallStack[n]));
			}
			return frames.ToArray ();
		}

		public ObjectValue[] GetLocalVariables (int frameIndex, EvaluationOptions options)
		{
			List<ObjectValue> values = new List<ObjectValue> ();
            if (Engine.Symbols.ScopeLocalSymbols == null)
                return values.ToArray();

            for (uint i = 0; i < Engine.Symbols.ScopeLocalSymbols.Count; i++)
            {
                string name = Engine.Symbols.ScopeLocalSymbols.Symbols[i].Name;
                string typename = Engine.Symbols.ScopeLocalSymbols.Symbols[i].TypeName;
                string val = Engine.Symbols.ScopeLocalSymbols.Symbols[i].TextValue;

                ObjectValueFlags flags = ObjectValueFlags.Variable;
                ObjectValue ov = ObjectValue.CreatePrimitive(this, new ObjectPath(name), typename, new EvaluationResult(val), flags);

                values.Add(ov);
            }
            return values.ToArray();

            //Engine.CallStack[0]
            //Engine
            //ScopeLocalSymbols

            /*
            List<ObjectValue> values = new List<ObjectValue> ();
            SelectFrame (frameIndex);
            DDebugCommandResult res = session.RunCommand("-stack-list-locals", "0");
            foreach (ResultData data in res.GetObject ("locals"))
                values.Add (CreateVarObject (data.GetValue ("name")));
			
            return values.ToArray ();
            */


        }

        public ObjectValue[] GetParameters(int frameIndex, EvaluationOptions options)
		{
			List<ObjectValue> values = new List<ObjectValue> ();
			return values.ToArray();
			
			
			SelectFrame (frameIndex);
			
			return values.ToArray ();
		}

		public ObjectValue GetThisReference (int frameIndex, EvaluationOptions options)
		{
			return null;
		}
		
		public ObjectValue[] GetAllLocals (int frameIndex, EvaluationOptions options)
		{
			List<ObjectValue> locals = new List<ObjectValue> ();
			/*
            locals.AddRange (GetParameters (frameIndex, options));
            */
			locals.AddRange (GetLocalVariables (frameIndex, options));
			return locals.ToArray ();
		}

		public ObjectValue[] GetExpressionValues (int frameIndex, string[] expressions, EvaluationOptions options)
		{
			List<ObjectValue> values = new List<ObjectValue> ();


			SelectFrame (frameIndex);
			foreach (string exp in expressions)
				values.Add (CreateVarObject (exp));
			return values.ToArray ();
		}
		
		public ExceptionInfo GetException (int frameIndex, EvaluationOptions options)
		{
			return null;
		}
		
		public ValidationResult ValidateExpression (int frameIndex, string expression, EvaluationOptions options)
		{
			return new ValidationResult (true, null);
		}
		
		public CompletionData GetExpressionCompletionData (int frameIndex, string exp)
		{
			SelectFrame (frameIndex);
			
			return null;
		}

		
		ObjectValue CreateVarObject (string exp)
		{
			try {
				session.SelectThread (threadId);

                DebugEngineWrapper.DebugSymbolData[] datasymbols = Engine.Symbols.GetSymbols(exp);

                for (uint i = 0; i < Engine.Symbols.ScopeLocalSymbols.Count; i++)
                {
                    if (exp == Engine.Symbols.ScopeLocalSymbols.Symbols[i].Name)
                    {

                        session.RegisterTempVariableObject(exp);
                        return CreateObjectValue(exp, Engine.Symbols.ScopeLocalSymbols.Symbols[i]); 
                    }
                }

                return ObjectValue.CreateUnknown(exp);
			} catch {
				return ObjectValue.CreateUnknown (exp);
			}
		}

		ObjectValue CreateObjectValue (string name, DebugEngineWrapper.DebugScopedSymbol symbol)
		{

            

            string vname = symbol.Name;
            string typeName = symbol.TypeName;
            string value = symbol.TextValue;
            int nchild = (int) symbol.ChildrenCount;
			
			ObjectValue val;
			ObjectValueFlags flags = ObjectValueFlags.Variable;
			
			// There can be 'public' et al children for C++ structures
			if (typeName == null)
				typeName = "none";

            val = ObjectValue.CreatePrimitive(this, new ObjectPath(vname), typeName, new EvaluationResult(value), flags);
            val.Name = name;
            return val;

            /*
			if (typeName.EndsWith ("]")) {
				val = ObjectValue.CreateArray (this, new ObjectPath (vname), typeName, nchild, flags, null);
			} else if (value == "{...}" || typeName.EndsWith ("*") || nchild > 0) {
				val = ObjectValue.CreateObject (this, new ObjectPath (vname), typeName, value, flags, null);
			} else {
				val = ObjectValue.CreatePrimitive (this, new ObjectPath (vname), typeName, new EvaluationResult (value), flags);
			}
			val.Name = name;
            */
             
			return val;
		}

		public ObjectValue[] GetChildren (ObjectPath path, int index, int count, EvaluationOptions options)
		{
			List<ObjectValue> children = new List<ObjectValue> ();
			session.SelectThread (threadId);
            return children.ToArray();

            /*
            DDebugCommandResult res = session.RunCommand("-var-list-children", "2", path.Join("."));
			ResultData cdata = res.GetObject ("children");
			
			// The response may not contain the "children" list at all.
			if (cdata == null)
				return children.ToArray ();
			
			if (index == -1) {
				index = 0;
				count = cdata.Count;
			}
			
			for (int n=index; n<cdata.Count && n<index+count; n++) {
				ResultData data = cdata.GetObject (n);
				ResultData child = data.GetObject ("child");
				
				string name = child.GetValue ("exp");
				if (name.Length > 0 && char.IsNumber (name [0]))
					name = "[" + name + "]";
				
				// C++ structures may contain typeless children named
				// "public", "private" and "protected".
				if (child.GetValue("type") == null) {
					ObjectPath childPath = new ObjectPath (child.GetValue ("name").Split ('.'));
					ObjectValue[] subchildren = GetChildren (childPath, -1, -1, options);
					children.AddRange(subchildren);
				} else {
					ObjectValue val = CreateObjectValue (name, child);
					children.Add (val);
				}
			}
			return children.ToArray ();
             */
		}
		
		public EvaluationResult SetValue (ObjectPath path, string value, EvaluationOptions options)
		{
			session.SelectThread (threadId);
			
			return new EvaluationResult (value);
		}
		
		public ObjectValue GetValue (ObjectPath path, EvaluationOptions options)
		{
			throw new NotSupportedException ();
		}
		
		void SelectFrame (int frame)
		{
			session.SelectThread (threadId);

            /*
			if (frame != currentFrame) {
				session.RunCommand ("-stack-select-frame", frame.ToString ());
				currentFrame = frame;
			}
             */
		}

        StackFrame CreateFrame(DEW.StackFrame frameData)
		{

            string fn;
            uint ln;
            ulong off = frameData.InstructionOffset;
            Engine.Symbols.GetLineByOffset(off, out fn, out ln);

            /*
			SourceLocation loc = new SourceLocation (func ?? "?", sfile, line);
			
			long addr;
			if (!string.IsNullOrEmpty (sadr))
				addr = long.Parse (sadr.Substring (2), NumberStyles.HexNumber);
			else
				addr = 0;
			*/

            string methodName = Engine.Symbols.GetNameByOffset(off);
            SourceLocation loc = new SourceLocation(methodName, fn, (int)ln);

            return new StackFrame((long)off, loc, "Native");

            /*
			string lang = "Native";
			string func = frameData.GetValue ("func");
			string sadr = frameData.GetValue ("addr");
			
			if (func == "??" && session.IsMonoProcess) {
				// Try to get the managed func name
				try {
					ResultData data = session.RunCommand ("-data-evaluate-expression", "mono_pmip(" + sadr + ")");
					string val = data.GetValue ("value");
					if (val != null) {
						int i = val.IndexOf ('"');
						if (i != -1) {
							func = val.Substring (i).Trim ('"',' ');
							lang = "Mono";
						}
					}
				} catch {
				}
			}

			int line = -1;
			string sline = frameData.GetValue ("line");
			if (sline != null)
				line = int.Parse (sline);
			
			string sfile = frameData.GetValue ("fullname");
			if (sfile == null)
				sfile = frameData.GetValue ("file");
			if (sfile == null)
				sfile = frameData.GetValue ("from");
			SourceLocation loc = new SourceLocation (func ?? "?", sfile, line);
			
			long addr;
			if (!string.IsNullOrEmpty (sadr))
				addr = long.Parse (sadr.Substring (2), NumberStyles.HexNumber);
			else
				addr = 0;
			
			return new StackFrame (addr, loc, lang);
             */
		}

		public AssemblyLine[] Disassemble (int frameIndex, int firstLine, int count)
		{
			SelectFrame (frameIndex);
            return null;
		}
		
		public object GetRawValue (ObjectPath path, EvaluationOptions options)
		{
			return null;
		}
		
		public void SetRawValue (ObjectPath path, object value, EvaluationOptions options)
		{
		}
	}

}
