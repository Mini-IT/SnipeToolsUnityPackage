#if UNITY_EDITOR && SNIPE_8_0_OR_NEWER

using UnityEditor;

namespace MiniIT.Snipe.Unity.Editor
{
	// ensure class initializer is called whenever scripts recompile
	[InitializeOnLoad]
	public static class ErrorCodesHighlightController
	{
		private static ErrorCodesTracker s_tracker;

		static ErrorCodesHighlightController()
		{
			EditorApplication.playModeStateChanged += LogPlayModeState;
		}

		private static void LogPlayModeState(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredPlayMode)
			{
				s_tracker ??= new ErrorCodesTracker();
				s_tracker.Clear();

				UnitySnipeServicesFactory.DebugErrorsTracker = s_tracker;
			}
			else if (state == PlayModeStateChange.ExitingPlayMode)
			{
				if (s_tracker == UnitySnipeServicesFactory.DebugErrorsTracker && s_tracker.Items.Count > 0)
				{
					ErrorCodesHighlightWindow.ShowWindow(s_tracker);
				}
			}
		}
	}
}

#endif
