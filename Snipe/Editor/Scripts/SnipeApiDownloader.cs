#if UNITY_EDITOR

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
	public class SnipeApiDownloader : EditorWindow
	{
#if SNIPE_8_0_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V8";
#elif SNIPE_7_1_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V71";
#elif SNIPE_7_0_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V7";
#else
		private const string SNIPE_VERSION_SUFFIX = "V61";
#endif

		private const string SERVICE_FILE_NAME = "SnipeApiService.cs";

		private string _directoryPath;

		private TextField _directoryField;
		private Button _browseButton;
		private Label _versionLabel;
		private Button _downloadButton;
		private AuthKeyWidget _authKeyWidget;

		[MenuItem("Snipe/Download SnipeApi...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeApiDownloader>("SnipeApi");
		}

		// protected void OnEnable()
		// {
		// 	SnipeToolsConfig.Load();
		//
		// 	FindSnipeApiDirectory();
		//
		// 	if (SnipeAutoUpdater.AutoUpdateEnabled)
		// 	{
		// 		SnipeAutoUpdater.CheckUpdateAvailable();
		// 	}
		// }

		private void FindSnipeApiDirectory()
		{
			string[] results = AssetDatabase.FindAssets("SnipeApi");
			if (results != null && results.Length > 0)
			{
				for (int i = 0; i < results.Length; i++)
				{
					string path = AssetDatabase.GUIDToAssetPath(results[i]);

					if (path.EndsWith(SERVICE_FILE_NAME))
					{
						// Application.dataPath ends with "Assets"
						// path starts with "Assets" and ends with "SnipeApiService.cs"
						_directoryPath = Application.dataPath + path.Substring(6, path.Length - 24);
						break;
					}
				}
			}

			if (string.IsNullOrEmpty(_directoryPath))
			{
				_directoryPath = Application.dataPath;
			}
		}

		public void CreateGUI()
		{
			SnipeToolsConfig.Load();

			FindSnipeApiDirectory();

			if (SnipeAutoUpdater.AutoUpdateEnabled)
			{
				SnipeAutoUpdater.CheckUpdateAvailable();
			}

			//-------

			var root = rootVisualElement;
			UIUtility.LoadUI(root, "SnipeApiDownloader", "base");

			_directoryField = root.Q<TextField>("directory");
			_browseButton = root.Q<Button>("btn-browse");
			_versionLabel = root.Q<Label>("version-label");
			_downloadButton = root.Q<Button>("btn-download");
			_authKeyWidget = root.Q<AuthKeyWidget>("auth-key-widget");

			_versionLabel.text = "Snipe API Service Version: " + SNIPE_VERSION_SUFFIX;

			_directoryField.value = string.IsNullOrEmpty(_directoryPath) ? Application.dataPath : _directoryPath;
			_directoryField.RegisterValueChangedCallback(evt => _directoryPath = evt.newValue);

			_browseButton.clicked += () =>
			{
				string filename = SERVICE_FILE_NAME;
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
					_directoryField.value = path;
				}
			};

			SetControlsEnabled(SnipeToolsConfig.IsAuthKeyValid);

			_downloadButton.clicked += OnDownloadButtonPressed;
		}

		private void SetControlsEnabled(bool enabled)
		{
			_directoryField?.SetEnabled(enabled);
			_browseButton?.SetEnabled(enabled);
			_downloadButton?.SetEnabled(enabled);
			_authKeyWidget?.SetEnabled(enabled);
		}

		private async void OnDownloadButtonPressed()
		{
			SetControlsEnabled(false);
			try
			{
				await DownloadSnipeApi();
				await Task.Yield();
				AssetDatabase.Refresh();
			}
			finally
			{
				SetControlsEnabled(true);
			}
		}

		public async Task DownloadSnipeApi()
		{
			Debug.Log("DownloadSnipeApi - start");

			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				Debug.LogError("DownloadSnipeApi - FAILED to get token");
				return;
			}

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/code/unityBindings{SNIPE_VERSION_SUFFIX}";

				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadSnipeApi - FAILED to get token; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return;
				}

				string filePath = Path.Combine(_directoryPath, SERVICE_FILE_NAME);

				using (StreamWriter sw = File.CreateText(filePath))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}
			}

			Debug.Log("DownloadSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
