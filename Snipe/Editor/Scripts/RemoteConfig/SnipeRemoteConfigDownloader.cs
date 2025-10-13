#if UNITY_EDITOR && SNIPE_6_1_OR_NEWER

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;
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
		private static string s_filePath;
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

		public static void InitFilePath()
		{
			s_filePath = Path.Combine(Application.streamingAssetsPath, SA_FILENAME);
		}

		protected void OnEnable()
		{
			SnipeToolsConfig.Load();

			InitFilePath();

			_platform = Application.platform;
			_appInfo = new MockApplicationInfo();

			ReadConfigFile();
		}

		private async void ReadConfigFile()
		{
			try
			{
				_content = await File.ReadAllTextAsync(s_filePath);
			}
			catch (Exception)
			{
				_content = string.Empty;
			}
		}

		public void CreateGUI()
		{
			SnipeToolsConfig.Load();

			var root = rootVisualElement;
			var baseStyle = UIUtility.LoadStyleSheet("base");
			if (baseStyle != null)
			{
				root.styleSheets.Add(baseStyle);
			}

			var tree = UIUtility.LoadUxml("SnipeRemoteConfigDownloader");
			if (tree != null)
			{
				tree.CloneTree(root);
			}

			var toggleLoadDefault = root.Q<Toggle>("load-default");
			var sectionCustom = root.Q<VisualElement>("custom-config-section");
			var sectionDefault = root.Q<VisualElement>("default-config-section");
			var appIdField = root.Q<TextField>("app-identifier");
			var appVersionField = root.Q<TextField>("app-version");
			var platformField = root.Q<EnumField>("platform");
			var androidSubField = root.Q<EnumField>("android-subplatform");
			var webglSubField = root.Q<EnumField>("webgl-subplatform");
			var downloadPlatformButton = root.Q<Button>("download-platform");
			var downloadDefaultButton = root.Q<Button>("download-default");
			var contentField = root.Q<TextField>("content");

			contentField.isReadOnly = true;
			contentField.multiline = true;

			toggleLoadDefault.value = SnipeToolsConfig.LoadDefaultConfigOnBuild;
			toggleLoadDefault.RegisterValueChangedCallback(evt =>
			{
				SnipeToolsConfig.LoadDefaultConfigOnBuild = evt.newValue;
				SnipeToolsConfig.Save();
				UpdateSectionsVisibility(sectionCustom, sectionDefault, evt.newValue);
				UpdateButtonsState(downloadPlatformButton, downloadDefaultButton);
			});

			appIdField.value = _appInfo.ApplicationIdentifier;
			appIdField.RegisterValueChangedCallback(evt => _appInfo.ApplicationIdentifier = evt.newValue);
			appVersionField.value = _appInfo.ApplicationVersion;
			appVersionField.RegisterValueChangedCallback(evt => _appInfo.ApplicationVersion = evt.newValue);

			platformField.Init(_platform);
			androidSubField.Init(_androidSubplatform);
			webglSubField.Init(_webglSubplatform);

			platformField.RegisterValueChangedCallback(evt =>
			{
				_platform = (RuntimePlatform)evt.newValue;
				UpdateSubplatformVisibility(platformField, androidSubField, webglSubField);
				UpdateAppInfoPlatform();
			});

			androidSubField.RegisterValueChangedCallback(evt =>
			{
				_androidSubplatform = (AndriodSubPlatform)evt.newValue;
				UpdateAppInfoPlatform();
			});

			webglSubField.RegisterValueChangedCallback(evt =>
			{
				_webglSubplatform = (WebGLSubPlatform)evt.newValue;
				UpdateAppInfoPlatform();
			});

			downloadPlatformButton.clicked += OnDownloadButtonPressed;
			downloadDefaultButton.clicked += OnDownloadDefaultButtonPressed;

			UpdateSectionsVisibility(sectionCustom, sectionDefault, SnipeToolsConfig.LoadDefaultConfigOnBuild);
			UpdateSubplatformVisibility(platformField, androidSubField, webglSubField);
			UpdateButtonsState(downloadPlatformButton, downloadDefaultButton);
			RenderContent(contentField);
		}

		private void UpdateSectionsVisibility(VisualElement sectionCustom, VisualElement sectionDefault, bool loadDefault)
		{
			sectionCustom.style.display = loadDefault ? DisplayStyle.None : DisplayStyle.Flex;
			sectionDefault.style.display = loadDefault ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void UpdateSubplatformVisibility(EnumField platformField, EnumField androidSubField, EnumField webglSubField)
		{
			if (_platform == RuntimePlatform.Android)
			{
				androidSubField.style.display = DisplayStyle.Flex;
				webglSubField.style.display = DisplayStyle.None;
			}
			else if (_platform == RuntimePlatform.WebGLPlayer)
			{
				androidSubField.style.display = DisplayStyle.None;
				webglSubField.style.display = DisplayStyle.Flex;
			}
			else
			{
				androidSubField.style.display = DisplayStyle.None;
				webglSubField.style.display = DisplayStyle.None;
			}
		}

		private void UpdateButtonsState(Button downloadPlatformButton, Button downloadDefaultButton)
		{
			bool authValid = !string.IsNullOrWhiteSpace(SnipeToolsConfig.AuthKey);
			downloadDefaultButton.SetEnabled(authValid);
			downloadPlatformButton.SetEnabled(true);
		}

		private void RenderContent(TextField contentField)
		{
			contentField.value = _content ?? string.Empty;
		}

		private void UpdateAppInfoPlatform()
		{
			if (_platform == RuntimePlatform.Android)
			{
				_webglSubplatform = WebGLSubPlatform.None;
			}
			else if (_platform == RuntimePlatform.WebGLPlayer)
			{
				_androidSubplatform = AndriodSubPlatform.GooglePlay;
			}
			else
			{
				_androidSubplatform = AndriodSubPlatform.GooglePlay;
				_webglSubplatform = WebGLSubPlatform.None;
			}

			_appInfo.ApplicationPlatform = GetPlaftomString();
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
			_content = await CheckAndSaveLoadedConfig(json);
		}

		private async void OnDownloadDefaultButtonPressed()
		{
			string json = await DownloadDefaultConfig();
			_content = await CheckAndSaveLoadedConfig(json);
		}

		public static async Task<string> CheckAndSaveLoadedConfig(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				Debug.LogError("Config fetching error. Invalid JSON");
				return null;
			}

			if (string.IsNullOrEmpty(s_filePath))
			{
				InitFilePath();
			}

			await File.WriteAllTextAsync(s_filePath, json);

			return json;
		}

		private async Task<string> DownloadPlatformConfig()
		{
			Debug.Log("DownloadPlatformConfig - start");

			if (!SnipeToolsConfig.Initialized)
			{
				Debug.LogError("DownloadPlatformConfig - project ID not specified");
				return null;
			}

			if (!SnipeServices.IsInitialized)
			{
				SnipeServices.Initialize(new UnitySnipeServicesFactory());
			}

			string projectStringID = SnipeToolsConfig.GetProjectStringID();
			var loader = new SnipeConfigLoader(projectStringID, _appInfo);
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

		public static async Task<string> DownloadDefaultConfig(string targetPlatform = null)
		{
			Debug.Log("DownloadDefaultConfig - start");

			string contentString = await RequestDefaultConfig(targetPlatform);

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

		public static async Task<string> DownloadAndSaveDefaultConfig(string targetPlatform)
		{
			string json = await DownloadDefaultConfig(targetPlatform);
			return await CheckAndSaveLoadedConfig(json);
		}

		private static async Task<string> RequestDefaultConfig(string targetPlatform)
		{
			string projectStringID = SnipeToolsConfig.GetProjectStringID();

			using var loader = new HttpClient();

			loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
			loader.Timeout = TimeSpan.FromSeconds(10);

			string url = string.IsNullOrEmpty(targetPlatform)
				? $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/clientConfigDefaultStrings"
				: $"https://config.snipe.dev/api/v1/buildConfigStrings/{projectStringID}/{targetPlatform}";

			Debug.Log("Download config: " + url);

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

#endif // UNITY_EDITOR
