using UnityEditor;

namespace MiniIT.Snipe.Unity.Editor
{
	public static class SnipeToolsGUI
	{
		public static void DrawAuthKeyWidget()
		{
			string authKey = EditorGUILayout.TextField("API Key", SnipeToolsConfig.AuthKey);
			if (authKey != SnipeToolsConfig.AuthKey)
			{
				if (SnipeToolsConfig.TrySetAuthKey(authKey))
				{
					SnipeToolsConfig.Save();
				}
			}
		}

		public static string DrawProjectStringIDWidget()
		{
			string projectStringID = EditorGUILayout.TextField("Project String ID", SnipeToolsConfig.ProjectStringID).Trim();
			if (projectStringID != SnipeToolsConfig.ProjectStringID)
			{
				if (projectStringID.EndsWith("_dev"))
				{
					projectStringID = projectStringID.Substring(0, projectStringID.Length - 4);
				}
				else if (projectStringID.EndsWith("_live"))
				{
					projectStringID = projectStringID.Substring(0, projectStringID.Length - 5);
				}

				SnipeToolsConfig.ProjectStringID = projectStringID;
				SnipeToolsConfig.Save();
			}

			if (string.IsNullOrWhiteSpace(SnipeToolsConfig.ProjectStringID))
			{
				EditorGUILayout.HelpBox("You must specify a valid project stringID", MessageType.Error);
			}

			return projectStringID;
		}
	}
}
