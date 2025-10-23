using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public static class SnipeToolsConfig
	{
		private struct ConfigData
		{
			public string AuthKey;
			public string ProjectStringID;
			public bool LoadDefaultConfigOnBuild;

			internal void SetValues(ConfigData data)
			{
				if (AuthKey == data.AuthKey)
				{
					return;
				}

				AuthKey = data.AuthKey;
				ProjectStringID = data.ProjectStringID;
				LoadDefaultConfigOnBuild = data.LoadDefaultConfigOnBuild;
			}
		}

		public static bool Initialized => s_data.AuthKey != null;
		public static bool IsAuthKeyValid => !string.IsNullOrEmpty(AuthKey) && ProjectId > 0;

		public static string AuthKey => s_data.AuthKey;
		public static int ProjectId { get; private set; }

		// public static string ProjectStringID
		// {
		// 	get => s_data?.ProjectStringID ?? string.Empty;
		// 	private set
		// 	{
		// 		if (s_data != null)
		// 		{
		// 			s_data.ProjectStringID = value;
		// 		}
		// 	}
		// }

		public static bool LoadDefaultConfigOnBuild
		{
			get => s_data.LoadDefaultConfigOnBuild;
			set => s_data.LoadDefaultConfigOnBuild = value;
		}

		private static ConfigData s_data;

		private static string GetConfigFilePath()
		{
			return Path.Combine(Application.dataPath, "..", "snipe_tool_config.json");
		}

		private static string GetLegacyAuthKeyFilePath()
		{
			return Path.Combine(Application.dataPath, "..", "snipe_api_key");
		}

		public static void Load(bool force = false)
		{
			if (Initialized && !force)
			{
				return;
			}

			string path = GetConfigFilePath();
			if (!File.Exists(path))
			{
				Log("Load - config file does not exist");

				path = GetLegacyAuthKeyFilePath();
				if (File.Exists(path))
				{
					Log("Load - legacy snipe_api_key file found");

					string legacyAuthKey = File.ReadAllText(path);
					if (TryParseAuthKey(legacyAuthKey, out int legacyProjectId))
					{
						ProjectId = legacyProjectId;

						s_data.AuthKey = legacyAuthKey;
						s_data.LoadDefaultConfigOnBuild = true;

						Log("Load - Deleting legacy snipe_api_key file");
						File.Delete(path);

						Log("Load - Save new config file");
						Save();
					}
				}
			}

			ConfigData data;

			try
			{
				string content = File.ReadAllText(path);
				data = fastJSON.JSON.ToObject<ConfigData>(content);
			}
			catch (Exception e)
			{
				Log($"Load - failed to parse config: {e}");
				return;
			}

			if (data.AuthKey == null)
			{
				Log("Load - failed to parse config");
				return;
			}

			if (TryParseAuthKey(data.AuthKey, out int projectId))
			{
				s_data.SetValues(data);
				ProjectId = projectId;
			}
		}

		public static void Save()
		{
			string json = fastJSON.JSON.ToNiceJSON(s_data, new fastJSON.JSONParameters() { UseExtensions = false });

			string path = GetConfigFilePath();
			File.WriteAllText(path, json);
		}

		public static bool TrySetAuthKey(string authKey)
		{
			if (TryParseAuthKey(authKey, out int projectId))
			{
				s_data.AuthKey = authKey;
				ProjectId = projectId;
				s_data.ProjectStringID = null;
				return true;
			}

			return false;
		}

		private static bool TryParseAuthKey(string authKey, out int projectId)
		{
			string extractedProjectId = null;
			if (!string.IsNullOrEmpty(authKey))
			{
				string[] parts = authKey.Split('-');
				if (parts.Length > 3 && parts[0] == "api")
				{
					extractedProjectId = parts[1];
				}
			}

			if (!string.IsNullOrEmpty(extractedProjectId) && int.TryParse(extractedProjectId, out int pid))
			{
				projectId = pid;
				return true;
			}

			projectId = 0;
			return false;
		}

		public static string GetProjectStringID(bool fetch = true)
		{
			if (fetch && string.IsNullOrEmpty(s_data.ProjectStringID) && IsAuthKeyValid)
			{
				s_data.ProjectStringID = FetchProjectStringID();
			}
			return s_data.ProjectStringID;
		}

		public static string StripProjectStringID(string projectStringID)
		{
			if (projectStringID.EndsWith("_dev"))
			{
				return projectStringID[..^4];
			}

			if (projectStringID.EndsWith("_live"))
			{
				return projectStringID[..^5];
			}

			return projectStringID;
		}

		private static string FetchProjectStringID()
		{
			string projectStringID;

			using var httpClient = new HttpClient();

			try
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{ProjectId}/stringID";
				var content = httpClient.GetStringAsync(url).Result;

				Log($"{content}");

				var responseData = fastJSON.JSON.ToObject<ProjectStringIdResponseData>(content);
				projectStringID = responseData.stringID;
				Log($"Project StringID request errorCode = {responseData.errorCode}");
				Log($"Project StringID = {projectStringID}");
				projectStringID = StripProjectStringID(projectStringID);
				Log($"Stripped Project StringID = {projectStringID}");
			}
			catch (Exception e)
			{
				LogError($"[{nameof(SnipeToolsConfig)}] FAILED to fetch projects list: {e}");
				projectStringID = null;
			}

			return projectStringID;
		}

		private static void Log(string msg)
		{
			Debug.Log($"[{nameof(SnipeToolsConfig)}] {msg}");
		}

		private static void LogError(string msg)
		{
			Debug.LogError($"[{nameof(SnipeToolsConfig)}] {msg}");
		}
	}

	[Serializable]
	internal class ProjectStringIdResponseData
	{
		public string errorCode;
		public string stringID;
	}
}
