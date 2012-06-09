using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Parser;

namespace D_Parser.Misc
{
	/// <summary>
	/// Helper class which scans through source directories.
	/// For better performance, this will be done using at least one thread.
	/// </summary>
	public class ThreadedDirectoryParser
	{
		#region Properties
		public static int numThreads = Environment.ProcessorCount;

		public Exception LastException;
		string baseDirectory;
		Stack<KeyValuePair<string, ModulePackage>> queue = new Stack<KeyValuePair<string, ModulePackage>>();
		#endregion

		public static ParsePerformanceData Parse(string directory, RootPackage rootPackage)
		{
			var ppd = new ParsePerformanceData { BaseDirectory = directory };

			/*
			 * 1) List all files
			 * 2) Create module packages
			 * 3) Enlist all file-package pairs in the queue
			 * 4) Start parse threads
			 * 5) Wait for all to join
			 */

			var tpd = new ThreadedDirectoryParser
			{
				baseDirectory = directory
			};

			// 1), 2), 3)
			tpd.PrepareQueue(rootPackage);

			ppd.AmountFiles = tpd.queue.Count;
			var sw = new Stopwatch();
			sw.Start();

			// 4)
			var threads = new Thread[numThreads];
			for (int i = 0; i < numThreads; i++)
			{
				var th = threads[i] = new Thread(tpd.ParseThread) {
					IsBackground = true,
					Priority= ThreadPriority.Lowest,
					Name = "Parser thread #"+i+" ("+directory+")"
				};
				th.Start();
			}

			// 5)
			for (int i = 0; i < numThreads; i++)
				if (threads[i].IsAlive)
					threads[i].Join(10000);

			sw.Stop();
			ppd.TotalDuration = sw.Elapsed.TotalSeconds;

			return ppd;
		}

		void PrepareQueue(RootPackage root)
		{
			//ISSUE: wild card character ? seems to behave differently across platforms
			// msdn: -> Exactly zero or one character.
			// monodocs: -> Exactly one character.
			var dFiles = Directory.GetFiles(baseDirectory, "*.d", SearchOption.AllDirectories);
			var diFiles = Directory.GetFiles(baseDirectory, "*.di", SearchOption.AllDirectories);
			var files = new string[dFiles.Length + diFiles.Length];
			Array.Copy(dFiles, 0, files, 0, dFiles.Length);
			Array.Copy(diFiles, 0, files, dFiles.Length, diFiles.Length);

			var lastPack = (ModulePackage)root;
			var lastDir = baseDirectory;

			bool isPhobosRoot = this.baseDirectory.EndsWith(Path.DirectorySeparatorChar + "phobos");

			foreach (var file in files)
			{
				var modulePath = DModule.GetModuleName(baseDirectory, file);

				if (lastDir != (lastDir = Path.GetDirectoryName(file)))
				{
					isPhobosRoot = this.baseDirectory.EndsWith(Path.DirectorySeparatorChar + "phobos");

					var packName = ModuleNameHelper.ExtractPackageName(modulePath);
					lastPack = root.GetOrCreateSubPackage(packName, true);
				}

				// Skip index.d (D2) || phobos.d (D2|D1)
				if (isPhobosRoot && (file.EndsWith("index.d") || file.EndsWith("phobos.d")))
					continue;

				queue.Push(new KeyValuePair<string, ModulePackage>(file, lastPack));
			}
		}

		void ParseThread()
		{
			var file = "";
			ModulePackage pack = null;

			while (queue.Count != 0)
			{
				lock (queue)
				{
					if (queue.Count == 0)
						return;

					var kv = queue.Pop();
					file = kv.Key;
					pack = kv.Value;
				}

				try
				{
					// If no debugger attached, save time + memory by skipping function bodies
					var ast = DParser.ParseFile(file, !Debugger.IsAttached);

					if (!(pack is RootPackage))
						ast.ModuleName = pack.Path + "." + Path.GetFileNameWithoutExtension(file);

					ast.FileName = file;
					pack.Modules[ModuleNameHelper.ExtractModuleName(ast.ModuleName)] = ast;
				}
				catch (Exception ex)
				{
					LastException = ex;
				}
			}
		}
	}
}
