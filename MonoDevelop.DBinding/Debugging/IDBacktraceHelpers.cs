using D_Parser.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Debugging
{
	public interface IDBacktraceSymbol
	{
		ulong Offset { get; }
		string Name { get; }
		string TypeName { get; }
		string Value { get; }
		string FileName { get; }

		bool HasParent { get; }
		IDBacktraceSymbol Parent { get; }

		int ChildCount { get; }
		IEnumerable<IDBacktraceSymbol> Children { get; }
	}

	public interface IDBacktraceHelpers
	{
		void SelectStackFrame(int frameIndex);

		IEnumerable<IDBacktraceSymbol> Parameters { get; }
		IEnumerable<IDBacktraceSymbol> Locals { get; }

		int PointerByteSize { get; }

		byte[] ReadBytes(ulong offset, ulong size);
		byte ReadByte(ulong offset);
		short ReadInt16(ulong offset);
		int ReadInt32(ulong offset);
		long ReadInt64(ulong offset);

		ResolutionContext LocalsResolutionHelperContext { get; }
		void UpdateHelperContextToCurrentStackFrame();
	}
}
