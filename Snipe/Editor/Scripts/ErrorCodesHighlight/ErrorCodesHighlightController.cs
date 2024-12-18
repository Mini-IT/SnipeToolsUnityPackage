#if UNITY_EDITOR

using System.Collections.Generic;
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
				s_tracker?.Clear();
				UnitySnipeServicesFactory.DebugErrorsTracker = s_tracker;
			}
			else if (state == PlayModeStateChange.ExitingPlayMode)
			{
				if (s_tracker != null && s_tracker.Items.Count > 0)
				{
					ErrorCodesHighlightWindow.ShowWindow(s_tracker);
				}
			}
		}

		public static void AddNotOk(IDictionary<string, object> properties)
		{
			s_tracker ??= new ErrorCodesTracker();
			s_tracker.TrackNotOk(properties);
		}
	}
}

#endif
