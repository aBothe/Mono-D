using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace D_Parser.Misc
{
	public class ParseLog
	{
		StreamWriter sw;
		bool hadErrors;

		public static void Write(ParseCache cache, string outputLog)
		{
			var ms = new MemoryStream(32000);

			var pl = new ParseLog(ms);

			pl.Write(cache);

			if (File.Exists(outputLog))
				File.Delete(outputLog);

			File.WriteAllBytes(outputLog, ms.ToArray());
			ms.Close();
		}

		public ParseLog(Stream s)
		{
			sw = new StreamWriter(s, System.Text.Encoding.Unicode) { AutoFlush=false };
		}

		~ParseLog()
		{
			sw.Close();
		}

		public void Write(ParseCache cache)
		{
			sw.WriteLine("Parser error log");
			sw.WriteLine();

			Write(cache.Root);

			if(!hadErrors)
				sw.WriteLine("No errors found.");

			sw.WriteLine();
			sw.Flush();
		}

		void Write(ModulePackage package)
		{
			foreach (var kv in package.Modules)
			{
				if (kv.Value.ParseErrors.Count < 1)
					continue;
				hadErrors = true;
				sw.WriteLine(kv.Value.ModuleName + "\t\t(" + kv.Value.FileName + ")");
				foreach (var err in kv.Value.ParseErrors)
					sw.WriteLine(string.Format("\t\t{0}\t{1}\t{2}", err.Location.Line, err.Location.Column, err.Message));

				sw.WriteLine();
			}

			sw.Flush();

			foreach (var kv in package.Packages)
				Write(kv.Value);
		}
	}
}
