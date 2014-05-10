using D_Parser.Dom;
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
		void GetCurrentStackFrameInfo(out string file, out ulong offset, out CodeLocation sourceLocation);

		IEnumerable<IDBacktraceSymbol> Parameters { get; }
		IEnumerable<IDBacktraceSymbol> Locals { get; }

		/// <summary>
		/// Amount of bytes per pointer.
		/// Used for determining whether the program is a 64 or 32 bit program.
		/// </summary>
		int PointerSize { get; }

		byte[] ReadBytes(ulong offset, ulong size);
		byte ReadByte(ulong offset);
		short ReadInt16(ulong offset);
		int ReadInt32(ulong offset);
		long ReadInt64(ulong offset);

		ResolutionContext LocalsResolutionHelperContext { get; }

		/// <summary>
		/// Strictly optional: Allows dynamic execution/injection of code while debuggee status is claimed to be "paused".
		/// Mainly used for toString()-Examination for D objects.
		/// </summary>
		IActiveExamination ActiveExamination { get; }
	}

	public interface IActiveExamination
	{
		/// <summary>
		/// Throws InvalidOperationException if data couldn't be allocated
		/// </summary>
		ulong Allocate(int size);
		void Free(ulong offset, int size);

		void Write(ulong offset, byte[] data);
		/// <summary>
		/// Pushes the stack base pointer, puts the E/RIP to <para name="offset"/>, executes it until a return command occurs.
		/// </summary>
		void Execute(ulong offset);
	}
}
