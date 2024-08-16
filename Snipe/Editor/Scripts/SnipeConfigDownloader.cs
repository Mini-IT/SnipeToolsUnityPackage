#if UNITY_EDITOR && SNIPE_6_1

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeConfigDownloader : EditorWindow
	{
		private const string SA_FILENAME = "remote_config_defaults.json";
		private const string PREFS_PROPJECT_STRING_ID = "SnipeProjectStringID";

		public enum AndriodSubPlatform
		{
			GooglePlay,
			Amazon,
			RuStore,
			Nutaku,
		}

		public enum WebGLSubPlatform
		{
			None,
			Nutaku,
		}

		private MockApplicationInfo _appInfo;
		private string _projectStringID;
		private string _filePath;
		private RuntimePlatform _platform;
		private AndriodSubPlatform _androidSubplatform;
		private WebGLSubPlatform _webglSubplatform;
		private string _content;

		[MenuItem("Snipe/Config...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeConfigDownloader>("Snipe Config");
		}

		protected void OnEnable()
		{
			_projectStringID = EditorPrefs.GetString(GetProjectStringIdKey());
			_filePath = Path.Combine(Application.streamingAssetsPath, SA_FILENAME);

			_platform = Application.platform;
			_appInfo = new MockApplicationInfo();

			ReadConfigFile();
		}

		private static string GetProjectStringIdKey()
		{
			return $"{Application.identifier}.{PREFS_PROPJECT_STRING_ID}";
		}

		private async void ReadConfigFile()
		{
			try
			{
				_content = await File.ReadAllTextAsync(_filePath);
			}
			catch (Exception)
			{
				_content = string.Empty;
			}
		}

		private void OnGUI()
		{
			EditorGUILayout.Space();
			
			EditorGUIUtility.labelWidth = 100;
			
			string projectStringID = EditorGUILayout.TextField("Project String ID", _projectStringID).Trim();
			if (projectStringID != _projectStringID)
			{
				if (projectStringID.EndsWith("_dev"))
				{
					projectStringID = projectStringID.Substring(0, projectStringID.Length - 4);
				}
				else if (projectStringID.EndsWith("_live"))
				{
					projectStringID = projectStringID.Substring(0, projectStringID.Length - 5);
				}

				_projectStringID = projectStringID;
				EditorPrefs.SetString(GetProjectStringIdKey(), _projectStringID);
			}
			
			EditorGUILayout.Space();

			_appInfo.ApplicationIdentifier = EditorGUILayout.TextField("App Identifier", _appInfo.ApplicationIdentifier);
			_appInfo.ApplicationVersion = EditorGUILayout.TextField("App Version", _appInfo.ApplicationVersion);

			var platform = (RuntimePlatform)EditorGUILayout.EnumPopup("Platform", _platform);
			var androidSubplatform = _androidSubplatform;
			var webglSubplatform = _webglSubplatform;

			if (platform == RuntimePlatform.Android)
			{
				androidSubplatform = (AndriodSubPlatform)EditorGUILayout.EnumPopup("SubPlatform", _androidSubplatform);
				webglSubplatform = WebGLSubPlatform.None;
			}
			else if (platform == RuntimePlatform.WebGLPlayer)
			{
				androidSubplatform = AndriodSubPlatform.GooglePlay;
				webglSubplatform = (WebGLSubPlatform)EditorGUILayout.EnumPopup("SubPlatform", _webglSubplatform);
			}
			else
			{
				androidSubplatform = AndriodSubPlatform.GooglePlay;
				webglSubplatform = WebGLSubPlatform.None;
			}

			if (_platform != platform || _androidSubplatform != androidSubplatform || _webglSubplatform != webglSubplatform)
			{
				_platform = platform;
				_androidSubplatform = androidSubplatform;
				_webglSubplatform = webglSubplatform;
				_appInfo.ApplicationPlatform = GetPlaftomString();
			}

			EditorGUILayout.Space();
			
			EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_projectStringID));
			EditorGUILayout.BeginHorizontal();

			EditorGUI.BeginDisabledGroup(true);
			GUILayout.Label(_appInfo.ApplicationPlatform);
			EditorGUI.EndDisabledGroup();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Download"))
			{
				OnDownloadButtonPressed();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();

			if (GUILayout.Button("Download Default"))
			{
				OnDownloadDefaultButtonPressed();
			}

			GUILayout.TextArea(_content);
		}

		private string GetPlaftomString()
		{
			if (_platform == RuntimePlatform.Android)
			{
				string subPlatform = _androidSubplatform != AndriodSubPlatform.GooglePlay ? _androidSubplatform.ToString() : string.Empty;
				return $"{_platform}{subPlatform}";
			}
			
			if (_platform == RuntimePlatform.WebGLPlayer)
			{
				string subPlatform = _webglSubplatform != WebGLSubPlatform.None ? _webglSubplatform.ToString() : string.Empty;
				return $"{_platform}{subPlatform}";
			}

			return $"{_platform}";
		}

		private async void OnDownloadButtonPressed()
		{
			string json = await DownloadConfig();
			await ProcessLoadedConfig(json);
		}

		private async void OnDownloadDefaultButtonPressed()
		{
			string json = await DownloadDefaultConfig();
			await ProcessLoadedConfig(json);
		}

		private async Task ProcessLoadedConfig(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				Debug.LogError("Config fetching error. Invalid JSON");
				return;
			}

			_content = json;

			await File.WriteAllTextAsync(_filePath, json);
		}

		private async Task<string> DownloadConfig()
		{
			Debug.Log("DownloadConfig - start");

			if (string.IsNullOrWhiteSpace(_projectStringID))
			{
				Debug.LogError("DownloadConfig - project ID not specified");
				return null;
			}

			var loader = new SnipeConfigLoader(_projectStringID, _appInfo);
			var config = await loader.Load();
			if (config == null)
			{
				Debug.LogError("DownloadConfig - null");
				return null;
			}

			string json = fastJSON.JSON.ToNiceJSON(config);

			Debug.Log(json);
			Debug.Log("DownloadConfig - done");

			return json;
		}

		private async Task<string> DownloadDefaultConfig()
		{
			Debug.Log("DownloadDefaultConfig - start");

			if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
			{
				SnipeAuthKey.Load();

				if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
				{
					Debug.LogError("DownloadDefaultConfig - API Key not specified");
					return null;
				}
			}

			string contentString;

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/clientConfigDefaultStrings";
				loader.Timeout = TimeSpan.FromSeconds(10);

				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadDefaultConfig - FAILED - HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return null;
				}

				contentString = await response.Content.ReadAsStringAsync();
				Debug.Log("DownloadDefaultConfig - loaded: " + contentString);
			}

			if (string.IsNullOrEmpty(contentString))
			{
				Debug.LogError("DownloadDefaultConfig - downloaded content is empty");
				return null;
			}

			int startIndex = contentString.IndexOf('{', 1);
			int endIndex = contentString.LastIndexOf('}');
			endIndex = contentString.LastIndexOf('}', endIndex - 1) + 1;
			string json = contentString.Substring(startIndex, endIndex - startIndex);

			// Pretyfy JSON
			var obj = fastJSON.JSON.Parse(json);
			json = fastJSON.JSON.ToNiceJSON(obj);
			
			Debug.Log(json);
			Debug.Log("DownloadDefaultConfig - done");

			return json;
		}
	}
}

#endif // UNITY_EDITOR
