using System.Collections.Generic;

namespace D_Parser.Parser
{
	public interface TokenTracker
	{
		void OnToken(AbstractLexer lexer,int kind);
	}

	public class TrackerContainer
	{
		readonly AbstractLexer assocLexer;

		public TrackerContainer(AbstractLexer lx)
		{
			assocLexer = lx;
		}

		public readonly List<TokenTracker> Trackers = new List<TokenTracker>();

		public void Register(TokenTracker tracker)
		{
			Trackers.Add(tracker);
		}

		public void InformToken(int kind)
		{
			if(Trackers.Count < 1)
				return;

			Trackers[0].OnToken(assocLexer,kind);

			if (Trackers.Count > 1)
				for (int i = 1; i < Trackers.Count; i++)
					Trackers[i].OnToken(assocLexer,kind);
		}
	}
}
