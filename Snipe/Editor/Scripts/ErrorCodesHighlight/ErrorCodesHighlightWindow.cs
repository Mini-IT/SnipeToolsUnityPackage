#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;

namespace MiniIT.Snipe.Unity.Editor
{
	public class ErrorCodesHighlightWindow : EditorWindow
	{
		private ErrorCodesTracker _tracker;
		private Dictionary<string, List<string>> _groupedMessages;

		//[MenuItem("Snipe/ErrorCodes...")]
		public static void ShowWindow(ErrorCodesTracker tracker)
		{
			var window = EditorWindow.GetWindow<ErrorCodesHighlightWindow>(true, "Snipe ErrorCodes", true);
			window.Init(tracker);
		}

		private void Init(ErrorCodesTracker tracker)
		{
			_tracker = tracker;

			_groupedMessages = new Dictionary<string, List<string>>(_tracker.Items.Count);
			foreach (var item in _tracker.Items)
			{
				if (item.TryGetValue("message_type", out var msgtype) && msgtype is string messageType)
				{
					if (!_groupedMessages.ContainsKey(messageType))
					{
						_groupedMessages.Add(messageType, new List<string>());
					}
					_groupedMessages[messageType].Add(fastJSON.JSON.ToJSON(item));
				}
			}
		}

		private void OnGUI()
		{
			if (_tracker == null)
			{
				return;
			}

			EditorGUILayout.BeginVertical();

			foreach (var msg in _groupedMessages)
			{
				EditorGUILayout.Foldout(true, $"{msg.Key} ({msg.Value.Count})", true);
				EditorGUI.indentLevel++;
				foreach (string item in msg.Value)
				{
					EditorGUILayout.TextField(item);
				}
				EditorGUI.indentLevel++;
			}

			EditorGUILayout.EndVertical();
		}
	}
}

#endif
