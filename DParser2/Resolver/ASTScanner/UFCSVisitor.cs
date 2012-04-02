using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace D_Parser.Resolver.ASTScanner
{
	public class UFCSVisitor : AbstractVisitor
	{
		public UFCSVisitor(ResolverContextStack ctxt) : base(ctxt) {
		}

		/// <summary>
		/// If null, this filter will be bypassed
		/// </summary>
		public string NameToSearch;
		public ResolveResult FirstParamToCompareWith;

		public List<DMethod> Matches=new List<DMethod>();

		#region Threading
		/// <summary>
		/// Spreads symbol resolution on multiple threads.
		/// </summary>
		public bool WorkAsync;

		Thread[] resolveThreads = new Thread[Environment.ProcessorCount];
		Stack<DMethod>[] queues = new Stack<DMethod>[Environment.ProcessorCount];
		bool[] go = new bool[Environment.ProcessorCount];
		bool finishedQueuing;

		public override void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers = MemberFilter.All)
		{
			finishedQueuing = false;

			/*
			 * Start handling methods even WHILE enqueing for maximum performance.
			 */

			if (WorkAsync)
				for (int i = 0; i < Environment.ProcessorCount; i++)
				{
					queues[i] = new Stack<DMethod>();
					var th = resolveThreads[i] = new Thread(_th);
					th.Start(i);
				}

			base.IterateThroughScopeLayers(Caret, VisibleMembers);

			finishedQueuing = true;

			// Wait for all threads to finish resolving
			for (int i = 0; i < Environment.ProcessorCount; i++)
			{
				var th = resolveThreads[i];
				if (th != null && th.IsAlive)
				{
					th.Join(10000);
					th = null;
				}
			}
		}

		void _th(object s)
		{
			int i = (int)s;
			Thread.CurrentThread.IsBackground = true;

			var q=queues[i];
			do
			{
				while (q.Count > 0)
					HandleMethod(q.Pop());

				Thread.Sleep(1);
			}
			while ((q.Count!= 0 && !finishedQueuing) || !go[i]);
		}
		#endregion

		long k;
		protected override bool HandleItem(INode n)
		{
			if ((NameToSearch == null ? !string.IsNullOrEmpty(n.Name) : n.Name == NameToSearch) && 
				n is DMethod)
			{
				var dm = (DMethod)n;

				if (dm.Parameters.Count != 0)
				{
					if (WorkAsync) // Assign items to threads evenly
					{
						k++;
						queues[k % Environment.ProcessorCount].Push(dm);
						go[k % Environment.ProcessorCount] = true;
					}
					else
						HandleMethod(dm);
				}
			}

			return false;
		}

		void HandleMethod(object s)
		{
			var dm = (DMethod)s;

			var threadSafeContext = Context.Clone();

			var firstParam = TypeResolution.TypeDeclarationResolver.Resolve(dm.Parameters[0].Type, threadSafeContext);

			//TODO: Compare the resolved parameter with the first parameter given
			if (true)
			{
				Matches.Add(dm);
			}
		}
	}
}
