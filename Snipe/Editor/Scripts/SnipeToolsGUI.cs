using UnityEditor;
using UnityEngine;

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

					// Try fetching the ProjectStringID
					// string psid = SnipeToolsConfig.GetProjectStringID();
					// if (!string.IsNullOrEmpty(psid))
					// {
					// 	SnipeToolsConfig.Save();
					// }
				}
			}

			if (SnipeToolsConfig.IsAuthKeyValid)
			{
				string psid = SnipeToolsConfig.GetProjectStringID(false);

				GUILayout.BeginHorizontal();

				EditorGUI.BeginDisabledGroup(true);
				GUILayout.Label("Project String ID: " + psid);
				EditorGUI.EndDisabledGroup();

				if (string.IsNullOrEmpty(psid))
				{
					if (GUILayout.Button("Fetch Project String ID"))
					{
						psid = SnipeToolsConfig.GetProjectStringID(true);
						if (!string.IsNullOrEmpty(psid))
						{
							SnipeToolsConfig.Save();
						}
					}
				}

				GUILayout.EndHorizontal();
			}
			else
			{
				EditorGUILayout.HelpBox("Specify a valid API key", MessageType.Error);
			}
		}

		// public static string DrawProjectStringIDWidget()
		// {
		// 	string projectStringID = EditorGUILayout.TextField("Project String ID", SnipeToolsConfig.ProjectStringID).Trim();
		// 	if (projectStringID != SnipeToolsConfig.ProjectStringID)
		// 	{
		// 		if (projectStringID.EndsWith("_dev"))
		// 		{
		// 			projectStringID = projectStringID.Substring(0, projectStringID.Length - 4);
		// 		}
		// 		else if (projectStringID.EndsWith("_live"))
		// 		{
		// 			projectStringID = projectStringID.Substring(0, projectStringID.Length - 5);
		// 		}
		//
		// 		SnipeToolsConfig.ProjectStringID = projectStringID;
		// 		SnipeToolsConfig.Save();
		// 	}
		//
		// 	if (string.IsNullOrWhiteSpace(SnipeToolsConfig.ProjectStringID))
		// 	{
		// 		EditorGUILayout.HelpBox("You must specify a valid project stringID", MessageType.Error);
		// 	}
		//
		// 	return projectStringID;
		// }
	}
}
