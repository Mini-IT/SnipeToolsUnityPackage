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
	public class SnipeRemoteConfigDownloader : EditorWindow
	{
		private const string SA_FILENAME = "remote_config_defaults.json";

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
		private string _filePath;
		private RuntimePlatform _platform;
		private AndriodSubPlatform _androidSubplatform;
		private WebGLSubPlatform _webglSubplatform;
		private string _content;
		private Vector2 _contentScrollPosition;

		[MenuItem("Snipe/Remote Config...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeRemoteConfigDownloader>("Snipe Remote Config");
		}

		protected void OnEnable()
		{
			SnipeToolsConfig.Load();

			_filePath = Path.Combine(Application.streamingAssetsPath, SA_FILENAME);

			_platform = Application.platform;
			_appInfo = new MockApplicationInfo();

			ReadConfigFile();
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

			EditorGUIUtility.labelWidth = 200;

			SnipeToolsConfig.LoadDefaultConfigOnBuild = EditorGUILayout.Toggle("Load Default Config On Build", SnipeToolsConfig.LoadDefaultConfigOnBuild);
			
			EditorGUILayout.Space();

			EditorGUIUtility.labelWidth = 100;

			if (SnipeToolsConfig.LoadDefaultConfigOnBuild)
			{
				SnipeToolsGUI.DrawAuthKeyWidget();
			}
			else
			{
				SnipeToolsGUI.DrawProjectStringIDWidget();
			}

			EditorGUILayout.Space();

			if (!SnipeToolsConfig.LoadDefaultConfigOnBuild)
			{
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

				EditorGUILayout.BeginHorizontal();

				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(SnipeToolsConfig.ProjectStringID));
				if (GUILayout.Button($"Download {_appInfo.ApplicationPlatform}"))
				{
					OnDownloadButtonPressed();
				}
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndHorizontal();
			}
			else
			{
				EditorGUILayout.BeginHorizontal();

				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(SnipeToolsConfig.AuthKey));
				if (GUILayout.Button("Download Default"))
				{
					OnDownloadDefaultButtonPressed();
				}
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndHorizontal();
			}

			_contentScrollPosition = EditorGUILayout.BeginScrollView(_contentScrollPosition, GUILayout.ExpandHeight(true));
			GUILayout.TextArea(_content);
			EditorGUILayout.EndScrollView();
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
			string json = await DownloadPlatformConfig();
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

		private async Task<string> DownloadPlatformConfig()
		{
			Debug.Log("DownloadPlatformConfig - start");

			if (string.IsNullOrWhiteSpace(SnipeToolsConfig.ProjectStringID))
			{
				Debug.LogError("DownloadPlatformConfig - project ID not specified");
				return null;
			}

			var loader = new SnipeConfigLoader(SnipeToolsConfig.ProjectStringID, _appInfo);
			var config = await loader.Load();
			if (config == null)
			{
				Debug.LogError("DownloadPlatformConfig - null");
				return null;
			}

			string json = fastJSON.JSON.ToNiceJSON(config);

			Debug.Log(json);
			Debug.Log("DownloadPlatformConfig - done");

			return json;
		}

		private async Task<string> DownloadDefaultConfig()
		{
			Debug.Log("DownloadDefaultConfig - start");

			string contentString = await RequestDefaultConfig();

			if (string.IsNullOrEmpty(contentString))
			{
				Debug.LogError("DownloadDefaultConfig - downloaded content is empty");
				return null;
			}

			Debug.Log("DownloadDefaultConfig - loaded: " + contentString);

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

		private static async Task<string> RequestDefaultConfig()
		{
			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/clientConfigDefaultStrings";
				loader.Timeout = TimeSpan.FromSeconds(10);

				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadDefaultConfig - FAILED - HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return null;
				}

				return await response.Content.ReadAsStringAsync();
			}
		}
	}
}

#endif // UNITY_EDITOR
