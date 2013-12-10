using System;
using MonoDevelop.Ide.Gui.Content;
using System.Threading;
using MonoDevelop.Ide;
using MonoDevelop.Core;

namespace MonoDevelop.D.Gui
{
	public class MixinInsightExtension : TextEditorExtension
	{
		static AutoResetEvent stateChanged = new AutoResetEvent(false);
		static Thread updateTh;
		static bool initialized;

		public override void Initialize()
		{
			base.Initialize();

			if (!initialized)
			{
				initialized = true;

				if(MixinInsightPad.EnableCaretTracking)
					StartUpdateThread();

				PropertyService.AddPropertyHandler(MixinInsightPad.activateAutomatedCaretTrackingPropId, (object s, PropertyChangedEventArgs pea) =>
				{
					if ((bool)pea.NewValue)
					{
						StartUpdateThread();
					}
					else if (updateTh != null)
					{
						updateTh.Abort();
					}
				});
			}

			Document.DocumentParsed += Document_DocumentParsed;
		}

		static void StartUpdateThread()
		{
			if(updateTh == null || !updateTh.IsAlive){
				updateTh = new Thread(updateTh_method);
				updateTh.IsBackground = true;
				updateTh.Priority = ThreadPriority.Lowest;
				updateTh.Start();
			}
		}

		void Document_DocumentParsed(object sender, EventArgs e)
		{
			stateChanged.Set();
		}

		public override void Dispose()
		{
			Document.DocumentParsed -= Document_DocumentParsed;
		}

		public override void TextChanged(int startIndex, int endIndex)
		{
			stateChanged.Set();
			base.TextChanged(startIndex, endIndex);
		}

		public override void CursorPositionChanged()
		{
			base.CursorPositionChanged();
			stateChanged.Set();
		}

		static void updateTh_method()
		{
			while (true)
			{
				stateChanged.WaitOne();
				while (stateChanged.WaitOne(400));

				var pad = MixinInsightPad.Instance;			
				if (pad != null && pad.Window.ContentVisible)
					pad.Update();
			}
		}

		public override bool ExtendsEditor(MonoDevelop.Ide.Gui.Document doc, IEditableTextBuffer editor)
		{
			return doc.IsFile && DLanguageBinding.IsDFile(doc.FileName);
		}
	}
}
