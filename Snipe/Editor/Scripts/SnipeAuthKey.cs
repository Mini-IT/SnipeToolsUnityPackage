using System.IO;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public static class SnipeAuthKey
	{
		public static string AuthKey { get; private set; }
		public static int ProjectId { get; private set; }

		private static string GetAuthKeyFilePath()
		{
			return Path.Combine(Application.dataPath, "..", "snipe_api_key");
		}

		public static void Load()
		{
			string path = GetAuthKeyFilePath();
			if (File.Exists(path))
			{
				string content = File.ReadAllText(path);
				Set(content);
			}
		}

		public static void Save()
		{
			string path = GetAuthKeyFilePath();
			if (string.IsNullOrEmpty(AuthKey))
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			else
			{
				File.WriteAllText(path, AuthKey);
			}
		}

		public static void Set(string value)
		{
			string project_id = null;
			if (!string.IsNullOrEmpty(value))
			{
				string[] parts = value.Split('-');
				if (parts.Length > 3 && parts[0] == "api")
				{
					project_id = parts[1];
				}
			}

			if (!string.IsNullOrEmpty(project_id) && int.TryParse(project_id, out int pid))
			{
				AuthKey = value;
				ProjectId = pid;
			}
			else
			{
				AuthKey = null;
			}
		}
	}
}