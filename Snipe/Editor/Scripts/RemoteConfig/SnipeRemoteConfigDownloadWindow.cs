#if UNITY_EDITOR && SNIPE_6_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeRemoteConfigDownloadWindow : EditorWindow
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
			Yandex,
		}

		private readonly Dictionary<string, string> _platformList = new()
		{
			["amazon"] = "Android (Amazon)",
			["android"] = "Android (Google Play)",
			["androidNutaku"] = "Android (Nutaku)",
			["rustore"] = "Android (RuStore)",
			["huawei"] = "Android (Huawei)",
			["ios"] = "iOS",
			["linux"] = "Linux",
			["macos"] = "macOS",
			["ps4"] = "PS4",
			["ps5"] = "PS5",
			["steam"] = "Steam",
			["switch"] = "Switch",
			["editor"] = "Unity Editor",
			["webgl"] = "WebGL",
			["webglNutaku"] = "WebGL (Nutaku)",
			["webglYandex"] = "WebGL (Yandex)",
			["windows"] = "Windows (UWP)",
			["xboxone"] = "Xbox One",
		};

		private MockApplicationInfo _appInfo;
		private static string s_filePath;
		private RuntimePlatform _platform;
		private AndriodSubPlatform _androidSubplatform;
		private WebGLSubPlatform _webglSubplatform;
		private string _content;
		private Vector2 _contentScrollPosition;
		private TextField _contentField;
		private DropdownField _targetPlatformDropdown;
		private string _selectedTargetPlatform;

		[MenuItem("Snipe/Remote Config...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeRemoteConfigDownloadWindow>("Snipe Remote Config");
		}

		public static void InitFilePath()
		{
			s_filePath = Path.Combine(Application.streamingAssetsPath, SA_FILENAME);
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

			RefreshContentField();
		}

		private void RefreshContentField()
		{
			if (_contentField != null)
			{
				_contentField.value = _content;
			}
		}

		public void CreateGUI()
		{
			SnipeToolsConfig.Load();

			InitFilePath();

			_platform = Application.platform;
			_appInfo = new MockApplicationInfo();

			var root = rootVisualElement;
			UIUtility.LoadUI(root, "SnipeRemoteConfigDownloadWindow", "base");

			var toggleLoadDefault = root.Q<Toggle>("load-on-build-toggle");
			var sectionRuntime = root.Q<VisualElement>("runtime-config-section");
			var sectionBuildtime = root.Q<VisualElement>("buildtime-config-section");
			var appIdField = root.Q<TextField>("app-identifier");
			var appVersionField = root.Q<TextField>("app-version");
			var platformField = root.Q<EnumField>("platform");
			var androidSubField = root.Q<EnumField>("android-subplatform");
			var webglSubField = root.Q<EnumField>("webgl-subplatform");
			var downloadRuntimeButton = root.Q<Button>("btn-download-runtime");
			var downloadBuildtimeButton = root.Q<Button>("btn-download-buildtime");
			_targetPlatformDropdown = root.Q<DropdownField>("target-platform");
			_contentField = root.Q<TextField>("content");

			_contentField.isReadOnly = true;
			_contentField.multiline = true;

			toggleLoadDefault.value = SnipeToolsConfig.LoadDefaultConfigOnBuild;
			toggleLoadDefault.RegisterValueChangedCallback(evt =>
			{
				SnipeToolsConfig.LoadDefaultConfigOnBuild = evt.newValue;
				SnipeToolsConfig.Save();
				UpdateSectionsVisibility(sectionRuntime, sectionBuildtime, evt.newValue);
				UpdateButtonsState(downloadRuntimeButton, downloadBuildtimeButton);
			});
			// Populate target platform dropdown (default-config section)
			if (_targetPlatformDropdown != null)
			{
				var displayNames = new List<string>(_platformList.Values);
				_targetPlatformDropdown.choices = displayNames;
				// Default to first value
				_selectedTargetPlatform = new List<string>(_platformList.Keys)[0];
				_targetPlatformDropdown.value = _platformList[_selectedTargetPlatform];
				_targetPlatformDropdown.RegisterValueChangedCallback(evt =>
				{
					// map selected display value back to key
					foreach (var kv in _platformList)
					{
						if (kv.Value == evt.newValue)
						{
							_selectedTargetPlatform = kv.Key;
							break;
						}
					}
				});
			}

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

			downloadRuntimeButton.clicked += OnDownloadRuntimeButtonPressed;
			downloadBuildtimeButton.clicked += OnDownloadBuildtimeButtonPressed;

			UpdateSectionsVisibility(sectionRuntime, sectionBuildtime, SnipeToolsConfig.LoadDefaultConfigOnBuild);
			UpdateSubplatformVisibility(platformField, androidSubField, webglSubField);
			UpdateButtonsState(downloadRuntimeButton, downloadBuildtimeButton);

			ReadConfigFile();
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

		private async void OnDownloadRuntimeButtonPressed()
		{
			string json = await DownloadRuntimeConfig();
			_content = await CheckAndSaveLoadedConfig(json);
			RefreshContentField();
		}

		private async void OnDownloadBuildtimeButtonPressed()
		{
			if (string.IsNullOrEmpty(_selectedTargetPlatform))
			{
				Debug.LogError("Target platform must be specified");
				return;
			}
			string json = await DownloadBuildtimeConfig(_selectedTargetPlatform);
			_content = await CheckAndSaveLoadedConfig(json);
			RefreshContentField();
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

		private async Task<string> DownloadRuntimeConfig()
		{
			Debug.Log("DownloadRuntimeConfig - start");

			if (!SnipeToolsConfig.Initialized)
			{
				Debug.LogError("DownloadRuntimeConfig - project ID not specified");
				return null;
			}

			if (!SnipeServices.IsInitialized)
			{
				SnipeServices.Initialize(new UnitySnipeServicesFactory());
			}

			string projectStringID = SnipeToolsConfig.GetProjectStringID();

			if (string.IsNullOrEmpty(projectStringID))
			{
				Debug.LogError("DownloadRuntimeConfig - Failed extracting ProjectStringID");
				return null;
			}

			var loader = new SnipeConfigLoader(projectStringID, _appInfo);
			var config = await loader.Load();
			if (config == null)
			{
				Debug.LogError("DownloadRuntimeConfig - config is null");
				return null;
			}

			string json = fastJSON.JSON.ToNiceJSON(config);

			LogJson(config);
			Debug.Log("DownloadRuntimeConfig - done");

			return json;
		}

		public static async Task<string> DownloadBuildtimeConfig(string targetPlatform)
		{
			Debug.Log("DownloadBuildtimeConfig - start");

			if (string.IsNullOrEmpty(targetPlatform))
			{
				Debug.LogError("DownloadBuildtimeConfig - targetPlatform is required");
				return null;
			}

			string contentString = await RequestDefaultConfig(targetPlatform);

			if (string.IsNullOrEmpty(contentString))
			{
				Debug.LogError("DownloadBuildtimeConfig - downloaded content is empty");
				return null;
			}

			Debug.Log("DownloadBuildtimeConfig - loaded: " + contentString);

			int startIndex = contentString.IndexOf('{', 1);
			int endIndex = contentString.LastIndexOf('}');
			endIndex = contentString.LastIndexOf('}', endIndex - 1) + 1;
			string json = contentString.Substring(startIndex, endIndex - startIndex);

			// Pretyfy JSON
			var conf = (Dictionary<string, object>)fastJSON.JSON.Parse(json);
			json = fastJSON.JSON.ToNiceJSON(conf);

			LogJson(conf);
			Debug.Log("DownloadBuildtimeConfig - done");

			return json;
		}

		public static async Task<string> DownloadAndSaveDefaultConfig(string targetPlatform)
		{
			string json = await DownloadBuildtimeConfig(targetPlatform);
			return await CheckAndSaveLoadedConfig(json);
		}

		private static async Task<string> RequestDefaultConfig(string targetPlatform)
		{
			string projectStringID = SnipeToolsConfig.GetProjectStringID();

			using var loader = new HttpClient();

			loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
			loader.Timeout = TimeSpan.FromSeconds(10);

			if (string.IsNullOrEmpty(targetPlatform))
			{
				Debug.LogError("RequestDefaultConfig - targetPlatform is required");
				return null;
			}

			string url = $"https://config.snipe.dev/api/v1/buildConfigStrings/{projectStringID}/{targetPlatform}";

			Debug.Log("Download config: " + url);

			var response = await loader.GetAsync(url);

			if (!response.IsSuccessStatusCode)
			{
				Debug.LogError($"DownloadBuildtimeConfig - FAILED - HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
				return null;
			}

			return await response.Content.ReadAsStringAsync();
		}

		/// <summary>
		/// Special logging method for Unity Dashboard
		/// </summary>
		/// <param name="dict"></param>
		private static void LogJson(Dictionary<string, object> dict)
		{
			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("---- RC values ---- " + dict.Count);
			foreach (var pair in dict)
			{
				stringBuilder.AppendFormat("  {0} = {1}", pair.Key, pair.Value);
				stringBuilder.AppendLine();
			}
			Debug.Log(stringBuilder);
		}
	}
}

#endif // UNITY_EDITOR
