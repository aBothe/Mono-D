using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Debugging.Client;
using Mono.Debugging.Backend;

namespace MonoDevelop.D.Debugging
{
	public class DLocalExamBacktrace : IObjectValueSource
	{
		public readonly IDBacktraceHelpers BacktraceHelper;

		public DLocalExamBacktrace(IDBacktraceHelpers helper)
		{
			this.BacktraceHelper = helper;
		}



		public ObjectValue CreateObjectValue(IDBacktraceSymbol s)
		{
			return ObjectValue.CreateError(this, new ObjectPath(), s.TypeName, s.Value, ObjectValueFlags.Error);
		}

		public ObjectValue[] GetChildren(ObjectPath path, int index, int count, EvaluationOptions options)
		{
			throw new NotImplementedException();
		}

		public object GetRawValue(ObjectPath path, EvaluationOptions options)
		{
			throw new NotImplementedException();
		}

		public ObjectValue GetValue(ObjectPath path, EvaluationOptions options)
		{
			throw new NotImplementedException();
		}

		public void SetRawValue(ObjectPath path, object value, EvaluationOptions options)
		{
			throw new NotImplementedException();
		}

		public EvaluationResult SetValue(ObjectPath path, string value, EvaluationOptions options)
		{
			throw new NotImplementedException();
		}
	}
}
