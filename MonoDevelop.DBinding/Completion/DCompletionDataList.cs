using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ICSharpCode.NRefactory.Completion;
using MonoDevelop.Ide.CodeCompletion;

namespace MonoDevelop.D.Completion
{
	/// <summary>
	/// Strictly add-only list.
	/// </summary>
	sealed class DCompletionDataList : ICompletionDataList // IMutableCompletionDataList implementation is somehow broken and not suited for multithreading.
	{
		#region Properties
		public event EventHandler Changing;
		public event EventHandler Changed;
		public event EventHandler CompletionListClosed;

		bool changing;
		public bool IsChanging {
			get{ return changing; }
			set{ 
				var wasChanging = changing;
				changing = value;

				if (changing && !wasChanging) {
					if (Changing != null)
						MonoDevelop.Ide.DispatchService.GuiSyncDispatch (() => 
							Changing (null, EventArgs.Empty));
				} else if (!changing && wasChanging) {
					if (Changed != null)
						MonoDevelop.Ide.DispatchService.GuiSyncDispatch (() => 
							Changed (null, EventArgs.Empty));
				}
			}
		}

		bool sorted;
		public bool IsSorted {
			get {
				return sorted;
			}
		}

		public bool AutoCompleteUniqueMatch { get;set; }
		public bool AutoCompleteEmptyMatch { get;set; }
		public bool AutoCompleteEmptyMatchOnCurlyBrace { get;set; }
		public bool CloseOnSquareBrackets { get;set; }
		public bool AutoSelect { get;set; }
		public string DefaultCompletionString { get;set; }
		public CompletionSelectionMode CompletionSelectionMode { get;set; }

		readonly List<ICompletionKeyHandler> keyHandlers = new List<ICompletionKeyHandler>();
		public void AddKeyHandler(ICompletionKeyHandler h) { keyHandlers.Add(h); }
		public System.Collections.Generic.IEnumerable<ICompletionKeyHandler> KeyHandler {
			get {  return keyHandlers; }
		}

		readonly List<ICompletionData> sortedList = new List<ICompletionData>();
		public readonly CancellationToken cancelAddition;
		#endregion

		public void Dispose ()	{	}

		public DCompletionDataList(CancellationToken tok)
		{
			IsChanging = true;

			AutoCompleteEmptyMatch = false;
			AutoCompleteEmptyMatchOnCurlyBrace = false;
			AutoSelect = true;
			CloseOnSquareBrackets = true;

			cancelAddition = tok;
		}

		void WaitForChangingCompleted()
		{
			while (IsChanging && !cancelAddition.IsCancellationRequested)
				Thread.Sleep (1);
		}

		public void Sort (Comparison<ICompletionData> comparison)
		{
			lock (sortedList) {
				sortedList.Sort (comparison);
				sorted = true;
			}
		}

		public void Sort (System.Collections.Generic.IComparer<ICompletionData> comparison)
		{
			lock (sortedList) {
				sortedList.Sort (comparison);
				sorted = true;
			}
		}

		public void OnCompletionListClosed (EventArgs e)
		{
			if (CompletionListClosed != null)
				CompletionListClosed (this, e);
		}



		public int IndexOf (ICompletionData item)
		{
			return sortedList.IndexOf (item);
		}

		public void Insert (int index, ICompletionData item)
		{
			throw new NotImplementedException ();
		}

		public void RemoveAt (int index)
		{
			throw new NotImplementedException ();
		}

		public ICompletionData this [int index] {
			get {
				return sortedList [index];
			}
			set {
				throw new NotImplementedException ();
			}
		}

		public void Add (ICompletionData item)
		{
			sortedList.Add (item);
			sorted = false;
		}

		public void Clear ()
		{
			sortedList.Clear ();
		}

		public bool Contains (ICompletionData item)
		{
			return sortedList.Contains (item);
		}

		public void CopyTo (ICompletionData[] array, int arrayIndex)
		{
			sortedList.CopyTo (array, arrayIndex);
		}

		public bool Remove (ICompletionData item)
		{
			throw new NotImplementedException ();
		}

		public int Count {
			get {
				WaitForChangingCompleted ();
				return sortedList.Count;
			}
		}

		public bool IsReadOnly {
			get {
				return false;
			}
		}

		public System.Collections.Generic.IEnumerator<ICompletionData> GetEnumerator ()
		{
			return sortedList.GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return sortedList.GetEnumerator ();
		}
	}
}

