using System;
using System.IO;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public static class SnipeToolsConfig
	{
		class ConfigData
		{
			public string AuthKey;
			public string ProjectStringID;
			public bool LoadDefaultConfigOnBuild;
		}

		public static bool Initialized => _data?.AuthKey != null;

		public static string AuthKey => _data?.AuthKey;
		public static int ProjectId { get; private set; }

		public static string ProjectStringID
		{
			get => _data?.ProjectStringID ?? String.Empty;
			set
			{
				if (_data != null)
				{
					_data.ProjectStringID = value;
				}
			}
		}

		public static bool LoadDefaultConfigOnBuild
		{
			get => _data?.LoadDefaultConfigOnBuild ?? false;
			set
			{
				if (_data != null)
				{
					_data.LoadDefaultConfigOnBuild = value;
				}
			}
		}

		private static ConfigData _data = new ConfigData();

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
				Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - config file does not exist");

				path = GetLegacyAuthKeyFilePath();
				if (File.Exists(path))
				{
					Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - legacy snipe_api_key file found");

					string legacyAuthKey = File.ReadAllText(path);
					if (TryParseAuthKey(legacyAuthKey, out int legacyProjectId))
					{
						ProjectId = legacyProjectId;

						_data = new ConfigData()
						{
							AuthKey = legacyAuthKey,
							LoadDefaultConfigOnBuild = true,
						};

						Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - Deleting legacy snipe_api_key file");
						File.Delete(path);

						Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - Save new config file");
						Save();
					}
				}
			}

			ConfigData data = null;

			try
			{
				string content = File.ReadAllText(path);
				data = fastJSON.JSON.ToObject<ConfigData>(content);
			}
			catch (Exception e)
			{
				Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - failed to parse config: {e}");
				return;
			}
			
			if (data == null)
			{
				Debug.Log($"[{nameof(SnipeToolsConfig)}] Load - failed to parse config");
				return;
			}
			
			if (TryParseAuthKey(data.AuthKey, out int projectId))
			{
				_data = data;
				ProjectId = projectId;
			}
		}

		public static void Save()
		{
			string json = fastJSON.JSON.ToNiceJSON(_data, new fastJSON.JSONParameters() { UseExtensions = false });

			string path = GetConfigFilePath();
			File.WriteAllText(path, json);
		}

		public static bool TrySetAuthKey(string authKey)
		{
			if (TryParseAuthKey(authKey, out int projectId))
			{
				_data ??= new ConfigData();
				_data.AuthKey = authKey;
				ProjectId = projectId;
				return true;
			}

			return false;
		}

		private static bool TryParseAuthKey(string authKey, out int projectId)
		{
			string project_id = null;
			if (!string.IsNullOrEmpty(authKey))
			{
				string[] parts = authKey.Split('-');
				if (parts.Length > 3 && parts[0] == "api")
				{
					project_id = parts[1];
				}
			}

			if (!string.IsNullOrEmpty(project_id) && int.TryParse(project_id, out int pid))
			{
				projectId = pid;
				return true;
			}

			projectId = 0;
			return false;
		}
	}
}