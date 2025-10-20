#if UNITY_EDITOR

using System.Collections.Generic;
using MiniIT.Snipe.Debugging;
using UnityEditor;

namespace MiniIT.Snipe.Unity.Editor
{
	public class ErrorCodesHighlightWindow : EditorWindow
	{
		private ErrorCodesTracker _tracker;
		private Dictionary<string, List<string>> _groupedMessages;

		//[MenuItem("Snipe/ErrorCodes")] only for tests
		public static void ShowWindow()
		{
			var window = GetWindow<ErrorCodesHighlightWindow>(true, "Snipe ErrorCodes", true);
			window.Init(UnitySnipeServicesFactory.DebugErrorsTracker);
		}

		private void Init(ISnipeErrorsTracker tracker)
		{
			if (tracker is not ErrorCodesTracker errorTracker)
			{
				return;
			}

			_tracker = errorTracker;

			if (_tracker != null)
			{
				_groupedMessages = new Dictionary<string, List<string>>(_tracker.Items.Count);

				foreach (var item in _tracker.Items)
				{
					if (item.TryGetValue("message_type", out object msgType) && msgType is string messageType)
					{
						if (!_groupedMessages.ContainsKey(messageType))
						{
							_groupedMessages.Add(messageType, new List<string>());
						}

						_groupedMessages[messageType].Add(fastJSON.JSON.ToJSON(item));
					}
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
					EditorGUILayout.LabelField(item);
				}

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.EndVertical();
		}
	}
}

#endif
