#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;

namespace MiniIT.Snipe.Unity.Editor
{
	public class ErrorCodesHighlightWindow : EditorWindow
	{
		private ErrorCodesTracker _tracker;

		//[MenuItem("Snipe/ErrorCodes...")]
		public static void ShowWindow(ErrorCodesTracker tracker)
		{
			var window = EditorWindow.GetWindow<ErrorCodesHighlightWindow>("Snipe ErrorCodes");
			window.Init(tracker);
		}

		private void Init(ErrorCodesTracker tracker)
		{
			_tracker = tracker;
		}

		private void OnGUI()
		{
			if (_tracker == null)
			{
				return;
			}

			EditorGUILayout.BeginVertical();

			for (int i = 0; i < _tracker.Items.Count; i++)
			{
				IDictionary<string, object> item = _tracker.Items[i];
				EditorGUILayout.TextField(fastJSON.JSON.ToJSON(item));
			}

			EditorGUILayout.EndVertical();
		}
	}
}

#endif
