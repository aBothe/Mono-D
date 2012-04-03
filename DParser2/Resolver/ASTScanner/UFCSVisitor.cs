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
		public UFCSVisitor(ResolverContextStack ctxt)
			: base(ctxt)
		{
		}

		/// <summary>
		/// If null, this filter will be bypassed
		/// </summary>
		public string NameToSearch;
		public ResolveResult FirstParamToCompareWith;

		public List<DMethod> Matches = new List<DMethod>();

		#region Threading
		/// <summary>
		/// Spreads symbol resolution on multiple threads.
		/// </summary>
		public bool WorkAsync;

		Thread[] resolveThreads = new Thread[Environment.ProcessorCount];
		Stack<DMethod>[] queues = new Stack<DMethod>[Environment.ProcessorCount];

		public override void IterateThroughScopeLayers(CodeLocation Caret, MemberFilter VisibleMembers = MemberFilter.All)
		{
			if (WorkAsync)
				for (int i = 0; i < Environment.ProcessorCount; i++)
				{
					queues[i] = new Stack<DMethod>();
					resolveThreads[i] = new Thread(_th);
				}

			base.IterateThroughScopeLayers(Caret, VisibleMembers);

			if (WorkAsync)
			{
				for (int i = 0; i < Environment.ProcessorCount; i++)
					resolveThreads[i].Start(queues[i]);

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
		}

		void _th(object s)
		{
			var q = (Stack<DMethod>)s;

			Thread.CurrentThread.IsBackground = true;

			while (q.Count > 0)
				HandleMethod(q.Pop());
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
