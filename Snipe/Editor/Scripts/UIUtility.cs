using UnityEditor;
using UnityEngine.UIElements;

namespace MiniIT.Snipe.Unity.Editor
{
	public static class UIUtility
	{
		public static void LoadUI(VisualElement root, string umxlName, string stylesheetName)
		{
			var baseStyle = LoadStyleSheet(stylesheetName);
			if (baseStyle != null)
			{
				root.styleSheets.Add(baseStyle);
			}

			var tree = LoadUxml(umxlName);
			if (tree != null)
			{
				tree.CloneTree(root);
			}
		}

		private static VisualTreeAsset LoadUxml(string fileStem)
		{
			string filter = fileStem + " t:VisualTreeAsset";
			var guids = AssetDatabase.FindAssets(filter);
			if (guids != null && guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
			}
			return null;
		}

		private static StyleSheet LoadStyleSheet(string fileStem)
		{
			string filter = fileStem + " t:StyleSheet";
			var guids = AssetDatabase.FindAssets(filter);
			if (guids != null && guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
			}
			return null;
		}
	}
}
