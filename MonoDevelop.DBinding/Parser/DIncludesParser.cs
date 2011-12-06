using System;
using System.Collections.Generic;
using System.Threading;
using D_Parser.Completion;

namespace MonoDevelop.D
{
	public class DIncludesParser
	{
		#region DDirectoryParserItem
		private class DDirectoryParserItem
		{
			public string Directory{get;set;}
			public ASTStorage Storage{get;set;}
			public DDirectoryParserItem(string directory, ASTStorage storage)
			{
				this.Directory = directory;
				this.Storage = storage;
			}
		}	
		
		#endregion
		
		Queue<DDirectoryParserItem> directoriesToParse;
		Thread thread;
		public DIncludesParser ()
		{
			directoriesToParse = new Queue<DDirectoryParserItem>();
        	thread = new Thread(new ThreadStart(execute));			
		}
		
		public void AddDirectory(string directory, ASTStorage storage)
		{
			lock (directoriesToParse){
				directoriesToParse.Enqueue(new DDirectoryParserItem(directory, storage));
			}
			start();
		}
		
		public void AddDirectoryRange(List<string> directories, ASTStorage storage)
		{
			foreach(string directory in directories)
			{
				lock(directoriesToParse){				
					directoriesToParse.Enqueue(new DDirectoryParserItem(directory, storage));
				}
			}		
			start();			
		}
		
		private void start()
		{
			switch (thread.ThreadState)
			{
				case ThreadState.WaitSleepJoin:
					thread.Interrupt();
					break;
				case ThreadState.Unstarted:
					thread.Start();
					break;
			}
		
			if (thread.ThreadState == ThreadState.Unstarted)			
        		thread.Start();
			
		}
		
		private delegate int safeGetCountFunc();
		private void execute()
		{		
			while (true)
			{
				DDirectoryParserItem parserItem = null;
				safeGetCountFunc  safeGetCount = delegate(){lock (directoriesToParse){return directoriesToParse.Count;}};			
				
				List<ASTCollection> col = new List<ASTCollection>();			
				while (safeGetCount() != 0)
				{
					string currentDir = "";
					lock (directoriesToParse){	
						parserItem = directoriesToParse.Dequeue();
						currentDir = parserItem.Directory;
					}
	
		            var ac = new ASTCollection(currentDir);
		            ac.UpdateFromBaseDirectory();
					col.Add(ac);
					
					if (col.Count != 0) 
					{
						lock(parserItem.Storage){
							parserItem.Storage.ParsedGlobalDictionaries.AddRange(col);
						}	            
					}								
				}	
				try	{Thread.Sleep(Timeout.Infinite);}
				catch (ThreadInterruptedException e)
				{/*do nothing*/ }
			}						
		}		
	}
}

