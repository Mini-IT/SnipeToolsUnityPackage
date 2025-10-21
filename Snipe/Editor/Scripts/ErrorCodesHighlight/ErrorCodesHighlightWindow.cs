#if UNITY_EDITOR

using System.Collections.Generic;
using MiniIT.Snipe.Debugging;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

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

			RefreshUI();
		}

		public void CreateGUI()
		{
			var root = rootVisualElement;
			UIUtility.LoadUI(root, "ErrorCodesHighlightWindow", "base");
		}

		private void RefreshUI()
		{
			var root = rootVisualElement;

			var search = root.Q<ToolbarSearchField>("search");
			var groupsList = root.Q<ListView>("groups");
			var itemsList = root.Q<ListView>("items");

			var groups = new List<string>(_groupedMessages.Keys);
			groupsList.itemsSource = groups;
			groupsList.makeItem = () => new Label();
			groupsList.bindItem = (e, i) => ((Label)e).text = $"{groups[i]} ({_groupedMessages[groups[i]].Count})";
			groupsList.onSelectionChange += selected =>
			{
				foreach (var obj in selected)
				{
					if (obj is string key)
					{
						SetItems(itemsList, _groupedMessages[key]);
					}
					break;
				}
			};

			SetItems(itemsList, groups.Count > 0 ? _groupedMessages[groups[0]] : null);

			search.RegisterValueChangedCallback(evt =>
			{
				string term = evt.newValue?.Trim();
				if (string.IsNullOrEmpty(term))
				{
					groupsList.itemsSource = groups;
					groupsList.Rebuild();
					return;
				}
				var filtered = new List<string>();
				foreach (var g in groups)
				{
					if (g.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
					{
						filtered.Add(g);
					}
				}
				groupsList.itemsSource = filtered;
				groupsList.Rebuild();
			});
		}

		private static void SetItems(ListView list, List<string> items)
		{
			items ??= new List<string>();
			list.itemsSource = items;
			list.makeItem = () => new TextField() { isReadOnly = true };
			list.bindItem = (e, i) => ((TextField)e).value = items[i];
			list.Rebuild();
		}
	}
}

#endif
